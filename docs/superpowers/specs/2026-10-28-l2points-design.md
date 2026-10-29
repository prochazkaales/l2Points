# L2Points – návrh aplikace

Datum: 2026-10-28
Stav: návrh ke schválení

## 1. Účel a proč

Webová aplikace pro **CP partu** (command channel) hrající Lineage II na serveru **L2Reborn – Eternal**.
Eviduje, kdo se účastnil kterých epic bossů (normal i chaotic) a Castle Siege, a přiděluje za to body.

Proč: když party získá epický šperk, potřebuje férový a průhledný podklad, kdo si ho nejvíc zaslouží.
Zároveň má být vidět, kdo jak často chodí a kdy kde byl.

Nejde o klanový systém ani o DKP s utrácením bodů. Body se **nikdy neodečítají ani nenulují**, jsou to čistá historie účasti.

## 2. Cíle, ne-cíle, anti-cíle

**Cíle**
- Hráč si sám rychle odklikne týdenní účast na přehledné stránce.
- Každý vidí svoji účast i účast ostatních, za libovolný týden i zpětně.
- Celkový přehled (žebříček) bodů a procentuální účasti od začátku i za týden, včetně rozpadu podle kategorií.
- Admin spravuje hráče, bodové hodnoty, rozpis a přiřazuje šperky. Všichni vidí, kdo má jaký šperk a kolik jich party má.
- Moderní vzhled, přepínač dark/light.
- Běh v Dockeru, později nasazení do Azure.

**Ne-cíle**
- Registrace hráčů zvenku, e-maily, obnova hesla e-mailem.
- Odečítání bodů za získané šperky, aukce, DKP.
- Evidence přesného času killu, dropů nebo lootu (kromě přiřazení šperku).
- Více part / klanů v jedné instanci (multi-tenant).
- Datum získání šperku.

**Anti-cíle (selhání, i kdyby to technicky fungovalo)**
- Zapisování účasti je zdlouhavé. Musí to být pár kliknutí na týden.
- Procenta účasti počítaná relativně k nejaktivnějšímu hráči nebo jen z událostí, kam někdo šel. Uživatel výslovně chce počítat ze **všech** výskytů podle rozpisu.
- Hráč může zpětně přepisovat starou historii.

## 3. Doména

### 3.1 Aktivity (co se dá navštívit)

Všechny časy se v aplikaci **zobrazují v českém čase (Europe/Prague)**. Server L2Reborn běží v UTC. Rozpis se ukládá v UTC a převádí se.

**Normal epicy** – náhodný respawn. Eviduje se jen **den**, ne čas.

| Boss | Respawn (server) | Typ rozpisu |
|---|---|---|
| Queen Ant | 24 h + 0–4 h | flexibilní |
| Core | 36 h + 0–4 h | flexibilní |
| Orfen | 36 h + 0–4 h | flexibilní |
| Zaken | 36 h + 0–4 h | flexibilní |
| Frintezza | 48 h + 0–4 h | flexibilní |
| Baium | sobota 20:00 UTC (+1 h) | týdně, sobota |
| Antharas | neděle, střídá se s Valakasem | ob týden, neděle |
| Valakas | neděle, střídá se s Antharasem | ob týden, neděle |

- **Flexibilní** boss se může zaškrtnout v libovolný den. Má nastavitelný **očekávaný počet výskytů za týden**. Výchozí hodnoty: QA 6, Core 4, Orfen 4, Zaken 4, Frintezza 3. Admin je může změnit.
- **Antharas/Valakas:** neděle **1. 11. 2026 = Antharas**, 8. 11. 2026 = Valakas a dál se střídají. Uloží se jako „kotevní datum“ + perioda 2 týdny, admin to může upravit.

**Chaotic epicy** – pevné časy, **dva sloty**: **Večerní** a **Ranní**.

