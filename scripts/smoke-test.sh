#!/usr/bin/env bash
# Smoke test of a built L2Points image against SQL Server in a container (docker compose).
# Usage: scripts/smoke-test.sh <image>
# Checks: health live/ready, /login renders the Czech UI, case-sensitive static files,
# tzdata (Europe/Prague) and ICU in the image, no fail/crit log entries from the web container.
set -euo pipefail

if [[ $# -ne 1 || -z "$1" ]]; then
  echo "Usage: $0 <image>" >&2
  exit 2
fi

IMAGE="$1"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BASE_URL="http://localhost:8080"
TIMEOUT_SECONDS=240

WORK_DIR="$(mktemp -d)"
ENV_FILE="$WORK_DIR/smoke.env"

# Separate project name and env file so a developer's own .env and database volume stay untouched.
compose() {
  docker compose -p l2points-smoke -f "$REPO_ROOT/docker-compose.yml" --env-file "$ENV_FILE" "$@"
}

# Random passwords that stay inside this run. Only [A-Za-z0-9_] so nothing breaks the connection string,
# the env file or the shell in the db healthcheck. The fixed prefixes guarantee the required character classes
# (SQL Server: upper, lower, digit, symbol; Identity: digit and lower case).
MSSQL_SA_PASSWORD="Sm0ke_$(openssl rand -hex 16)"
ADMIN_PASSWORD="adm1n_$(openssl rand -hex 12)"

cat > "$ENV_FILE" <<ENV
L2POINTS_IMAGE=$IMAGE
MSSQL_SA_PASSWORD=$MSSQL_SA_PASSWORD
ADMIN_USERNAME=smokeadmin
ADMIN_PASSWORD=$ADMIN_PASSWORD
ADMIN_NICK=Smoke
ENV

on_exit() {
  local status=$?
  if [[ $status -ne 0 ]]; then
    echo "Smoke test FAILED (exit $status). Logs of all services:" >&2
    compose logs --no-color --tail 200 >&2 || true
  fi
  compose down -v --remove-orphans >/dev/null 2>&1 || true
  rm -rf "$WORK_DIR"
  exit "$status"
}
trap on_exit EXIT

fail() {
  echo "FAIL: $*" >&2
  exit 1
}

http_code() {
  curl -s -o /dev/null -w '%{http_code}' --max-time 5 "$1" || true
}

web_running() {
  local id
  id="$(compose ps -q web)"
  [[ -n "$id" && "$(docker inspect -f '{{.State.Running}}' "$id")" == "true" ]]
}

wait_for_200() {
  local url="$1" deadline="$2" code
  while true; do
    code="$(http_code "$url")"
    if [[ "$code" == "200" ]]; then
      echo "OK: $url -> 200"
      return 0
    fi
    if ! web_running; then
      fail "web container is not running (last response from $url: $code)"
    fi
    if (( SECONDS >= deadline )); then
      fail "$url did not return 200 within ${TIMEOUT_SECONDS}s (last: $code)"
    fi
    sleep 3
  done
}

echo "Starting SQL Server and $IMAGE ..."
compose up -d --no-build

# Migrations and seeding run before the HTTP server listens, so the first start can take a while.
deadline=$(( SECONDS + TIMEOUT_SECONDS ))
wait_for_200 "$BASE_URL/health/live" "$deadline"
wait_for_200 "$BASE_URL/health/ready" "$deadline"

# Login page: status 200 and the Czech heading from Components/Account/Login.razor.
body_file="$WORK_DIR/login.html"
code="$(curl -sS -o "$body_file" -w '%{http_code}' --max-time 10 "$BASE_URL/login")"
[[ "$code" == "200" ]] || fail "/login returned $code"
grep -qF 'Přihlášení' "$body_file" || fail "/login does not contain the Czech heading"
echo "OK: /login -> 200 with Czech heading"

# Static file with a case-sensitive name (Linux file system).
headers_file="$WORK_DIR/image-headers.txt"
code="$(curl -sS -o /dev/null -D "$headers_file" -w '%{http_code}' --max-time 10 "$BASE_URL/images/jewels/AntharasEarring.png")"
[[ "$code" == "200" ]] || fail "/images/jewels/AntharasEarring.png returned $code"
grep -qiE '^content-type: *image/png' "$headers_file" || fail "/images/jewels/AntharasEarring.png is not served as image/png"
echo "OK: /images/jewels/AntharasEarring.png -> 200 image/png"

# Blazor framework script (missing when the framework web assets were not restored).
headers_file="$WORK_DIR/blazor-headers.txt"
code="$(curl -sS -o /dev/null -D "$headers_file" -w '%{http_code}' --max-time 10 "$BASE_URL/_framework/blazor.web.js")"
[[ "$code" == "200" ]] || fail "/_framework/blazor.web.js returned $code"
grep -qiE '^content-type: *text/javascript' "$headers_file" || fail "/_framework/blazor.web.js is not served as JavaScript"
echo "OK: /_framework/blazor.web.js -> 200 text/javascript"

# Time zone data and ICU in the runtime image.
docker run --rm --entrypoint sh "$IMAGE" -c 'test -f /usr/share/zoneinfo/Europe/Prague && ls /usr/lib/*/libicuuc.so.*' \
  || fail "image is missing tzdata (Europe/Prague) or ICU"
echo "OK: tzdata and ICU present"

# No error or critical log entries (startup migrations and seeding included); simple and JSON console formats.
logs_file="$WORK_DIR/web.log"
compose logs --no-color web > "$logs_file"
if grep -E '(^|[[:space:]|])(fail|crit): |"LogLevel": *"(Error|Critical)"' "$logs_file"; then
  fail "web container logged errors"
fi
echo "OK: no fail/crit log entries"

echo "Smoke test passed."
