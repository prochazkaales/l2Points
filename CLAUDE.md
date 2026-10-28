# L2Points

Webová aplikace pro CP partu (command channel) hrající Lineage II (server L2Reborn – Eternal). Eviduje účast na epic bossech (normal i chaotic) a Castle Siege a přiděluje za ni body. Body se nikdy neodečítají ani nenulují, jde o čistou historii účasti, ze které se pozná, kdo si epický šperk nejvíc zaslouží.

- Spec (zdroj pravdy pro doménová pravidla): `docs/superpowers/specs/2026-10-28-l2points-design.md`
- Roadmapa částí implementace: `docs/superpowers/plans/2026-10-28-00-roadmap.md`
- Stav: hotové části 1 až 4 (kostra, doména a persistence, stránka Můj týden, přehledy: Přehled, Týden – všichni, Detail hráče). Další stránky přijdou v částech 5 a 6.

## Struktura solution

`L2Points.slnx`, .NET 10 (`global.json`: SDK 10.0.204, rollForward latestMinor), společné nastavení v `Directory.Build.props` (Nullable, ImplicitUsings).

- `src/L2Points.Domain` – entity, enumy a čistá logika, bez závislosti na EF Core, UI i `AppUser` (viz sekce Doména a persistence).
- `src/L2Points.Infrastructure` – EF Core + SQL Server (`Persistence/`: `AppDbContext`, `Configurations/`, `ReferenceDataSeeder`, `CatalogQueries`, `DatabaseInitializer`, `Migrations/`), ASP.NET Core Identity (`Identity/`: `AppUser`, `Roles`, `AdminOptions`, `AppSignInManager`, `IdentitySeeder`), aplikační služby (`Services/`: `AttendanceService` – týdenní mřížka a změna účasti s kontrolou pravidel úprav na serveru, `PlayerStatsService` – body a procenta hráče za týden), DI přes `AddInfrastructure`.
- `src/L2Points.Web` – Blazor Web App (MudBlazor 9.x), `Program.cs`, stránky, layout; aplikační služby jen volá. Účtové stránky (`Components/Account`: `/login`, `/profile`) jsou statické SSR.
  - Znovupoužitelné komponenty jsou v `Components/Attendance`: `WeekGridView` (mřížka, jen zobrazení a události), `EditableWeekGrid` (načtení, ukládání a obnova mřížky ze serveru pro cílového hráče; použije i část 6), `WeekSummaryCard` (body a procenta), `WeekNavigator` (přepínání týdnů). Formátování textů pro UI a odkazy na detail hráče (`PlayerUrl`, id escapované) jsou ve `Formatting/DisplayFormat`.
  - Stránky přehledů (část 4): `/leaderboard` (Přehled – žebříček celkově a za týden, `?week=`, `?inactive=1`), `/week` (Týden – všichni: tabulka hráčů a události týdne, `?week=`), `/players/{Id}` (Detail hráče: souhrny, historie po týdnech, jen čtecí mřížka účasti vybraného týdne, `?week=`). Data berou jen přes `OverviewService` a `PlayerStatsService`, mřížku přes `AttendanceService`.
  - `Components/Stats/StatCells` vykreslí buňky tabulky (body + pět procent) pro jednoho hráče a období; poměr „započteno / očekáváno“ je v nativním `title` (ne `MudTooltip`, na dlouhých tabulkách by bylo desítky popoverů). Sloupec s nickem je přilepený vlevo přes scoped CSS (`::deep`) kvůli vodorovnému scrollu na mobilu.
- `tests/L2Points.Domain.Tests` – xUnit, čistá doménová logika.
- `tests/L2Points.Infrastructure.Tests` – xUnit nad SQLite in-memory (Identity, seed katalogu, mapování).

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
- Účtové stránky (login, profil) jsou statické SSR kvůli cookie, ostatní stránky Interactive Server. Prerender je vypnutý (`InteractiveServerRenderMode(prerender: false)` v `App.razor`): stránky načítají data jen jednou přes circuit, ne dvakrát (prerender + interaktivní start), a nic se neblikne se starými daty. Výchozí autorizace je „přihlášený uživatel“, anonymní jsou jen login, chybová stránka (Error), `/health` a statické soubory.
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