| Boss | Dny | Večerní (UTC → CZ zima) | Ranní (UTC → CZ zima) |
|---|---|---|---|
| Chaotic Queen Ant | Po–Pá | 18:00 → 19:00 | 01:30 → 02:30 |
| Chaotic Core | Po–Pá | 18:30 → 19:30 | 02:00 → 03:00 |
| Chaotic Orfen | Po–Pá | 19:00 → 20:00 | 02:30 → 03:30 |
| Chaotic Zaken | Po, St, Pá | 19:30 → 20:30 | 03:00 → 04:00 |
| Chaotic Frintezza | Út, Čt | 19:30 → 20:30 | 03:00 → 04:00 |

- Týdně je to celkem 40 chaotic výskytů (QA, Core, Orfen 5×2, Zaken 3×2, Frintezza 2×2).
- Rozpis (boss × den v týdnu × slot × čas UTC) je **uložen v DB a editovatelný adminem**, ne natvrdo v kódu.
- Ranní slot daného dne (např. Po 02:30) patří ke kalendářnímu dni, kdy reálně nastane. Rozpis se seeduje doslova podle stránky serveru („Mon–Fri 01:30 UTC“ = pondělí 02:30 CZ).

**Castle Siege** – každou neděli 18:00 UTC (19:00 CZ zima), 1× týdně.

Zdroj: https://l2reborn.org/server-features/eternal-2/

### 3.2 Události a účast

**Událost** je jednoznačně určená kombinací **aktivita + datum (+ slot u chaotic)**. Nezakládá se, je implicitní.
Všichni hráči, kteří zaškrtnou stejnou kombinaci, byli na stejné události.

**Účast** = záznam „hráč X byl na aktivitě A dne D (ve slotu S)“. Unikátní na (hráč, aktivita, datum, slot).

### 3.3 Body

- Každá aktivita má **bodovou hodnotu**, kterou nastavuje admin. Normal a chaotic varianta bosse jsou samostatné aktivity se samostatnými body, CS má také vlastní hodnotu.
- U chaotic bossů lze body volitelně nastavit **zvlášť pro Večerní a Ranní slot**. Výchozí hodnota je pro oba sloty stejná, admin může jeden přepsat. *(Moje volba: připraveno dopředu, aby se později nemusel měnit model.)*
- Body hráče = součet bodových hodnot jeho účastí.
- Body se počítají vždy podle **aktuálních** hodnot. Změna hodnoty se projeví zpětně. *(Moje volba: hodnoty se budou ještě ladit.)*
- Výchozí hodnoty při seedu: 1 bod za vše (placeholder), skutečné nastaví admin.

### 3.4 Procentuální účast

**100 % = hráč byl na všem, co podle rozpisu proběhlo** (ne relativně k ostatním, ne jen z událostí, kam někdo šel).

Jmenovatel (očekávané výskyty) za týden:
- Chaotic: počet záznamů rozpisu v daném týdnu (standardně 40).
- Flexibilní normal: nastavený očekávaný počet za týden.
- Baium: 1. Antharas/Valakas: 1 podle toho, čí je týden. CS: 1.

Čitatel: počet účastí hráče. U každé aktivity se za týden počítá nejvýš do výše očekávaného počtu, aby procento nepřesáhlo 100 % *(moje volba)*. Body se počítají ze všech účastí bez stropu.

Výpočet procenta *(moje volba, vyplývá z „100 % = byl na všem“)*: **součet započtených účastí ÷ součet očekávaných výskytů** za dané období a kategorii. Bez vážení body, nejde o průměr kategorií. Chaotic tak v „Celkem“ tvoří většinu jmenovatele, což odpovídá realitě.

Zobrazovaná procenta, vždy **za vybraný týden** i **celkově**:
- **Celkem** – všechny aktivity
- **Normal** – normal epicy (QA, Core, Orfen, Zaken, Frintezza, Baium, Antharas/Valakas)
- **Večerní** – chaotic večerní slot
- **Ranní** – chaotic ranní slot
- **CS** – Castle Siege

