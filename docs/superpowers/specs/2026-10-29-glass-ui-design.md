# Glass UI – návrh

Datum: 29. 10. 2026. Větev `feature/glass-ui`, založená z `feature/favicon` (používá novou faviconu jako logo; merge glass UI s sebou vezme i faviconu).

Vizuální reference: plátno „L2Points Glass UI“ (https://claude.ai/artifact/7VEDSTUYWSqnANoNXh718J) – rozbor palety a stránka Přehled v tmavé i světlé variantě. Data na plátně jsou ilustrační.

## Záměr

Celá aplikace dostane jednotný vzhled ve stylu „minimal glassmorphism“: tmavá noc s jemnou fialovou září, karty jako skoro průhledné sklo s tenkou světlou hranou, barva jen v malých dávkách. Uživatel zadal jediný požadavek: **přehlednost, hlavně při vyplňování Můj týden**. Grafická rozhodnutí přenechal builderovi (všechny konkrétní hodnoty níže jsou rozhodnutí návrhu, ne uživatele).

## Cíle

- Jednotný glass vzhled na všech stránkách (interaktivních i účtových `/login`, `/profile`), dark i light.
- Můj týden (a mřížka v `/admin/attendance` a na detailu hráče, sdílí `WeekGridView`) je na první pohled čitelnější: hned je vidět dnešní den, kategorie aktivity, slot Večerní/Ranní a co je zamčené.
- Velká čísla (body, KPI, procenta v souhrnu) v tenkém řezu, jako hlavní vizuální prvek.
- Funguje na mobilu (dotykové cíle ≥ 40 px v mřížce), v obou režimech má text kontrast aspoň 4,5:1.

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
| Pozadí | `#0B0A1A` | `#EEF0FA` |
| Záře 1 (pravý dolní roh) | `rgba(124,92,250,.36)` | `rgba(124,92,250,.22)` |
| Záře 2 (levý horní roh) | `rgba(199,125,255,.14)` | `rgba(96,165,250,.20)` |
| Sklo – výplň | `rgba(255,255,255,.045)` | `rgba(255,255,255,.58)` |
| Sklo – silnější (řádky, vnořené plochy) | `rgba(255,255,255,.06)` | `rgba(255,255,255,.70)` |
| Sklo – hrana | `rgba(255,255,255,.09)` | `rgba(255,255,255,.85)` |
| Sklo – horní světlo (inset) | `rgba(255,255,255,.12)` | `rgba(255,255,255,.90)` |
| Stín karty | `0 8px 32px rgba(0,0,0,.35)` | `0 10px 30px rgba(60,40,140,.10)` |
| Primary | `#8B6CFF` | `#6D4AE0` |
| Primary jako text (`--l2p-primary-text`) | `#B9A6FF` | `#6D4AE0` |
| Primary 2 (konec přechodu) | `#C77DFF` | `#A855F7` |
| Text na primary | `#0B0A1A` | `#FFFFFF` |
| Secondary | `#FF9A3C` (beze změny) | `#C2410C` (beze změny) |
| Text | `#F4F2FF` | `#1D1838` |
| Text 2 | `#B9B5D6` | `#4F4A6E` |
| Tlumený | `#A8A4CA` | `#625D84` |
| Dělicí čára | `rgba(255,255,255,.06)` | `rgba(60,40,140,.08)` |
| Neprůhledný povrch (popovery, dialogy, sticky buňky) | `#16132A` | `#F8F8FD` |

Barvy kategorií (tečka, pruh, obrys; nikdy velká plocha):

| Kategorie | Dark | Light |
|---|---|---|
| Normal epic | `#6E9BFF` | `#2F6FE0` |
| Chaotic · Večerní | `#A78BFA` | `#7C5CFA` |
| Chaotic · Ranní | `#F5B14C` | `#C27A0E` |
| Castle Siege | `#3FD0B0` | `#0F9A7D` |

Pásma procent (`.l2p-tone-*`) a místa 1–3 (`.l2p-place-*`) zůstávají významově stejná, odstíny se doladí tak, aby měly kontrast 4,5:1 na skle v obou režimech.

Text na plné primary (rozhodnutí návrhu): v tmavém režimu tmavý `#0B0A1A`, protože bílá na `#8B6CFF` má jen ~3,7:1; tmavá na ní má ~5,3:1 a stejně to už dnes dělají účtové stránky (`--account-accent-text`). Primary `#8B6CFF` jako text má na tmavém pozadí ~4,9:1, ale nad září v pravém dolním rohu jen ~3:1, proto text používá světlejší `--l2p-primary-text` (`#B9A6FF`, >= 5,3:1); zář `--l2p-glow-1` má v tmavém režimu .27 a tlumený text `#A8A4CA` (>= 5,3:1 na kartě nad zářím). Ve světlém režimu bílá na `#6D4AE0` (~5,6:1). Kde se níže píše „bílý text/fajfka na primary“, platí tato barva.

Pravidlo dávkování: plná primary jen na jednom prvku v kartě (aktivní položka navigace, zvolený přepínač, zaškrtnutá buňka, hlavní tlačítko). Přechod primary → primary 2 jen na pruhu bodů v žebříčku.

## Sklo

- Globální třída `.l2p-glass`: výplň, 1px hrana, `inset 0 1px 0` horní světlo, stín, `border-radius: 20px`, `backdrop-filter: blur(20px) saturate(140%)` (s `-webkit-` prefixem).
- Blur jen na velkých kontejnerech: app bar, drawer, karty sekcí. Nikdy na řádcích, buňkách, chipech (výkon na mobilu). Vnořené plochy uvnitř karty používají jen „silnější sklo“ bez blur.
- Popovery, menu, select, autocomplete, dialogy, tooltipy, snackbary a přilepené buňky tabulek mají **neprůhledný povrch** z tabulky výše. MudBlazor `Surface` v paletě zůstává neprůhledný (tím se tohle zajistí automaticky); sklo se dává jen třídou na vybrané kontejnery.
- Fallback: `@supports not (backdrop-filter: blur(1px))` a `@media (prefers-reduced-transparency: reduce)` → karty dostanou neprůhledný povrch.
- Pozadí stránky: dvě radiální záře nad barvou pozadí, `background-attachment: fixed` na `body` (na iOS, kde `fixed` nefunguje, stačí, že záře scrolluje s obsahem).

## Typografie

- Písmo Outfit (OFL), self-hosted jako variable `woff2` v `wwwroot/fonts/` s licencí `OFL.txt`, podmnožiny latin a latin-ext (česká diakritika). Žádné externí CDN, jako dosud.
- Fallback stack zůstává (`Segoe UI`, `Roboto`, …), aby se nic nerozbilo před načtením fontu (`font-display: swap`).
- Váhy: text 400, nadpisy a popisky 500, velká čísla 200–300. Velká čísla: KPI hodnoty, body v souhrnu týdne, body ve stupních vítězů. `font-variant-numeric: tabular-nums` u čísel v tabulkách a mřížce.
- Účtové stránky (statické SSR) používají stejné písmo.

## Shell (MainLayout)

- App bar: sklo s blur, bez elevace, výška beze změny. Vlevo logo (favicona `favicon.svg` jako `<img>` 32 px) + „L2Points“, vpravo nick, přepínač tématu, odhlášení jako dnes.
- Drawer: sklo, položky s ikonou a textem jako dnes; aktivní položka má plnou primary výplň a bílý text, zaoblení 12 px, ostatní text 2 a při hoveru silnější sklo.
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
- `.l2p-section-head`: nadpis sekce bez kapitálek, váha 500, velikost ~1.25rem, barva textu (ne primary) – klidnější, jako v referenci.
- Chipy a štítky (pásma procent, „neaktivní“, „admin“): obrys + barevný text místo plné výplně.
- Přepínače (`MudToggleGroup` Karty/Mřížka na `/week`, `WeekNavigator`): segmentovaný přepínač na silnějším skle, zvolená položka plná primary. Přehled (`/leaderboard`) si nechává dvě sekce pod sebou (Celkově, Týden); přepínač Týden/Celkově z mockupu se nedělá (změna struktury stránky).

### Můj týden – mřížka (`WeekGridView`), priorita

- Celá mřížka je jedna glass karta; řádky bez blur.
- Skupiny (Normal epicy, Chaotic epicy, Castle Siege): řádek skupiny má barevnou tečku kategorie a název ve větší váze 500, bez kapitálek a bez plošného podbarvení. U chaotic se slot v buňce značí malou tečkou barvy slotu před textem „V 21:00“ / „R 05:30“.
- Dnešní den: celý sloupec jemně podbarvený primary (~10 %), v hlavičce pilulka „Dnes“ s plnou primary pod názvem dne. Hlavička dnů zůstává opakovaná u spodních skupin (mobil).
- Zamčené dny: sloupec tlumený (text tlumený), ikona zámku v hlavičce zůstává.
- Stavy buňky (rozhodnutí návrhu):
  - zaškrtnuté a měnitelné: plná primary s fajfkou;
  - nezaškrtnuté a měnitelné: obrys s kontrastem ≥ 3:1 vůči pozadí;
  - zaškrtnuté, ale nejde měnit (zamčený den, limit): primary výplň zředěná na ~55 % (`color-mix` s povrchem), fajfka zůstává čitelná;
  - nezaškrtnuté a nejde přidat (zamčený den, mimo rozpis, limit): slabý čárkovaný obrys.
- Jen čtecí mřížka (`ReadOnly`, tj. `/players/{Id}`) a mřížka během načítání v `EditableWeekGrid` (`ReadOnly="_loading"`) se **nevybledí**: zaškrtnuté zůstanou plnou primary a nezaškrtnuté obrysem, jen bez kurzoru a hoveru. Zředění a čárkování patří jen buňkám, které jsou zamčené v editovatelné mřížce. Komponenta k tomu potřebuje rozlišit „celá mřížka jen ke čtení“ od „tahle buňka nejde měnit“ (dnes obojí končí jako `Disabled`).
- Hover řádku (desktop) zvýrazní celý řádek silnějším sklem, aby se dalo sledovat přes 7 dní. Přilepený první sloupec má neprůhledný povrch.
- Dotykový cíl zaškrtávátka aspoň 40×40 px na mobilu.
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
