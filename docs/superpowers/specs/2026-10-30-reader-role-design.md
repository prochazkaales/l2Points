# Role Čtenář – design

Datum: 30. 10. 2026. Doplňuje hlavní spec `2026-10-28-l2points-design.md` (role a oprávnění).

## Proč

Admin chce mít účet, přes který může někomu ukázat web, aniž by ten člověk do čehokoli zasahoval. Zároveň chce umět přepnout existujícího hráče do režimu „jen čtení“: nic už nevyplní, ale jeho historie zůstane. Dnes je každý účet hráč, který si zapisuje účast a mění si heslo; účet jen na prohlížení neexistuje.

## Cíle

- Nová role **Čtenář** (`Reader`). Čtenář vidí celý web jako běžný hráč, ale nic nezmění: nezapíše účast, nezmění si heslo, do adminu nemá přístup.
- U čtenáře přepínač **Zobrazovat ve statistikách**:
  - **zapnutý** – čtenář dál figuruje všude jako hráč (žebříček, Týden – všichni, galerie šperků, Detail hráče, čísla party), body i procenta se mu počítají stejně jako hráči;
  - **vypnutý** – čtenář nikde ve statistikách není, jako by neexistoval.
- Admin založí nový účet rovnou jako čtenáře a existující účet přepne hráč ↔ čtenář.
- Přepnutím na čtenáře se nic nesmaže (účast, šperky, body). Přepnutím zpátky na hráče se vše vrátí.

## Ne-cíle

- Žádné zmrazení procent: čtenáři se procenta počítají dál podle rozpisu a protože nic nevyplní, klesají (rozhodnutí uživatele). Žádné datum „čtenářem od“ se neukládá.
- Žádný anonymní nebo veřejný přístup bez přihlášení; čtenář je normální účet s heslem od admina.
- Žádná nová stránka; čtenář vidí stejné stránky jako hráč.

## Anti-cíle (selhání, i kdyby to technicky fungovalo)

- Čtenář dokáže cokoli změnit, i obejitím UI (přímé volání služby, starý formulář, circuit se zpožděnou rolí). Kontrola musí být na serveru proti stavu v DB.
- Skrytý čtenář prosákne do statistik (počet hráčů v KPI, průměry, stupně vítězů, galerie, nabídka hráčů).
- Přepnutí na čtenáře smaže nebo upraví účast či šperky.

## Chování

### Pro čtenáře

- **Můj týden (`/`)**: stejný vzhled jako u hráče – mřížka týdne, karta bodů a procent, přepínání týdnů. Všechny buňky jsou zamčené (stav jako zamčená buňka v editovatelné mřížce) a nad mřížkou je upozornění, např. „Jsi přihlášen jako čtenář. Účast tady nejde zapisovat.“ Mřížka ukazuje čtenářovu vlastní účast (u přepnutého hráče jeho historii, u nového čtenáře prázdnou). Karta bodů ukazuje jeho vlastní čísla i tehdy, když je ve statistikách skrytý.
- **Profil (`/profile`)**: stránka zůstane, formulář změny hesla je zamčený (disabled) se stejným typem upozornění. Server změnu hesla čtenáři odmítne i při odeslání formuláře mimo UI.
- **Přehled, Týden – všichni, Detail hráče, Šperky, Body**: jen ke čtení jako u hráče.
- **Admin**: bez přístupu (čtenář nemá roli Admin).
- Menu je stejné jako u hráče (bez admin sekce).

### Ve statistikách

- **Čtenář zobrazovaný ve statistikách** se chová přesně jako hráč: je v žebříčku, v Týden – všichni, v galerii šperků, v číslech party a má Detail hráče. Na Detailu hráče má vedle titulku čip „čtenář“ (stejně jako dnes čip „neaktivní“). V tabulkách se nijak neoznačuje.
- **Skrytý čtenář** není v žádném výpočtu ani výpisu: `OverviewService` (žebříček, týden, KPI, události týdne a jejich účastníci), `JewelService` (galerie včetně součtů kusů), `PlayerStatsService.GetPlayerDetailAsync` (Detail hráče vrátí „hráč nenalezen“), ikony šperků u nicků.
- „Hráč nenalezen“ na Detailu skrytého čtenáře platí **pro všechny, i pro admina** (rozhodnutí designu: skrytý = nikde). Služba proto nepotřebuje vědět, kdo se dívá. V Admin → Hráči se u skrytého čtenáře nick nevykreslí jako odkaz na detail (prostý text); odkazy „Detail hráče“ na `/admin/attendance` a `/admin/jewels` se ho netýkají, protože tam skrytý čtenář v nabídce není.

### Admin

- **Admin → Hráči**:
  - Při založení účtu výběr typu **Hráč / Čtenář**. U čtenáře checkbox „Zobrazovat ve statistikách“, u nového čtenáře výchozí **vypnuto** (nový účet na ukázku nemá co ukazovat).
  - U existujícího účtu akce **přepnout na čtenáře / přepnout na hráče**. Při přepnutí hráče na čtenáře je „Zobrazovat ve statistikách“ výchozí **zapnuto** (historie zůstává vidět, dokud ji admin neskryje). Přepínač jde u čtenáře kdykoli změnit.
  - V seznamu hráčů má čtenář čip „čtenář“, skrytý čtenář navíc čip „skrytý“ (přesný text nechán na builderovi). Seznam v Admin → Hráči ukazuje všechny účty včetně skrytých čtenářů.
  - Reset hesla, de/aktivace, smazání, úprava nicku a „aktivní od“ fungují u čtenáře stejně jako u hráče.