Pravidla období *(moje volby)*:
- Týden = pondělí–neděle v českém čase.
- **Aktuální týden** se počítá jen do dneška včetně: chaotic/fixní výskyty s datem ≤ dnes. Flexibilní očekávaný počet se poměrně krátí podle uplynulých dnů (zaokrouhleno nahoru). Rozpracovaný týden tak neukazuje uměle nízké procento.
- **Celkové i týdenní procento** se všem hráčům počítá od globálního začátku evidence, bez ohledu na to, kdy byl hráč založen. Datum „aktivní od“ neexistuje; hráč, který se přidal později, má celkové procento nižší, protože se mu počítají i výskyty před jeho založením. *(Změna na žádost uživatele.)*
- Globální **začátek evidence** je nastavení aplikace (výchozí: pondělí 26. 10. 2026). Nic před ním se nepočítá.
- **Deaktivovaný hráč:** jmenovatel se mu počítá jen do data deaktivace (`DeactivatedAt`), takže mu procento dál neklesá. Při reaktivaci se `DeactivatedAt` vymaže a jmenovatel se mu znovu počítá od začátku evidence do dneška, včetně doby neaktivity. *(Moje volba, zjednodušení bez historie období.)*
- **Změny rozpisu a nastavení platí zpětně** stejně jako body. Vše se počítá z aktuální konfigurace, bez „platí od“. Účast na dni nebo slotu, který už v rozpisu není, dál nese body a zobrazuje se, ale do procent se započítá jen do výše očekávaného počtu. Změny serverového rozpisu jsou vzácné a jednoduchost má přednost. *(Moje volba.)*

### 3.5 Okno pro úpravy

- Hráč smí přidat nebo odebrat **vlastní** účast jen pro data v rozsahu **dnes − 6 dní až dnes** (český čas). Starší a budoucí data jsou jen pro čtení.
- Kontrola musí být **na serveru**, ne jen v UI.
- Admin může upravit účast komukoli a bez omezení data.

### 3.6 Šperky

Typy (seed, s obrázkem – originální herní ikona uložená v aplikaci):
Ring of Queen Ant, Ring of Core, Earring of Orfen, Earring of Zaken, Necklace of Frintezza, Ring of Baium, Earring of Antharas, Necklace of Valakas.

- Admin přiřazuje šperk hráči a může ho odebrat. Hráč může mít víc šperků, i stejného typu. **Bez data.**
- Všichni vidí: počet kusů každého šperku v partě a kdo má jaký šperk.
- U nicku v žebříčku se zobrazují ikony jeho šperků.

### 3.7 Uživatelé a role

- Role **Admin**, **Player** a **SuperAdmin**. Registrace zvenku neexistuje.
- Admin zakládá hráče: **login + nick**. **Heslo vygeneruje aplikace**, zobrazí se adminovi jednou a admin ho předá.
- Hráč si po přihlášení může heslo změnit.
- Admin může: resetovat heslo (nové vygenerované), **deaktivovat** hráče (nemůže se přihlásit, historie zůstává, v žebříčku je skrytý s možností zobrazit neaktivní) a **smazat** hráče (smaže se i jeho účast a šperky, s potvrzením).
- Admin z konfigurace (env proměnné `Admin__UserName`, `Admin__Password`, `Admin__Nick`) se vytvoří při startu, pokud neexistuje. Je to **super admin** (role SuperAdmin, právě jeden, v UI se nepřiděluje): při každém startu je aktivní a má role Admin, Player a SuperAdmin, jeho existující heslo se nemění. Při změně `Admin__UserName` přejde role SuperAdmin na nový účet.
- **Admin je zároveň hráč** *(moje volba)*: má svůj týden, objevuje se v žebříčku a počítají se mu body. Role Admin je jen přídavek. Roli Admin uděluje a odebírá na stránce hráčů **jen super admin** *(změna na žádost uživatele)*. Admini na sebe navzájem nesahají: běžný admin nemůže jinému adminovi (ani super adminovi) deaktivovat účet, smazat ho ani resetovat heslo, super admina nemůže omezit nikdo. Super admin smí tyto akce u všech ostatních. Nick, účast a šperky může admin upravit komukoli *(změna na žádost uživatele)*. Žádný admin nemůže deaktivovat ani smazat sám sebe ani si odebrat roli Admin.

