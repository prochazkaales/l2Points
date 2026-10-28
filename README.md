# L2Points

Webová aplikace pro CP partu hrající Lineage II (L2Reborn – Eternal). Eviduje účast na epic bossech (normal i chaotic) a Castle Siege, přiděluje za ni body a ukazuje žebříček účasti v procentech.

Návrh a pravidla: [docs/superpowers/specs/2026-10-28-l2points-design.md](docs/superpowers/specs/2026-10-28-l2points-design.md), plán implementace: [docs/superpowers/plans/2026-10-28-00-roadmap.md](docs/superpowers/plans/2026-10-28-00-roadmap.md).

Stav: hotová kostra (přihlášení, změna hesla, layout s přepínačem dark/light, placeholder stránky, `/health`). Funkce se doplňují po částech podle roadmapy.

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

Aplikace poběží na http://localhost:8080 (health check na `/health`) a SQL Server v samostatném kontejneru. První admin se vytvoří z proměnných `ADMIN_USERNAME`, `ADMIN_PASSWORD` a `ADMIN_NICK` v `.env`. Docker konfigurace zatím nebyla ověřena spuštěním.

## Nová migrace

```bash
dotnet tool install --global dotnet-ef   # jednorázově
dotnet ef migrations add <Name> -p src/L2Points.Infrastructure -s src/L2Points.Web -o Persistence/Migrations
```

Další informace pro vývoj jsou v [CLAUDE.md](CLAUDE.md).
