# L2Points

Webová aplikace pro CP partu (command channel) hrající Lineage II (server L2Reborn – Eternal). Eviduje účast na epic bossech (normal i chaotic) a Castle Siege a přiděluje za ni body. Body se nikdy neodečítají ani nenulují, jde o čistou historii účasti, ze které se pozná, kdo si epický šperk nejvíc zaslouží.

- Spec (zdroj pravdy pro doménová pravidla): `docs/superpowers/specs/2026-10-28-l2points-design.md`
- Roadmapa částí implementace: `docs/superpowers/plans/2026-10-28-00-roadmap.md`
- Stav: hotová část 1 (kostra). Doménová logika a stránky přijdou v částech 2 až 6.

## Struktura solution

`L2Points.slnx`, .NET 10 (`global.json`: SDK 10.0.204, rollForward latestMinor), společné nastavení v `Directory.Build.props` (Nullable, ImplicitUsings).

- `src/L2Points.Domain` – entity, enumy a čistá logika (rozpis týdne, body, %, okno úprav). Bez závislosti na EF Core a UI. Zatím prázdný.
- `src/L2Points.Infrastructure` – EF Core + SQL Server (`Persistence/`: `AppDbContext`, `DatabaseInitializer`, `Migrations/`), ASP.NET Core Identity (`Identity/`: `AppUser`, `Roles`, `AdminOptions`, `AppSignInManager`, `IdentitySeeder`), DI přes `AddInfrastructure`.
- `src/L2Points.Web` – Blazor Web App (MudBlazor 9.x), `Program.cs`, stránky, layout, aplikační služby. Účtové stránky (`Components/Account`: `/login`, `/profile`) jsou statické SSR.
- `tests/L2Points.Domain.Tests` – xUnit, čistá doménová logika (zatím bez testů).
- `tests/L2Points.Infrastructure.Tests` – xUnit nad SQLite in-memory (Identity, seed).

## Příkazy

```bash
dotnet build                      # celé řešení
dotnet test                       # všechny testy
dotnet run --project src/L2Points.Web    # Development, http://localhost:5167 (profil https: https://localhost:7100)
```

Nová EF migrace (potřeba `dotnet tool install --global dotnet-ef`, spouštět z kořene repa):

```bash
dotnet ef migrations add <Name> -p src/L2Points.Infrastructure -s src/L2Points.Web -o Persistence/Migrations
```

Migrace se aplikují automaticky při startu aplikace (`DatabaseInitializer`), ručně `database update` není potřeba.

Docker (Docker na vývojovém stroji zatím není nainstalovaný, build image není ověřený):

```bash
cp .env.example .env              # upravit hesla
docker compose up --build         # web na http://localhost:8080, health na /health
```

Konfigurace: `ConnectionStrings__Default`, `Admin__UserName`, `Admin__Password`, `Admin__Nick`. Lokálně LocalDB `(localdb)\MSSQLLocalDB`, databáze `L2Points` (`src/L2Points.Web/appsettings.json`). Dev admin je v `src/L2Points.Web/appsettings.Development.json` (login `admin`, nick `Admin`).

## Konvence

- Jazyk: UI česky; kód, komentáře, názvy proměnných a větví, commit messages anglicky. Komunikace s uživatelem česky.
- Časy se zobrazují v Europe/Prague, rozpis serveru se ukládá v UTC a převádí se. Aktuální čas jen přes `TimeProvider` (nikdy `DateTime.Now`/`UtcNow`), aby šla logika testovat.
- Účtové stránky (login, profil) jsou statické SSR kvůli cookie, ostatní stránky Interactive Server. Výchozí autorizace je „přihlášený uživatel“, anonymní jsou jen login, chybová stránka (Error), `/health` a statické soubory.
- UI stavět z MudBlazor komponent, podporovat dark/light a mobil.
- Přístup k DB: interaktivní komponenty a aplikační služby používají `IDbContextFactory<AppDbContext>` (krátce žijící kontext na jednu operaci) nebo `OwningComponentBase`, nikdy `AppDbContext` injektovaný přímo do komponenty (v Blazor circuitu by žil celé spojení). Explicitní transakce jen přes `Database.CreateExecutionStrategy().ExecuteAsync(...)` kvůli `EnableRetryOnFailure`. `UserManager` v interaktivních komponentách je scoped na circuit, proto v admin stránkách (část 6) vytvářet scope na každou operaci.
- Testy běží na SQLite in-memory a neodhalí rozdíly proti SQL Serveru (collation – unikátní Nick je na SQL Serveru case-insensitive, typy). Unikátnost nicku proto validovat case-insensitive i v aplikačním kódu (část 6).
- TDD pro doménovou logiku (body, %, rozpis, okno úprav). Pro čisté UI a konfiguraci TDD nepoužívat.
- Konfigurace přes env proměnné, tajemství nepatří do repa (`.env` je v `.gitignore`).

## Doménová pravidla v kostce

Detail a výjimky viz spec, tady jen orientace.

- Událost = aktivita + datum (+ slot Večerní/Ranní u chaotic), je implicitní. Účast je unikátní na (hráč, aktivita, datum, slot).
- Body = součet bodových hodnot účastí podle **aktuálních** hodnot (změna platí zpětně), bez stropu.
- 100 % = hráč byl na všem, co podle rozpisu proběhlo. Procento = započtené účasti ÷ očekávané výskyty (u aktivity za týden nejvýš do očekávaného počtu). Kategorie: Celkem, Normal, Večerní, Ranní, CS.
- Aktuální týden se počítá jen do dneška, celkově od `ActiveFrom` hráče (deaktivovaný jen do `DeactivatedAt`), globálně od začátku evidence (26. 10. 2026).
- Okno úprav: hráč mění jen vlastní účast v rozsahu dnes − 6 dní až dnes (Europe/Prague), kontrola vždy na serveru. Admin bez omezení.
- Antharas/Valakas: každou neděli jeden z nich, střídavě; 1. 11. 2026 = Antharas, 8. 11. 2026 = Valakas.
- Chaotic: 2 sloty (Večerní/Ranní), 40 výskytů týdně.
- Rozpis chaotic, body a očekávané počty jsou v DB a edituje je admin, ne v kódu.
- Role Admin a Player, admin je zároveň hráč. Hesla generuje aplikace, žádná registrace ani e-maily.

## Workflow

Superpowers: spec → plán části → implementace. Každá část z roadmapy má vlastní plán v `docs/superpowers/plans/` (psaný až podle aktuálního stavu kódu). Pracuje se na feature větvích, malé úpravy lze bez plného procesu.

## Známé TODO / pozor

- Deaktivace hráče musí volat `UpdateSecurityStampAsync` a Blazor circuity potřebují `RevalidatingServerAuthenticationStateProvider`, jinak běžící spojení deaktivaci nepoznají (část 6).
- Data Protection klíče se ukládají do DB nešifrované (část 7, Azure: zvážit Key Vault / ochranu klíčů).
- `IdentitySeeder` nastavuje `ActiveFrom` na dnešní datum v UTC, část 2 to upřesní na pondělí týdne v Europe/Prague.
- Dockerfile a docker-compose zatím nebyly spuštěny.
