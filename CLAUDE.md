# L2Points

Webová aplikace pro CP partu (command channel) hrající Lineage II (server L2Reborn – Eternal). Eviduje účast na epic bossech (normal i chaotic) a Castle Siege a přiděluje za ni body. Body se nikdy neodečítají ani nenulují, jde o čistou historii účasti, ze které se pozná, kdo si epický šperk nejvíc zaslouží.

- Spec (zdroj pravdy pro doménová pravidla): `docs/superpowers/specs/2026-10-28-l2points-design.md`
- Roadmapa částí implementace: `docs/superpowers/plans/2026-10-28-00-roadmap.md`
- Stav: hotové části 1 až 6 (kostra, doména a persistence, stránka Můj týden, přehledy: Přehled, Týden – všichni, Detail hráče, šperky: galerie a admin přiřazení, admin: hráči, oprava účasti, aktivity a body, rozpis, nastavení). Zbývá část 7 (Azure).

## Struktura solution

`L2Points.slnx`, .NET 10 (`global.json`: SDK 10.0.204, rollForward latestMinor), společné nastavení v `Directory.Build.props` (Nullable, ImplicitUsings).

- `src/L2Points.Domain` – entity, enumy a čistá logika, bez závislosti na EF Core, UI i `AppUser` (viz sekce Doména a persistence).
- `src/L2Points.Infrastructure` – EF Core + SQL Server (`Persistence/`: `AppDbContext`, `Configurations/`, `ReferenceDataSeeder`, `CatalogQueries`, `DatabaseInitializer`, `Migrations/`), ASP.NET Core Identity (`Identity/`: `AppUser`, `Roles`, `AdminOptions`, `AppSignInManager`, `IdentitySeeder`, `PasswordGenerator`, `PrincipalValidator` – platí přihlášení dál: uživatel existuje, je aktivní a sedí security stamp), aplikační služby (`Services/`: `AttendanceService` – týdenní mřížka a změna účasti s kontrolou pravidel úprav na serveru, `PlayerStatsService` – body a procenta hráče za týden, `JewelService` – galerie a přiřazení šperků, `CatalogAdminService` – admin úpravy aktivit, bodů, rozpisu, kotvy A/V a začátku evidence, `PlayerAdminService` – správa hráčů, každá operace ve vlastním DI scope kvůli `UserManager`), sdílené dotazy `Persistence/JewelQueries` a `PlayerQueries.SelectPlayerInfo`, DI přes `AddInfrastructure`.
- `src/L2Points.Web` – Blazor Web App (MudBlazor 9.x), `Program.cs`, stránky, layout; aplikační služby jen volá. Účtové stránky (`Components/Account`: `/login`, `/profile`) jsou statické SSR. `Identity/IdentityRevalidatingAuthenticationStateProvider` každou minutu ověří přihlášení běžícího circuitu přes `PrincipalValidator` (smazaný nebo deaktivovaný účet neprojde, reset hesla, deaktivace i změna role rotují security stamp; hráč se tak do minuty odhlásí).
  - Znovupoužitelné komponenty jsou v `Components/Attendance`: `WeekGridView` (mřížka, jen zobrazení a události), `EditableWeekGrid` (načtení, ukládání a obnova mřížky ze serveru pro cílového hráče; používá ho i `/admin/attendance`), `WeekSummaryCard` (body a procenta), `WeekNavigator` (přepínání týdnů). Formátování textů pro UI a odkazy na detail hráče (`PlayerUrl`, id escapované) jsou ve `Formatting/DisplayFormat`.
  - Stránky přehledů (část 4): `/leaderboard` (Přehled – žebříček celkově a za týden, `?week=`, `?inactive=1`), `/week` (Týden – všichni: tabulka hráčů a události týdne, `?week=`), `/players/{Id}` (Detail hráče: souhrny, historie po týdnech, jen čtecí mřížka účasti vybraného týdne, `?week=`). Data berou jen přes `OverviewService` a `PlayerStatsService`, mřížku přes `AttendanceService`.
  - `Components/Stats/StatCells` vykreslí buňky tabulky (body + pět procent) pro jednoho hráče a období; poměr „započteno / očekáváno“ je v nativním `title` (ne `MudTooltip`, na dlouhých tabulkách by bylo desítky popoverů). Sloupec s nickem je přilepený vlevo kvůli vodorovnému scrollu na mobilu, nick s ikonami šperků vykresluje `Components/Stats/PlayerNickCell`; styl `.l2p-nick-cell` je globální v `wwwroot/app.css`, protože scoped CSS na `MudTd`/`MudTh` nedosáhne.
  - Šperky (část 5): `Domain/Jewels` (`JewelType`, `UserJewel`, `DefaultJewels` – výchozí katalog, `JewelCalculator` – seskupení). Hráč může mít víc kusů i stejného typu, bez data; odebrání odebere jeden kus. `Components/Jewels` (`JewelIcon`, `JewelIcons`) vykreslí ikonu s počtem. Stránky `/jewels` (galerie: kdo co drží) a `/admin/jewels` (jen role Admin: výběr hráče v `?player=`, přidat/odebrat kus; odkaz z `/admin`). Ikony jsou ve `wwwroot/images/jewels/{Code}.png` (z l2j.ru). Nový typ šperku = přidat do `DefaultJewels` + soubor ikony, `JewelImageTests` to hlídá.
  - Admin (část 6, všechny stránky jen role Admin, `[Authorize(Roles = Roles.Admin)]`): `/admin` (rozcestník), `/admin/players` (hráči: založení s vygenerovaným heslem zobrazeným jednou, úprava nicku a „aktivní od“, reset hesla, de/aktivace, role Admin, smazání s potvrzením), `/admin/attendance` (oprava účasti komukoli přes `EditableWeekGrid` s `ActorIsAdmin`), `/admin/activities` (body, přepsání bodů slotů, očekávané počty, aktivní), `/admin/schedule` (rozpis chaotic v UTC s časem v Praze, kotva Antharas/Valakas), `/admin/settings` (začátek evidence), `/admin/jewels` (část 5). Dialogy jsou v `Components/Admin`, zadávání času `Formatting/TimeInput` (`H:mm`/`HH:mm`). DB jen přes `CatalogAdminService` a `PlayerAdminService`.