## Doména a persistence

- Kde je logika: `src/L2Points.Domain` – `Time` (`AppTime`, `Week`, `DateRange`), `Activities` (entity, `DefaultCatalog`), `Attendances` (`Attendance`, `EditWindow`, `AttendanceRules`, `WeekGridBuilder`), `Schedule` (rozpis), `Stats` (body a procenta), `Settings`. Čisté statické kalkulátory bez I/O, čas dostávají parametrem nebo přes `TimeProvider`. Domain nezná `AppUser`: FK `Attendance.UserId` → `AspNetUsers` je nakonfigurovaná v Infrastructure (`HasOne<AppUser>()`).
- `EventSlot` (`None`, `Evening`, `Morning`) je součástí klíče účasti. Aktivity bez slotů (normal epic, CS) používají `None`, chaotic `Evening`/`Morning`. Ukládá se jako `int`, hodnoty enumů neměnit.
- Všechna `DateTime` v DB jsou UTC a čtou se s `DateTimeKind.Utc` (`UtcDateTimeConverter` registrovaný přes `ConfigureConventions`). Kalendářní dny jsou `DateOnly`, časy spawnů `TimeOnly` v UTC.
- Seed katalogu běží za běhu (`ReferenceDataSeeder` po migraci a `IdentitySeeder`), ne přes `HasData`. Přidá jen chybějící aktivity (podle `Code`) a chybějící nastavení, existující hodnoty (body, očekávané počty, rozpis) nikdy nepřepisuje, protože je edituje admin.
- Body vs. procenta: body = součet bodových hodnot účastí podle aktuálních hodnot aktivity (případně override v `ActivitySlotPoints`), bez stropu. Procenta = započtené účasti ÷ očekávané výskyty, u aktivity za týden nejvýš do očekávaného počtu. Počítá se v `Stats`, ne v DB ani UI.
- Přidání nové aktivity: přidat ji do `DefaultCatalog.CreateActivities()` s unikátním `Code` (max 32) a novým `SortOrder`; při dalším startu ji seed doplní i do existující DB. Nový `ScheduleType` vyžaduje úpravu `ScheduleCalculator` a testů. Změna už existující aktivity v katalogu se do seedované DB nepromítne, ta se mění přes admin UI.
- Globální začátek evidence je v `AppSettings` (`TrackingStartDate`, `yyyy-MM-dd`), čte se přes `CatalogQueries.GetTrackingStartAsync`; katalog přes `CatalogQueries.LoadActivitiesAsync`.
- `IdentitySeeder` nastavuje adminovi `ActiveFrom` na pondělí aktuálního týdne v Europe/Prague.
- Datum výskytu = UTC datum spawnu. Shoduje se s datem v Europe/Prague jen dokud je spawn `TimeUtc` < 22:00 UTC (letní čas). Aktuální katalog má spawny v rozmezí 01:30–20:00 UTC; admin editace rozpisu (část 6) musí validovat `TimeUtc < 22:00`.
- Aktivitu vyřazuj deaktivací (`IsActive = false`), nikdy ji nemaž: seeder při startu znovu vytvoří chybějící kódy z katalogu a FK z účastí je `Restrict`.

## Workflow

Superpowers: spec → plán části → implementace. Každá část z roadmapy má vlastní plán v `docs/superpowers/plans/` (psaný až podle aktuálního stavu kódu). Pracuje se na feature větvích, malé úpravy lze bez plného procesu.

## Známé TODO / pozor

- Deaktivace hráče musí volat `UpdateSecurityStampAsync` a Blazor circuity potřebují `RevalidatingServerAuthenticationStateProvider`, jinak běžící spojení deaktivaci nepoznají (část 6).
- Data Protection klíče se ukládají do DB nešifrované (část 7, Azure: zvážit Key Vault / ochranu klíčů).
- Dockerfile a docker-compose zatím nebyly spuštěny.