- **Kdo smí**: přepínat hráč ↔ čtenář a měnit „Zobrazovat ve statistikách“ smí každý existující aktivní admin (jako ostatní správu běžných hráčů). Platí stávající pořadí kontrol v `PlayerAdminService` (aktér → neznámý hráč → vlastní účet → super admin → admin).
- **Admin nemůže být čtenář**:
  - Účet s rolí Admin (včetně super admina) nejde přepnout na čtenáře; nejdřív mu super admin musí roli Admin odebrat.
  - Čtenáři nejde udělit roli Admin; nejdřív se musí přepnout na hráče.
  - Admin nepřepne sám sebe (plyne z předchozího, ale kontrola `CannotChangeSelf` zůstává).
- **Oprava účasti (`/admin/attendance`) a šperky (`/admin/jewels`)**:
  - Čtenář zobrazovaný ve statistikách v nabídce je a admin mu účast i šperky upravit smí (je ve statistikách, opravy musí jít).
  - Skrytý čtenář v nabídce není a server jeho úpravy odmítne (účast i šperky). Po přepnutí zpátky na hráče nebo zapnutí zobrazení jde upravit zase.

### Přihlášení a změna role

- Přepnutí hráč ↔ čtenář rotuje security stamp (jako dnes změna role), takže otevřený circuit se do minuty odhlásí (`IdentityRevalidatingAuthenticationStateProvider`) a po novém přihlášení má správné role. Změna „Zobrazovat ve statistikách“ stamp nerotuje (čtenáři se nic nemění v oprávněních).
- Kontroly na serveru čtou stav čtenáře z DB, ne z claimů v cookie, aby neplatila zpožděná role. Rozsah (rozhodnutí designu): (a) zápis účasti, když je aktér čtenář; (b) změna hesla čtenáře; (c) úprava účasti a šperků skrytého čtenáře adminem. `JewelService` kvůli tomu jen zkontroluje cíl (skrytý čtenář), parametr aktéra nedostává.
- Mimo rozsah: obecná minutová mezera, kdy circuit bývalého admina ještě nese claim Admin a admin stránky (šperky, katalog, `Create`/`Update` hráče) neověřují aktéra z DB. Existuje už dnes při odebrání role Admin, čtenář ji nezhoršuje; zapsat do roadmapy jako odložené.

## Model (rozhodnuté)

- Nová Identity role `Roles.Reader = "Reader"`. Čtenář má roli `Reader` **místo** `Player` (ne obě). Přepnutí = výměna `Player` ↔ `Reader`. `IdentitySeeder` roli `Reader` vytvoří spolu s ostatními.
- `IdentitySeeder` u účtu z konfigurace (`Admin__UserName`) kromě dnešních rolí odebere případnou roli `Reader`, zajistí `Player` a nastaví `ShowInStats = true`.
- Nový sloupec `AppUser.ShowInStats` (`bool`, výchozí `true`, EF migrace s default `true` pro existující řádky). Invariant: hráč (ne čtenář) má vždy `ShowInStats = true`; služba ho při přepnutí na hráče nastaví zpět na `true`. Díky tomu stačí ve statistických dotazech filtr `u.ShowInStats` bez joinu na role.
- Rozšířit `AdminPlayer` o příznaky čtenáře a zobrazení, `PlayerAdminService` o metody pro přepnutí typu a změnu zobrazení, založení účtu o typ a zobrazení. Nové stavy výsledků (např. `AdminCannotBeReader`, `ReaderCannotBeAdmin` v `PlayerAdminStatus`, `ReadOnlyAccount` v `AttendanceChangeResult`, odpovídající stav v `JewelChangeResult`) – přesné názvy nechány na builderovi.
- `AttendanceService.SetAttendanceAsync`: odmítne, když je aktér čtenář (bez ohledu na cíl), a když je cíl skrytý čtenář. Zamčení mřížky se řídí tím, jestli je čtenář **aktér** (ten, kdo se dívá), ne cíl: admin v `/admin/attendance` má mřížku zobrazovaného čtenáře normálně upravitelnou. `AttendanceService.GetWeekGridAsync` musí umět vrátit mřížku s všemi buňkami zamčenými pro čtenáře (stav buněk dnes vychází z `WeekGridBuilder`/`GridCellStates`; jak přesně zamknout, je na builderovi, ale zamčení musí platit i pro dnešek a okno úprav).

## Na builderovi

- Přesné texty upozornění a čipů (česky, v souladu se stávajícím UI).
- Jak v UI vypadá výběr typu a přepínač (dialog založení a úprav v `Components/Admin`), z MudBlazor komponent, dark/light, mobil, dotykové cíle 44 px.
- Rozdělení na doménu vs. infrastrukturu a testy: TDD pro pravidla (kdo smí co, odmítnutí zápisu, filtrování ve statistikách), testy služeb nad SQLite. Pro čisté UI TDD ne.
- Aktualizace `CLAUDE.md` (role, doménová pravidla) a hlavního specu o roli Čtenář.