- `tests/L2Points.Domain.Tests` – xUnit, čistá doménová logika.
- `tests/L2Points.Infrastructure.Tests` – xUnit nad SQLite in-memory (Identity, seed katalogu, mapování).
- `tests/L2Points.Web.Tests` – xUnit, čistá logika webu bez UI (`Formatting/DisplayFormat`, `Formatting/TimeInput`, `PlayerUrl`, `JewelImageTests` – každý seedovaný šperk má ikonu).

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
- Přístup k DB: interaktivní komponenty a aplikační služby používají `IDbContextFactory<AppDbContext>` (krátce žijící kontext na jednu operaci) nebo `OwningComponentBase`, nikdy `AppDbContext` injektovaný přímo do komponenty (v Blazor circuitu by žil celé spojení). Explicitní transakce jen přes `Database.CreateExecutionStrategy().ExecuteAsync(...)` kvůli `EnableRetryOnFailure`. `UserManager` v interaktivních komponentách je scoped na circuit, proto `PlayerAdminService` vytváří scope na každou operaci a stránky z circuitového `UserManager` berou jen `GetUserId` z principalu.
- Testy běží na SQLite in-memory a neodhalí rozdíly proti SQL Serveru (collation – unikátní Nick je na SQL Serveru case-insensitive, typy). Unikátnost nicku proto `PlayerAdminService` validuje case-insensitive i v aplikačním kódu (porovnání v paměti, SQLite `upper()` neumí diakritiku).
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
- Admin nemění sám sebe: nesmí se deaktivovat, smazat ani si odebrat roli Admin. Tím vždy zůstane aspoň jeden aktivní admin (ten, kdo akci provádí); kontrola je v `PlayerAdminService`, ne počítáním adminů.

## Doména a persistence

