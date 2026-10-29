# L2Points

Webová aplikace pro CP partu hrající Lineage II (L2Reborn – Eternal). Eviduje účast na epic bossech (normal i chaotic) a Castle Siege, přiděluje za ni body a ukazuje žebříček účasti v procentech.

Návrh a pravidla: [docs/superpowers/specs/2026-10-28-l2points-design.md](docs/superpowers/specs/2026-10-28-l2points-design.md), plán implementace: [docs/superpowers/plans/2026-10-28-00-roadmap.md](docs/superpowers/plans/2026-10-28-00-roadmap.md).

Stav: hotové části 1 až 7 (kostra, doména, Můj týden, přehledy, šperky, admin, nasazení do Azure). Nasazení do Azure je připravené v repu (`infra/`, `.github/workflows/`), první reálné zřízení a nasazení zatím neproběhlo.

## Požadavky

- .NET 10 SDK (viz `global.json`)
- Lokálně SQL Server LocalDB (součást Visual Studia), nebo Docker

## Lokální spuštění

```bash
dotnet run --project src/L2Points.Web
```

Aplikace běží na http://localhost:5167 (profil `https` navíc na https://localhost:7100). Používá LocalDB `(localdb)\MSSQLLocalDB`, databázi `L2Points`. Migrace se aplikují automaticky při startu.

Při prvním startu se vytvoří admin z konfigurace `Admin` (login `admin`, nick `Admin`). Heslo je v `src/L2Points.Web/appsettings.Development.json` a je určené jen pro vývoj.

Testy a build:

```bash
dotnet build
dotnet test
```

## Docker

```bash
cp .env.example .env     # a upravit hesla
docker compose up --build
```

Aplikace poběží na http://localhost:8080 a SQL Server v samostatném kontejneru. První admin se vytvoří z proměnných `ADMIN_USERNAME`, `ADMIN_PASSWORD` a `ADMIN_NICK` v `.env`. Image i compose ověřuje smoke test v CI (`scripts/smoke-test.sh`), ten ale zatím neběžel; lokálně Docker ověřený není.

Health endpointy: `/health/live` (proces běží, bez kontrol), `/health/ready` (včetně databáze) a `/health` jako alias `/health/ready`.

## Nasazení

Produkce běží v Azure (Container Apps + Azure SQL, image v GHCR). Infrastrukturu zřizuje ručně `infra/deploy.ps1` (Bicep v `infra/main.bicep`), nové verze nasazuje GitHub Actions po pushi na `main`:

```powershell
pwsh ./infra/deploy.ps1 -SubscriptionId <subscription-id>
```

Celý postup (první zřízení, běžné nasazení, logy, obnova databáze ze zálohy, náklady, smazání prostředí) je v [docs/deployment.md](docs/deployment.md).

## Nová migrace

```bash
dotnet tool install --global dotnet-ef   # jednorázově
dotnet ef migrations add <Name> -p src/L2Points.Infrastructure -s src/L2Points.Web -o Persistence/Migrations
```

Další informace pro vývoj jsou v [CLAUDE.md](CLAUDE.md).
