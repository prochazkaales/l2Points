# Glass UI – návrh

Datum: 29. 10. 2026. Větev `feature/glass-ui`, založená z `feature/favicon` (používá novou faviconu jako logo; merge glass UI s sebou vezme i faviconu).

Vizuální reference: plátno „L2Points Glass UI“ (https://claude.ai/artifact/7VEDSTUYWSqnANoNXh718J) – rozbor palety a stránka Přehled v tmavé i světlé variantě. Data na plátně jsou ilustrační.

## Záměr

Celá aplikace dostane jednotný vzhled ve stylu „minimal glassmorphism“: tmavá noc s jemnou fialovou září, karty jako skoro průhledné sklo s tenkou světlou hranou, barva jen v malých dávkách. Uživatel zadal jediný požadavek: **přehlednost, hlavně při vyplňování Můj týden**. Grafická rozhodnutí přenechal builderovi (všechny konkrétní hodnoty níže jsou rozhodnutí návrhu, ne uživatele).

## Cíle

- Jednotný glass vzhled na všech stránkách (interaktivních i účtových `/login`, `/profile`), dark i light.
- Můj týden (a mřížka v `/admin/attendance` a na detailu hráče, sdílí `WeekGridView`) je na první pohled čitelnější: hned je vidět dnešní den, kategorie aktivity, slot Večerní/Ranní a co je zamčené.
- Velká čísla (body, KPI, procenta v souhrnu) v tenkém řezu, jako hlavní vizuální prvek.
- Funguje na mobilu (dotykové cíle ≥ 44 px, v mřížce s mezerou 4 px, jinde 8 px), v obou režimech má text kontrast aspoň 4,5:1.

## Necíle

- Žádné nové funkce ani data. Prvky z mockupu, které v aplikaci nejsou (hodiny, „Dnešní události“, prstenec „Moje účast“, stupně vítězů s výškou sloupců, pozdrav s nickem), se **nedělají**. Mockup ukazuje styl, ne obsah.
- Žádná změna logiky: pravidla úprav, `EditableWeekGrid`, služby, doména, routy, autorizace zůstávají.
- Navigace se nemění na ikonový rail: zůstane drawer s textovými popisky (přehlednost), jen v novém vzhledu.

## Anti-cíle (selhání, i kdyby to technicky fungovalo)

- Mřížka Můj týden je hůř čitelná než teď (nízký kontrast skla, rozmazané pozadí za textem, zaškrtávátka splývají).
- Popovery, menu, dialogy, tooltipy nebo přilepené sloupce tabulek jsou průhledné a prosvítá přes ně obsah.
- Stránka na slabším mobilu laguje při scrollu kvůli blur na desítkách prvků.

## Paleta a tokeny

Barvy jsou CSS proměnné v `wwwroot/app.css` (jedna sada pro dark, jedna pro light) a odpovídající hodnoty v `AppTheme.cs`.

| Token | Dark | Light |
|---|---|---|
| Pozadí | `#0B0A1A` | `#E8EAF6` |
| Záře 1 (pravý dolní roh) | `rgba(124,92,250,.27)` | `rgba(124,92,250,.22)` |
| Záře 2 (levý horní roh) | `rgba(199,125,255,.14)` | `rgba(96,165,250,.20)` |
| Sklo – výplň | `rgba(255,255,255,.045)` | `rgba(255,255,255,.58)` |
| Sklo – silnější (řádky, vnořené plochy) | `rgba(255,255,255,.06)` | `rgba(109,74,224,.065)` (nádech primary, ne bělejší než karta, jinak dlaždice splývají) |
| Sklo – hrana (i okraje čipů, dnů a dělicí čára přilepeného sloupce) | `rgba(185,166,255,.16)` | `rgba(90,70,170,.22)` (bílá hrana na světlé stránce není vidět; v tmavém stejný fialový odstín, aby čáry v obou režimech působily stejně) |
| Sklo – horní světlo (inset) | `rgba(255,255,255,.04)` | `rgba(255,255,255,.90)` |
| Stín karty | `0 4px 20px rgba(0,0,0,.22)` | `0 10px 30px rgba(60,40,140,.14)` |
| Primary | `#8B6CFF` | `#6D4AE0` |
| Primary jako text (`--l2p-primary-text`) | `#B9A6FF` | `#5B39CC` |
| Primary 2 (konec přechodu) | `#C77DFF` | `#A855F7` |
| Text na primary | `#0B0A1A` | `#FFFFFF` |
| Secondary (`--l2p-secondary`) | `#FF9A3C` | `#A93A0B` |
| Text | `#F4F2FF` | `#1D1838` |
| Text 2 | `#B9B5D6` | `#4F4A6E` |
| Tlumený | `#AEAAD0` | `#58537A` |
| Dělicí čára | `rgba(185,166,255,.07)` | `rgba(60,40,140,.08)` |
| Neprůhledný povrch (popovery, dialogy, sticky buňky) | `#16132A` | `#F8F8FD` |
| Přilepený povrch (`--l2p-surface-pinned`) | primary 10 % nad neprůhledným povrchem | primary 5 % nad neprůhledným povrchem |
| Řádek „já“ (`--l2p-row-me`, `--l2p-row-me-solid`) | primary 10 % (průhledný, resp. nad neprůhledným povrchem) | stejně |
| Neaktivní text (`--l2p-text-disabled`) | `#8C88AE` (>= 3:1, ovládací prvky) | `#77738F` |
| Info (`--l2p-info`) | `#7CC4FF` | `#0B5CAD` |
| Success (`--l2p-success`) | `#6FE3C8` | `#086E59` |
| Warning (`--l2p-warning`) | `#F5C15C` | `#7A5200` |
| Danger (`--l2p-danger`, MudTheme Error) | `#FF8A8A` | `#A3241A` |
| Dlaždice ikon šperků (`--l2p-icon-tile`) | `#1C1929` | `#2A2545` |
| Závoj dialogů (`--l2p-scrim`) | pozadí 60 % | `#1D1838` 45 % |
| Čáry (`--l2p-line`, `--l2p-line-subtle`, `--l2p-line-strong`) | hrana skla, dělicí čára, primary 35 % | stejně |
| Obrys fokusu (`--l2p-focus-ring`) | `--l2p-primary-text` | `--l2p-primary-text` |
| Stíny (`--l2p-shadow-1` karta, `-2` popover, `-3` dialog) | `0 4px 20px .22`, `0 8px 24px .35`, `0 16px 48px .45` | `0 10px 30px .14`, `0 8px 24px .18`, `0 20px 56px .24` (fialový odstín `60,40,140`) |

Barvy kategorií (tečka, pruh, obrys; nikdy velká plocha):

| Kategorie | Dark | Light |
|---|---|---|
| Normal epic | `#6E9BFF` | `#2F6FE0` |
| Chaotic · Večerní (oranžová jako dřív) | `#FF9F4A` | `#E2690F` |
| Chaotic · Ranní (azurově modrá jako dřív) | `#3DBFF2` | `#1E8AD6` |
| Castle Siege | `#3FD0B0` | `#0F9A7D` |

Text štítků kategorií (`--l2p-cat-normal-text|evening-text|morning-text|cs-text`) je posunutý k barvě textu, aby na vlastní 16% výplni na vnořeném skle dosáhl 4,5:1: dark `#A5BFFF`, `#FCB375`, `#72CEF6`, `#61D6BF`; light `#2953AA`, `#8B4521`, `#1E5B95`, `#156360`.

Pásma procent (`.l2p-tone-*`) a místa 1–3 (`.l2p-place-*`) zůstávají významově stejná, odstíny se doladí tak, aby měly kontrast 4,5:1 na skle v obou režimech.

Hodnoty tokenů hlídají `TokenContrastTests` (kontrast textu, štítků a neaktivního textu nad sklem, září a řádkem „já“) a `AppThemeTokensTests` (palety `AppTheme.cs` zrcadlí tokeny; `DefaultBorderRadius` je 8 px a tlačítka bez velkých písmen).

Text na plné primary (rozhodnutí návrhu): v tmavém režimu tmavý `#0B0A1A`, protože bílá na `#8B6CFF` má jen ~3,7:1; tmavá na ní má ~5,3:1 a stejně to už dnes dělají účtové stránky (`--account-accent-text`). Primary `#8B6CFF` jako text má na tmavém pozadí ~4,9:1, ale nad září v pravém dolním rohu jen ~3:1, proto text používá světlejší `--l2p-primary-text` (`#B9A6FF`, nad září na kartě >= 5:1); zář `--l2p-glow-1` má v tmavém režimu .27 a tlumený text `--l2p-muted` (`#AEAAD0` v tmavém, `#58537A` ve světlém režimu; >= 4,5:1 i na vnořeném silnějším skle v maximu záře). Ve světlém režimu bílá na `#6D4AE0` (~5,6:1). Kde se níže píše „bílý text/fajfka na primary“, platí tato barva.

Pravidlo dávkování: plná primary jen na jednom prvku v kartě (aktivní položka navigace, zvolený přepínač, zaškrtnutá buňka, hlavní tlačítko). Přechod primary → primary 2 jen na pruhu bodů v žebříčku.

## Sklo

- Globální třída `.l2p-glass`: výplň, 1px hrana, `inset 0 1px 0` horní světlo, stín, `border-radius: 12px`, `backdrop-filter: blur(20px) saturate(140%)` (s `-webkit-` prefixem). Karty (outlined `MudPaper`/`MudTable`) mají `overflow: clip`, aby neprůhledné přilepené buňky v rohu nevyčnívaly přes zaoblení. Zaoblení je střídmé: karta 12 px, vnořená plocha 10 px, položka draweru, čip a výběr dne 8 px, štítek procent 6 px. Hlavička každé tabulky (MudTable i vlastní tabulky) je stejný neprůhledný pruh `--l2p-table-head` s popisky v barvě textu a váze 600; nadpisy sekcí nad kartami mají před sebou ikonu v `--l2p-primary-text`.
- Blur jen na velkých kontejnerech: app bar, drawer, karty sekcí. Nikdy na řádcích, buňkách, chipech (výkon na mobilu). Vnořené plochy uvnitř karty používají jen „silnější sklo“ bez blur. Stejně tak dlaždice v mřížce (`.l2p-tile`: KPI, karty stupňů vítězů, karty šperků) jsou sklo bez blur: `backdrop-filter` mají jen app bar, drawer a karty sekcí, dlaždice nikdy (počet vrstev tedy závisí na počtu sekcí stránky).
- Škály (tokeny `--l2p-*` v `app.css`, společné pro všechny stránky): mezery `--l2p-space-1..7` (4/8/12/16/24/32/48 px) a rytmus podle role (`--l2p-gap-title` 8 px pod nadpisem sekce, `--l2p-gap-tiles` 12 px mezi dlaždicemi, `--l2p-gap-section` 32 px mezi sekcemi, `--l2p-pad-card` 16 px, pod 600 px 12 px, `--l2p-page-head` 24 px pod nadpisem stránky); buňky tabulek a mřížky mají vlastní tokeny (`--l2p-cell-*`, `--l2p-grid-cell-*`). Radiusy `--l2p-radius-sm|md|lg|xl|full` (6/8/10/12 px/pilulka): štítky, checkbox a legenda sm; tlačítka, vstupy, čipy, položky draweru a výběr dne md; vnořené plochy a dlaždice lg; karty a dialogy xl; metry a pruhy full. Stíny `--l2p-shadow-1` (karta, `--l2p-glass-shadow` je jeho alias), `-2` (popover), `-3` (dialog). Písmo `--l2p-font-xs..3xl` (12 až 32 px, nejmenší text 12 px), čáry `--l2p-line-width` a `--l2p-line-width-strong`.
- Dialog (`.mud-dialog`) je neprůhledný se zaoblením xl, hranou `--l2p-line` a stínem 3, popover má radius md, hranu a stín 2; scrim dialogu a draweru je `--l2p-scrim`.
- Ikony (I2): navigace a akce jsou Filled, ikony nadpisů sekcí Outlined.
- Popovery, menu, select, autocomplete, dialogy, tooltipy, snackbary a přilepené buňky tabulek mají **neprůhledný povrch** z tabulky výše. MudBlazor `Surface` v paletě zůstává neprůhledný (tím se tohle zajistí automaticky); sklo se dává jen třídou na vybrané kontejnery.
- Fallback: `@supports not (backdrop-filter: blur(1px))` a `@media (prefers-reduced-transparency: reduce)` → karty dostanou neprůhledný povrch.
- Pozadí stránky: dvě radiální záře nad barvou pozadí, `background-attachment: fixed` na `body` (na iOS, kde `fixed` nefunguje, stačí, že záře scrolluje s obsahem).

## Typografie

- Písmo Outfit (OFL), self-hosted jako variable `woff2` v `wwwroot/fonts/` s licencí `OFL.txt`, podmnožiny latin a latin-ext (česká diakritika). Žádné externí CDN, jako dosud.
- Fallback stack zůstává (`Segoe UI`, `Roboto`, …), aby se nic nerozbilo před načtením fontu (`font-display: swap`).
- Váhy: text 400, nadpisy a popisky 500, KPI hodnoty 600 (32 px, na telefonu 24 px). Velká čísla: KPI hodnoty, body v souhrnu týdne, body ve stupních vítězů. `font-variant-numeric: tabular-nums` u čísel v tabulkách a mřížce.
- Účtové stránky (statické SSR) používají stejné písmo.

## Shell (MainLayout)

- App bar: sklo s blur, bez elevace, výška beze změny. Vlevo logo (favicona `favicon.svg` jako `<img>` 32 px) + „L2Points“, vpravo nick, přepínač tématu, odhlášení jako dnes.
- Drawer: sklo, položky s ikonou a textem jako dnes; aktivní položka má plnou primary výplň a bílý text, zaoblení 8 px, ostatní text 2 a při hoveru silnější sklo.
- Obsah: `MudContainer` beze změny šířky; pozadí stránky je záře, ne `Background` barva palety (ta zůstává pro případ, kdy CSS záře nenačte).
- Načítací placeholder `.l2p-loading` a pozadí před připojením circuitu odpovídají novému pozadí v obou režimech.
- Přebliknutí tématu se odstraní (rozhodnutí návrhu; dnes `MainLayout` začíná tmavě a uloženou volbu z `l2pTheme` načte až po prvním renderu, placeholder se řídí jen OS):
  - malý inline skript v `<head>` (`App.razor`) přečte `localStorage` (stejný klíč jako `l2pTheme`) a nastaví `data-theme="dark|light"` na `<html>` ještě před vykreslením; bez uložené volby podle `prefers-color-scheme`;
  - pozadí, záře, placeholder i účtové stránky se řídí `data-theme` (CSS proměnné pod `:root[data-theme=…]`);
  - `MainLayout` vezme počáteční `_isDarkMode` z téhož zdroje, přepínač aktualizuje `localStorage` i atribut;
  - logo v app baru se řídí zvoleným tématem (dvě varianty nebo inline SVG s `currentColor`/proměnnými), ne `prefers-color-scheme` uvnitř `favicon.svg`. Favicona v záložce zůstává podle OS (to jinak nejde).

## Stránky a komponenty

Všechny sekce, které dnes stojí na `MudPaper`/`MudTable`, dostanou sklo (třída nebo `Elevation="0"` + `.l2p-glass`). Konkrétně:

- `KpiCard`, `WeekSummaryCard`, `RankingTable`, karty na `/week`, `/jewels`, `/points`, `/players/{Id}`, admin stránky a dialogy (dialogy neprůhledné, viz výše).
- `.l2p-section-head`: nadpis sekce **nad** kartou (ne první řádek uvnitř karty), bez kapitálek, váha 600, velikost ~1.05rem, barva textu (ne primary), stejně jako nadpisy tabulek v Můj týden; ve stejném řádku mohou být ovládací prvky sekce (navigátor týdne, přepínač zobrazení). Platí pro Hráči a Události týdne na `/week` a Celkově a Týden na `/leaderboard`.
- Chipy a štítky „neaktivní“, „admin“: obrys + barevný text místo plné výplně. Štítky pásem procent (`.l2p-tone-*`) a druhu události (Normal/Večerní/Ranní/CS na `/week`) mají barevné podbarvení ~16 % a barevný text bez obrysu (podle procent má být barva vidět na první pohled i v dlouhém sloupci); odstíny pásem jsou tomu přizpůsobené, aby měly na podbarvení ≥ 4,5:1 (dark `#6FE3C8`/`#F5C15C`/`#FFA3A3`, light `#06604E`/`#714B00`/`#9F2A1D`).
- Přepínač Karty/Mřížka na `/week`: `MudSwitch` s popisky po stranách („Karty“ [přepínač] „Mřížka“), popisky jsou klikací a zvolený je v barvě `--l2p-primary-text` a tučněji. `WeekNavigator` je bez rámečku a pozadí (šipky, rozsah týdne a textové tlačítko „Tento týden“ přímo na stránce). Přehled (`/leaderboard`) si nechává dvě sekce pod sebou (Celkově, Týden); přepínač Týden/Celkově z mockupu se nedělá (změna struktury stránky).

### Můj týden – mřížka (`WeekGridView`), priorita

- Dvě tabulky pod sebou, každá vlastní glass karta s nadpisem nad ní (barevné tečky kategorií, váha 600): nahoře „Chaotic epicy“, pod ní „Normal epicy a Castle Siege“ (řádek Castle Siege je přes celou šířku jemně podbarvený barvou CS, ~12 %, bez tečky). Řádky bez blur. U chaotic se slot v buňce značí malou tečkou barvy slotu před textem „V 21:00“ / „R 05:30“.
- Hlavička tabulky je výrazný pruh: podbarvení primary, popisky v barvě textu a váze 600, pod ní čára primary; pruh je neprůhledný v celé šířce (token `--l2p-table-head`: primary smíchaná s `--l2p-surface-solid`, v tmavém režimu ~14 %, ve světlém ~7 % jako vnořené plochy, silnější odstín tam působí šedofialově), aby přes buňky dnů neprosvítalo sklo a hlavička nevypadala jako rámeček vedle „Boss“. Sloupec Boss odděluje čára z barvy textu (~14 %), viditelná v obou režimech; začíná až pod hlavičkou. Každá tabulka má vlastní hlavičku dnů; sloupce jsou v obou tabulkách stejně široké (pevné rozvržení, sloupec Boss 10rem, na mobilu 6,75rem, dny rovným dílem, nejméně 6,5rem), takže dny leží pod sebou.
- Kompaktní řádky: buňka 2 px svisle, zaškrtávací pole s plochou 28 px (na mobilu 36 px), řádek bez událostí má stejnou výšku jako řádek s jedním polem (33 px), aby čáry řádků byly rovnoměrné.
- Dnešní den: celý sloupec jemně podbarvený primary (~10 %), buňka dne v hlavičce plná primary s textem na primary (`aria-current="date"`, `title` „Dnes“).
- Zamčené dny: sloupec tlumený (text tlumený), ikona zámku v hlavičce zůstává.
- Stavy buňky (rozhodnutí návrhu):
  - zaškrtnuté a měnitelné: plná primary s fajfkou;
  - nezaškrtnuté a měnitelné: obrys s kontrastem ≥ 3:1 vůči pozadí;
  - zaškrtnuté, ale nejde měnit (zamčený den, limit): primary výplň zředěná na ~55 % (`color-mix` s povrchem), fajfka zůstává čitelná;
  - nezaškrtnuté a nejde přidat (zamčený den, mimo rozpis, limit): slabý čárkovaný obrys.
- Jen čtecí mřížka (`ReadOnly`, tj. `/players/{Id}`) a mřížka během načítání v `EditableWeekGrid` (`ReadOnly="_loading"`) se **nevybledí**: zaškrtnuté zůstanou plnou primary a nezaškrtnuté obrysem, jen bez kurzoru a hoveru. Zředění a čárkování patří jen buňkám, které jsou zamčené v editovatelné mřížce. Komponenta k tomu potřebuje rozlišit „celá mřížka jen ke čtení“ od „tahle buňka nejde měnit“ (dnes obojí končí jako `Disabled`).
- Hover řádku (desktop) zvýrazní celý řádek silnějším sklem, aby se dalo sledovat přes 7 dní. Přilepený první sloupec má neprůhledný povrch.
- Dotykový cíl zaškrtávátka aspoň 44×44 px na mobilu a na `(pointer: coarse)`, mezi poli slotu 4 px.
- Pod mřížkou krátká legenda: zaškrtnuto, nezaškrtnuto, zamčeno, mimo rozpis, V/R tečky.
- Tooltipy (limit, „Mimo rozpis“) zůstávají.

### Souhrn týdne (`WeekSummaryCard`)

Body velkým tenkým číslem, pět dlaždic procent (Celkem, Normal, Večerní, Ranní, CS) na silnějším skle; dlaždice Večerní/Ranní/Normal/CS mají tečku barvy kategorie. Dlaždice Celkem zvýrazněná obrysem primary místo silnějšího podbarvení.

### Účtové stránky

`.account-page` dostane stejné pozadí se září, `.account-card` sklo (v tomto případě neprůhledný fallback není nutný, jde o jednu kartu). Proměnné `--account-*` se sladí s paletou výše. Logo nad nadpisem.

## Omezení

- MudBlazor 9.x, žádná další UI knihovna. Barvy v C# jen v `AppTheme.cs`, zbytek v `app.css` a scoped CSS. Styly na `MudTd`/`MudTh`/`MudPaper` kořeny jen globálně (scoped CSS na ně nedosáhne), jak už je zvykem v `app.css`.
- Prerender je vypnutý, téma se volí po prvním renderu (`l2pTheme`); pozadí před připojením nesmí bliknout jinou barvou.
- Žádné TDD (čistě vizuální práce). Ověření: spuštěná aplikace, screenshoty všech stránek v dark i light a na šířce 375 px, kontrola kontrastu u textů na skle.
- Smoke test v CI kontroluje `/login` a ikony šperků; přidání fontu nesmí zvětšit image o víc než jednotky set kB.

## Nechává se na builderovi

- Rozdělení CSS mezi `app.css` a scoped soubory, názvy pomocných tříd kromě `.l2p-glass`.
- Přesné hodnoty v rozsahu ± (velikosti písma, odsazení, opacity), pokud drží kontrast a pravidlo dávkování.
- Jak přesně v MudBlazoru vypnout elevaci a navázat třídy (theme `Shadows`, `Elevation="0"`, CSS override).
- Pořadí práce a dělení na commity.

## Doladění tabulek a medailí (po prvním nasazení vzhledu)

- **Jednotný rytmus tabulek:** každá `MudTable` i vlastní mřížky (`.l2p-grid`, `.l2p-wk-grid`) mají buňky 0,35 rem × 0,75 rem, popisky hlavičky 0,8125 rem / 600, tělo 0,875 rem. První a poslední buňka řádku je odsazená 1 rem (na mobilu 0,75 rem), aby text nelepil na zaoblení karty. Číslo místa sousedí s nickem (bez mezery navíc).
- **Tabulky žebříčku** (`RankingTable`: Celkově, Týden, Hráči) mají pevné rozvržení se stejnými šířkami sloupců (místo 3 rem, nick 12 rem, body 6 rem, pruh bodů 22 %, zbytek procenta) a ve všech je pruh bodů, takže sloupce v tabulkách pod sebou leží přesně pod sebou. Pod minimální šířkou (42 rem, na mobilu 34 rem) se karta posouvá do strany.
- **Ukazatel načítání týdne** na Přehledu je tenká čára přes horní okraj karty (absolutně), nezabírá výšku. Rezervovaný pruh nad hlavičkou dělal z tabulky Týden jinou tabulku než Celkově.
- **Medaile** (okraj a pohár na stupních vítězů) mají vlastní tokeny `--l2p-medal-1..3`: v tmavém režimu totéž co `--l2p-place-*` (`#F5C15C`, `#C9CBE0`, `#E0915A`), ve světlém jasnější (`#E0A31B`, `#9EA3C4`, `#D4813F`). Tmavé odstíny `--l2p-place-*` platí jen pro text (pořadí v tabulkách, 4,5:1). Karta stupně má ~7 % barvy medaile jako podbarvení.
- **Tmavý režim:** hrana, horní světlo a stín jsou tišší (hodnoty v tabulce výše), aby karty nebyly obtažené; obrysová tlačítka mají hranu primary jen ze 45 %.
- **Kontrast textu** jsme ověřili skriptem (barva textu proti složenému pozadí všech textových prvků) na stránkách Můj týden, Týden – všichni, Přehled, Body, Šperky a Admin – Hráči v obou režimech; bez výsledků pod 4,5:1 (3:1 u velkého textu). Pásma procent se při tom zesvětlila/ztmavila (`--l2p-tone-low` ve světlém `#912317`), prázdná hodnota „–“ používá `--l2p-muted` a dny bez událostí v `/week` také (ne barvu „disabled“).