- Kde je logika: `src/L2Points.Domain` – `Time` (`AppTime`, `Week`, `DateRange`), `Activities` (entity, `DefaultCatalog`, `CatalogRules` – validace admin úprav katalogu), `Attendances` (`Attendance`, `EditWindow`, `AttendanceRules`, `WeekGridBuilder`), `Schedule` (rozpis), `Stats` (body a procenta), `Jewels` (typy šperků, `DefaultJewels`, `JewelCalculator`), `Settings`. Čisté statické kalkulátory bez I/O, čas dostávají parametrem nebo přes `TimeProvider`. Domain nezná `AppUser`: FK `Attendance.UserId` → `AspNetUsers` je nakonfigurovaná v Infrastructure (`HasOne<AppUser>()`), stejně `UserJewel.UserId` → `AspNetUsers` (cascade); `UserJewel.JewelTypeId` → `JewelTypes` je `Restrict`.
- `EventSlot` (`None`, `Evening`, `Morning`) je součástí klíče účasti. Aktivity bez slotů (normal epic, CS) používají `None`, chaotic `Evening`/`Morning`. Ukládá se jako `int`, hodnoty enumů neměnit.
- Všechna `DateTime` v DB jsou UTC a čtou se s `DateTimeKind.Utc` (`UtcDateTimeConverter` registrovaný přes `ConfigureConventions`). Kalendářní dny jsou `DateOnly`, časy spawnů `TimeOnly` v UTC.
- Seed katalogu běží za běhu (`ReferenceDataSeeder` po migraci a `IdentitySeeder`), ne přes `HasData`. Přidá jen chybějící aktivity (podle `Code`), typy šperků (podle `Code`) a nastavení, existující hodnoty (body, očekávané počty, rozpis) nikdy nepřepisuje, protože je edituje admin.
- Body vs. procenta: body = součet bodových hodnot účastí podle aktuálních hodnot aktivity (případně override v `ActivitySlotPoints`), bez stropu. Procenta = započtené účasti ÷ očekávané výskyty, u aktivity za týden nejvýš do očekávaného počtu. Počítá se v `Stats`, ne v DB ani UI.
- Přidání nové aktivity: přidat ji do `DefaultCatalog.CreateActivities()` s unikátním `Code` (max 32) a novým `SortOrder`; při dalším startu ji seed doplní i do existující DB. Nový `ScheduleType` vyžaduje úpravu `ScheduleCalculator` a testů. Změna už existující aktivity v katalogu se do seedované DB nepromítne, ta se mění přes admin UI.
- Globální začátek evidence je v `AppSettings` (`TrackingStartDate`, `yyyy-MM-dd`), čte se přes `CatalogQueries.GetTrackingStartAsync`; katalog přes `CatalogQueries.LoadActivitiesAsync`.
- `IdentitySeeder` nastavuje adminovi `ActiveFrom` na pondělí aktuálního týdne v Europe/Prague.
- Datum výskytu = UTC datum spawnu. Shoduje se s datem v Europe/Prague jen dokud je spawn `TimeUtc` < 22:00 UTC (letní čas). Aktuální katalog má spawny v rozmezí 01:30–20:00 UTC; admin editaci rozpisu proto `CatalogRules.ValidateSpawns` omezuje na `TimeUtc < 22:00` (`SpawnTimeLimitUtc`).
- Aktivitu vyřazuj deaktivací (`IsActive = false`), nikdy ji nemaž: seeder při startu znovu vytvoří chybějící kódy z katalogu a FK z účastí je `Restrict`.
- Typy šperků se také nikdy nemažou (FK `Restrict`, seeder chybějící kódy obnoví). Nový typ: přidat do `DefaultJewels.CreateJewelTypes()` a ikonu `wwwroot/images/jewels/{Code}.png` (hlídá `JewelImageTests`, názvy souborů rozlišují velikost písmen).

## Workflow

Superpowers: spec → plán části → implementace. Každá část z roadmapy má vlastní plán v `docs/superpowers/plans/` (psaný až podle aktuálního stavu kódu). Pracuje se na feature větvích, malé úpravy lze bez plného procesu.

## Známé TODO / pozor

- Data Protection klíče se ukládají do DB nešifrované (část 7, Azure: zvážit Key Vault / ochranu klíčů).
- Dockerfile a docker-compose zatím nebyly spuštěny.