## 4. Stránky

Celé UI je v češtině. V hlavičce je navigace, nick přihlášeného, odhlášení a **přepínač dark/light** (výchozí podle systému, volba se pamatuje).

1. **Přihlášení** – login + heslo.
2. **Můj týden** (úvodní stránka)
   - Mřížka: řádky = aktivity seskupené (Normal epicy / Chaotic epicy / Castle Siege), sloupce = Po–Ne s datem.
   - Normal flexibilní: zaškrtávátko v každém dni. Baium jen sobota. Antharas/Valakas jen neděle a jen ten, čí je týden.
   - Chaotic: v dnech, kdy boss spawnuje, dvě zaškrtávátka **Večerní / Ranní** s českým časem.
   - CS: neděle.
   - Navigace mezi týdny (← tento týden →). Dny mimo okno úprav jsou zamčené (vizuálně odlišené).
   - Nahoře souhrn: body za týden a % (Celkem / Normal / Večerní / Ranní / CS).
   - Zaškrtnutí se ukládá okamžitě, bez tlačítka Uložit.
3. **Týden – všichni** – pro vybraný týden matice hráčů × události. Plus tabulka hráčů s body a % za týden.
4. **Přehled (žebříček)** – nick + ikony šperků, body a % (Celkem / Normal / Večerní / Ranní / CS) **celkově i za vybraný týden vedle sebe** (na mobilu přepínač). Řazení podle sloupců, výchozí podle bodů. Volba zobrazit neaktivní hráče.
5. **Detail hráče** – nahoře body a % (všech 5 kategorií) **celkově i za aktuální týden**, pod tím historie po týdnech (body, %), jeho šperky, odkaz na jeho účast v konkrétním týdnu (stejná mřížka jako Můj týden, jen pro čtení).
6. **Šperky** – galerie typů s obrázkem, počtem kusů v partě a seznamem držitelů.
7. **Profil** – změna hesla.
8. **Admin**
   - Hráči: seznam, založit, reset hesla, aktivovat/deaktivovat, smazat, upravit nick.
   - Aktivity a body: bodové hodnoty, očekávané počty za týden, aktivní/neaktivní.
   - Chaotic rozpis: dny × sloty × časy UTC.
   - Antharas/Valakas: kotevní datum.
   - Šperky: přiřadit / odebrat hráči.
   - Účast: úprava účasti libovolného hráče v libovolném týdnu (stejná mřížka bez omezení okna).
   - Nastavení: začátek evidence.

## 5. Technické řešení (moje volby)

- **.NET 10**, **Blazor Web App** s render módem **Interactive Server**. Login a logout jsou statické SSR stránky kvůli cookie.
- **MudBlazor** jako UI knihovna (moderní Material vzhled, vestavěná podpora dark/light). Verzi kompatibilní s .NET 10 zvolí builder.
- **ASP.NET Core Identity** s vlastním uživatelem (`UserName` = login, `Nick`, `IsActive`, `DeactivatedAt`). Cookie autentizace, role Admin/Player, bez e-mailu.
- **EF Core 10 + SQL Server**. Lokálně SQL Server v docker-compose, v Azure pak **Azure SQL** (free/serverless tier).
- Migrace se aplikují automaticky při startu. Seed: aktivity, rozpis, sloty, typy šperků, první admin.
- **Data Protection klíče** se ukládají do DB, aby přihlášení přežilo restart/redeploy kontejneru.
- Časová zóna Europe/Prague přes `TimeZoneInfo` (v Linux kontejneru IANA ID). Čas se získává přes `TimeProvider`, aby šel testovat.

### 5.1 Struktura řešení

```
L2Points.sln
src/
  L2Points.Domain/          entity, enumy, čistá logika (rozpis týdne, výpočet bodů a %, okno úprav) – bez závislostí na EF/UI
  L2Points.Infrastructure/  EF Core DbContext, konfigurace, migrace, seed, Identity store
  L2Points.Web/             Blazor Web App – stránky, komponenty, auth, aplikační služby, DI, Program.cs
tests/
  L2Points.Domain.Tests/    xUnit – rozpis, body, procenta, okno úprav
  L2Points.Web.Tests/       (volitelně) integrační testy služeb nad SQLite/Testcontainers
docker/ nebo kořen:
  Dockerfile                multi-stage build → aspnet runtime image
  docker-compose.yml        web + sqlserver pro lokální běh
docs/superpowers/specs|plans/
CLAUDE.md
```

### 5.2 Datový model (orientační)

- **AppUser** (Identity): Id, UserName (login), Nick (unikátní), IsActive, DeactivatedAt (DateOnly?), CreatedAt
- **Activity**: Id, Code, Name, Category {NormalEpic, ChaoticEpic, CastleSiege}, ScheduleType {Flexible, Weekly, Biweekly, Slotted}, Points, ExpectedPerWeek?, WeeklyDay?, BiweeklyAnchorDate?, SortOrder, IsActive, JewelTypeId? (vazba boss → šperk, informativní)
- **ChaoticSlot**: Id, Code (Evening / Morning), Name (Večerní / Ranní), SortOrder
- **ActivitySlotPoints** (volitelné přepsání bodů pro slot): ActivityId, SlotId, Points
- **ActivitySpawn** (rozpis slotovaných aktivit): Id, ActivityId, DayOfWeek, SlotId, TimeUtc. Castle Siege může být Weekly s časem.
- **Attendance**: Id, UserId, ActivityId, Date (DateOnly, CZ), SlotId?, CreatedAt, CreatedByUserId. Unikátní (UserId, ActivityId, Date, SlotId).
- **JewelType**: Id, Code, Name, ImagePath, SortOrder
- **UserJewel**: Id, UserId, JewelTypeId
- **AppSetting**: Key, Value (např. TrackingStartDate)

Mazání hráče kaskádově maže Attendance a UserJewel.

## 6. Nasazení

- `Dockerfile` (multi-stage, `mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0`), port 8080.
- Konfigurace přes env proměnné: `ConnectionStrings__Default`, `Admin__UserName`, `Admin__Password`, `Admin__Nick`.
- `docker-compose.yml` pro lokální vývoj (web + `mcr.microsoft.com/mssql/server`).
- Azure: Container Apps nebo App Service for Containers + Azure SQL. Samotné nasazení do Azure (IaC, CI/CD) je **pozdější krok**, ne součást první verze. Aplikace na něj ale musí být připravená (env konfigurace, health endpoint `/health`, DP klíče v DB).

## 7. Nechává se na builderovi

- Přesný vizuál, barvy a rozložení v rámci MudBlazor. Má být moderní, přehledný, s responzivitou pro mobil (zápis účasti z telefonu).
- Konkrétní verze balíčků.
- Dohledání a stažení ikon šperků (např. z L2 databází/wiki), uložení do `wwwroot/images/jewels`. Staženo z l2j.ru (32×32 PNG, název souboru = `Code` typu šperku).
- Rozdělení služeb a komponent uvnitř projektu Web.
- Indexy, výkonové optimalizace (malý počet uživatelů, desítky hráčů).

## 8. Poznámky

- Lokálně zatím není nainstalovaný Docker. .NET 10 SDK je k dispozici.
- Projekt zatím není git repozitář. Při zakládání kostry se inicializuje.
