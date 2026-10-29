# UI/UX audit – opravy, implementační plán

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Opravit nálezy UI/UX auditu (přístupnost, kontrast, dotykové cíle, jednotná hierarchie, škály rozměrů, stavy) bez změny funkcí aplikace.

**Architecture:** Barvy a rozměry jsou tokeny `--l2p-*` v `wwwroot/app.css`. Paleta MudBlazoru v `AppTheme.cs` je zrcadlí a hlídá to test. Kontrast důležitých párů tokenů hlídá unit test, který z `app.css` načte tokeny a složí pozadí stejně jako prohlížeč. Opakované kusy UI (hlavička stránky, nadpis sekce, prázdný stav, skeleton, akce hráče v adminu) jsou nové komponenty v `Components/Common` a `Components/Admin`. Logika s rozhodováním (`DisplayFormat`, `PlayerActionState`) je čistá a psaná přes TDD, zbytek je CSS a markup ověřený v běžící aplikaci.

**Tech Stack:** .NET 10, Blazor Web App (Interactive Server, static SSR pro účet), MudBlazor 9.11, xUnit.

**Spec:** `docs/superpowers/specs/2026-10-29-ui-ux-audit.md` (nálezy A1–I2, návrh tokenů, kontrasty), vedle něj `docs/superpowers/specs/2026-10-29-glass-ui-design.md` (pravidla skla, která platí dál).

## Global Constraints

- Větev `feature/ui-audit` založená z aktuálního `feature/my-week-tables` (13 commitů napřed před `main`). Commit messages anglicky, komentáře a identifikátory anglicky, UI česky.
- Hodnoty tokenů přesně podle sekce „Návrh sjednocených tokenů“ v auditu. Žádné pevné hexy v komponentách, jen `--l2p-*`. Výjimka: `AppTheme.cs` hexy zrcadlí.
- Kontrast: text ≥ 4,5:1 v obou režimech nad sklem, nad vrcholem záře vpravo dole i na řádku „já“. Neaktivní ovládací prvky ≥ 3:1. Ne-textové prvky ovládání ≥ 3:1.
- Dotykové cíle na `(pointer: coarse)` a pod 600 px šířky aspoň 44×44 px, mezi sousedními cíli aspoň 4 px (v mřížce), jinde 8 px.
- Blur (`backdrop-filter`) jen na app baru, draweru a kartách sekcí. Dlaždice v mřížce (KPI, stupně vítězů, šperky) blur nemají. Popovery, menu, dialogy a přilepené buňky zůstávají neprůhledné.
- Žádná změna doménové logiky, služeb, rout, autorizace ani pravidel úprav. Pravidla ochrany adminů (`PlayerAdminService`) se nemění, `PlayerActionState` je jen jejich zrcadlo pro UI.
- Aktuální čas jen přes `TimeProvider` a `AppTime.TodayInPrague`. Časy pro uživatele v Europe/Prague.
- Styly na kořeny MudBlazor komponent (`MudPaper`, `MudTd`, `MudTh`, `MudText`) jen globálně v `app.css` nebo přes `::deep`. Scoped CSS na ně přímo nedosáhne.
- TDD jen pro `TokenContrastTests`, `AppThemeTokensTests`, `DisplayFormat` a `PlayerActionState`. CSS a markup se ověřují v běžící aplikaci:
  - preview `l2points-web` (`.claude/launch.json`), dev admin z `appsettings.Development.json`;
  - pokud port 5167 a DLL v `bin` drží jiná instance aplikace (build pak selže na MSB3027), požádej uživatele o její zastavení;
  - velikosti 1440×900 a 375×812, oba režimy (`localStorage['l2p-theme']` a reload), transitions vypnuté vloženým `*{transition:none!important;animation:none!important}`.
- Měření kontrastu v prohlížeči (krok „Ověř kontrast“ v úkolech) dělej skriptem přes `javascript_tool`:
  1. Pozadí slož od `html`: `--l2p-bg`, nad ním `--l2p-glow-2` (elipsa 800×480 px se středem v 0 % 0 %, lineárně do průhledna na 60 %) a `--l2p-glow-1` (elipsa 1100×620 px se středem v 88 % 100 % viewportu, do průhledna na 62 %).
  2. Pak alfa skládej `background-color` a první barvu `linear-gradient` všech předků.
  3. Barva textu má alfu násobenou `opacity` předků. Computed barvy z `color-mix()` přicházejí jako `color(srgb r g b / a)` v rozsahu 0–1.
  4. Měř každý viditelný text mimo `.mud-sr-only` na jeho místě i v režimu „nejhorší případ“ (pozadí v bodě 0,88·šířka, výška−1). Hlas vše pod 4,5:1 (velký text 3:1).
  5. Po ověření odstraň vše, co skript uložil do `localStorage`, a vrať uložené téma uživatele.
- Dokumentace se aktualizuje v úkolu, který změnu přináší (CLAUDE.md sekce Struktura a Konvence, spec glass UI, roadmapa).

## Review Focus

1. **Dlouhý nick bez mezer (32 znaků, `Nick` má max 32)** v kartě hráče na mobilu v `/admin/players`, v přilepené buňce s ikonou šperku a ve stupních vítězů. Text se zalomí (`overflow-wrap: anywhere`) a nic nepřeteče z karty ani nezvětší stránku nad 375 px. Kontrola v Task 7 a Task 10.
2. **Focus u okraje karty.** Karty mají `overflow: clip`, takže vnější `outline` na první a poslední buňce řádku, na checkboxu v rohu nebo na položce menu se ořízne. Focus musí být vidět celý na každém interaktivním prvku včetně buněk tabulek. Kontrola v Task 3.
3. **Týden přes přechod času:**
   - Týden 19.–25. 10. 2026 (25. 10. končí letní čas): Baium v sobotu 24. 10. 20:00 UTC = 22:00 v Praze, Castle Siege v neděli 25. 10. 18:00 UTC = 19:00.
   - Popisek rozpisu na `/points` musí počítat s datem toho týdne, ne s dneškem.
   - Test v Task 8.
4. **Posun mřížky na dnešek:**
   - Jiný než aktuální týden nemá dnešní sloupec a mřížka zůstane na začátku (`scrollLeft = 0`).
   - Návrat na aktuální týden posune znovu.
   - Pouhé zaškrtnutí políčka (překreslení se stejným týdnem) s mřížkou nehne.
   - Na šířce ≥ 600 px se neposouvá nikdy.
   - Kontrola v Task 6.
5. **Přepnutí tématu za běhu tlačítkem.** Tokeny i paleta MudBlazoru (neaktivní text, Info alert, čipy Success/Secondary, dialog otevřený až po přepnutí) se přepnou spolu, bez míchání barev z druhého režimu. Kontrola v Task 2.

---

### Task 1: Barevné tokeny, zrcadlení palety a testy kontrastu

**Files:**
- Create: `tests/L2Points.Web.Tests/Theme/CssTokens.cs` (parser tokenů a barev, výpočet kontrastu)
- Create: `tests/L2Points.Web.Tests/Theme/TokenContrastTests.cs`
- Create: `tests/L2Points.Web.Tests/Theme/AppThemeTokensTests.cs`
- Modify: `src/L2Points.Web/wwwroot/app.css:23-110` (barevné tokeny obou režimů)
- Modify: `src/L2Points.Web/Components/Layout/AppTheme.cs:10-76`
- Modify: `docs/superpowers/specs/2026-10-29-glass-ui-design.md` (sekce Paleta a tokeny)

**Interfaces:**
- Produces (CSS):
  - `--l2p-row-me`, `--l2p-row-me-solid`, `--l2p-surface-pinned`, `--l2p-text-disabled`, `--l2p-focus-ring`;
  - `--l2p-info|success|warning|danger|secondary`, `--l2p-cat-normal-text|evening-text|morning-text|cs-text`;
  - `--l2p-icon-tile`, `--l2p-scrim`, `--l2p-line`, `--l2p-line-subtle`, `--l2p-line-strong`;
  - upravené hodnoty `--l2p-glass-edge`, `--l2p-muted`, `--l2p-tone-*`, `--l2p-place-*` (vše podle auditu).
- Produces (C#, jen v testovém projektu):
  - `CssTokens.Read(string theme) -> IReadOnlyDictionary<string, string>`: sloučí všechny bloky `app.css`, jejichž selektor obsahuje `data-theme="<theme>"`, a vrátí deklarace `--l2p-*`.
  - `Rgba CssTokens.Color(string value)` pro `#RRGGBB` a `rgba(r, g, b, a)`.
  - `Rgba Over(Rgba top, Rgba bottom)`, `Rgba Tint(Rgba color, double share)` (= `color-mix(in srgb, color share, transparent)`), `double Ratio(Rgba fg, Rgba bg)`.
  - `readonly record struct Rgba(double R, double G, double B, double A)`.

- [ ] **Step 1: Založ větev a commitni audit**

```bash
git switch -c feature/ui-audit
git add docs/superpowers/specs/2026-10-29-ui-ux-audit.md docs/superpowers/specs/2026-10-29-ui-ux-audit docs/superpowers/plans/2026-10-29-ui-audit-fixes.md
git commit -m "docs: UI/UX audit and fix plan"
```

- [ ] **Step 2: Napiš padající `TokenContrastTests`**

Pozadí se skládá stejně jako v auditu (tabulka Kontrasty):
- `glow = Over(glow-1, bg)`, `card = Over(glass, glow)`, `nested = Over(glass-strong, card)`;
- `me = Over(Tint(primary, 0.10), card)`;
- `RowMeShare = 0.10`, `BadgeFill = 0.16`.

Testy (`[Theory]` přes `"dark"` a `"light"`):
- `Text_tokens_reach_4_5_over_glass_and_glow`: tokeny `--l2p-text`, `--l2p-text-2`, `--l2p-muted`, `--l2p-primary-text`, `--l2p-place-1`, `--l2p-place-2`, `--l2p-place-3`, `--l2p-info`, `--l2p-success`, `--l2p-warning`, `--l2p-danger`, `--l2p-secondary` mají `Ratio >= 4.5` na `card`, `nested` i `me`.
- `Tone_badges_reach_4_5_on_their_fill`: pro `--l2p-tone-high|mid|low` platí `Ratio(tone, Over(Tint(tone, 0.16), x)) >= 4.5` pro `x` ∈ {`card`, `me`}.
- `Category_badges_reach_4_5_on_nested_glass`: pro čtyři `--l2p-cat-*-text` platí `Ratio(text, Over(Tint(cat, 0.16), nested)) >= 4.5`, kde `cat` je odpovídající `--l2p-cat-*`.
- `Disabled_text_reaches_3_on_glass`: `Ratio(--l2p-text-disabled, card) >= 3.0`.
- `Row_me_token_matches_the_tested_share`: hodnota `--l2p-row-me` je přesně `color-mix(in srgb, var(--l2p-primary) 10%, transparent)`.

Assert hláška vždy uvede téma, token a poměr na dvě desetinná místa.

- [ ] **Step 3: Spusť testy, musí padat**

Run: `dotnet test tests/L2Points.Web.Tests --filter FullyQualifiedName~TokenContrastTests`
Expected: FAIL, mimo jiné na chybějících tokenech `--l2p-info`, `--l2p-row-me`, `--l2p-text-disabled` a `--l2p-cat-*-text`.

- [ ] **Step 4: Uprav tokeny v `app.css`**

Dark a light blok přesně podle bloku `:root,:root[data-theme="dark"]` a `:root[data-theme="light"]` v auditu (jen barvy a stíny; škály rozměrů přidá Task 5). Do dark bloku navíc `--l2p-secondary: #FF9A3C`.

Textové barvy kategorií jsou v auditu přepočítané pro přímé použití (≥ 4,6:1):
- dark `#A5BFFF`, `#FCB375`, `#72CEF6`, `#61D6BF`;
- light `#2953AA`, `#8B4521`, `#1E5B95`, `#156360`.

Pokud test některou hodnotu odmítne, posuň barvu kategorie k `--l2p-text` (`color-mix` v sRGB) jen o tolik, aby prošla, a stejnou hodnotu zapiš do auditu.

- [ ] **Step 5: Spusť testy, musí projít**

Run: `dotnet test tests/L2Points.Web.Tests --filter FullyQualifiedName~TokenContrastTests`
Expected: PASS.

- [ ] **Step 6: Napiš padající `AppThemeTokensTests.Palette_mirrors_css_tokens`**

Pro oba režimy porovnej `(R, G, B)` z `MudColor` v `AppTheme.Theme.PaletteDark|PaletteLight` s tokenem z `CssTokens.Read`:

| Paleta | Token |
|---|---|
| `Primary` | `--l2p-primary` |
| `PrimaryContrastText` | `--l2p-on-primary` |
| `Secondary` | `--l2p-secondary` |
| `Background` | `--l2p-bg` |
| `Surface` | `--l2p-surface-solid` |
| `TextPrimary` | `--l2p-text` |
| `TextSecondary` | `--l2p-text-2` |
| `TextDisabled` | `--l2p-text-disabled` |
| `Info` | `--l2p-info` |
| `Success` | `--l2p-success` |
| `Warning` | `--l2p-warning` |
| `Error` | `--l2p-danger` |

Druhý test `Theme_layout_and_buttons`: `AppTheme.Theme.LayoutProperties.DefaultBorderRadius == "8px"` a `AppTheme.Theme.Typography.Button.TextTransform == "none"`.

- [ ] **Step 7: Spusť, musí padat**

Run: `dotnet test tests/L2Points.Web.Tests --filter FullyQualifiedName~AppThemeTokensTests`
Expected: FAIL (chybí `Info` v dark, `TextDisabled`, liší se `Success`/`Secondary`/`Error`/`Warning` v light, radius 4px, uppercase).

- [ ] **Step 8: Uprav `AppTheme.cs`**

Hodnoty podle tabulky. `LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px" }`. `Button = new ButtonTypography { FontFamily = FontFamily, TextTransform = "none", FontWeight = "600" }`. Komentář nad paletami: „Mirrors the --l2p-* tokens in wwwroot/app.css; AppThemeTokensTests keeps them in sync.“

- [ ] **Step 9: Spusť celý testovací projekt**

Run: `dotnet test tests/L2Points.Web.Tests`
Expected: PASS.

- [ ] **Step 10: Aktualizuj spec glass UI**

Tabulka „Paleta a tokeny“ dostane nové a změněné tokeny a větu, že hodnoty hlídají `TokenContrastTests` a `AppThemeTokensTests`.

- [ ] **Step 11: Commit**

```bash
git add tests/L2Points.Web.Tests/Theme src/L2Points.Web/wwwroot/app.css src/L2Points.Web/Components/Layout/AppTheme.cs docs/superpowers/specs/2026-10-29-glass-ui-design.md
git commit -m "feat(web): semantic colour tokens with contrast and palette mirror tests"
```

### Task 2: Použití barevných tokenů v komponentách

Nálezy A1, A2, A4, A5, A8 a F5.

**Files:**
- Modify: `src/L2Points.Web/Components/Attendance/WeekNavigator.razor:10-12` a `.razor.css`
- Modify: `src/L2Points.Web/Components/Stats/RankingTable.razor.css:47,74-91`
- Modify: `src/L2Points.Web/Components/Pages/WeekOverview.razor.css:72,76-99,125,152,199,221-248`
- Modify: `src/L2Points.Web/wwwroot/app.css:155-173` (validace, `#blazor-error-ui`)
- Modify: `src/L2Points.Web/Components/Layout/ReconnectModal.razor.css:30,44,93-115`
- Modify: `src/L2Points.Web/Components/Pages/Admin/AdminPlayers.razor:66,76` (barvy čipů zůstávají `Color.Success`/`Color.Secondary`, mění se jen palety z Task 1, kontrola)

**Interfaces:**
- Consumes: tokeny z Task 1.
- Produces: CSS třída `.l2p-invisible { visibility: hidden; }` v `app.css`.

- [ ] **Step 1: Skryj „Tento týden“ v aktuálním týdnu**

V `WeekNavigator.razor` nahraď `Disabled="@IsCurrentWeek"` třídou `l2p-invisible`, když `IsCurrentWeek`. Prvek tak zabere místo, je mimo pořadí Tab a mimo strom přístupnosti.

- [ ] **Step 2: Řádek „já“ přes tokeny**

V `RankingTable.razor.css:74-78` a `WeekOverview.razor.css:221-229`:
- tón řádku → `var(--l2p-row-me)`;
- `--l2p-row-bg` a přilepené buňky → `var(--l2p-row-me-solid)`;
- proužek `inset 3px 0 0` → `var(--l2p-primary)`.

- [ ] **Step 3: Štítky typu události a palety MudBlazoru**

- `.l2p-wk-kind` bere `color: var(--l2p-kind-text)` a každá varianta nastaví `--l2p-kind-text: var(--l2p-cat-*-text)` vedle `--l2p-kind`.
- Všechna `var(--mud-palette-*)` v `RankingTable.razor.css` a `WeekOverview.razor.css` nahraď odpovídajícím `--l2p-*`:
  - `text-primary` → `--l2p-text`, `text-secondary` → `--l2p-text-2`, `text-disabled` → `--l2p-muted`;
  - `primary` → `--l2p-primary`, `success` → `--l2p-success`.

- [ ] **Step 4: Chybové a reconnect UI**

- `.validation-message` → `var(--l2p-danger)`.
- `#blazor-error-ui` → pozadí `var(--l2p-surface-solid)`, text `var(--l2p-text)`, levý okraj 4 px `var(--l2p-danger)`, bez `color-scheme: light only`.
- `ReconnectModal.razor.css`: modré hexy → `var(--l2p-primary)` (výplň tlačítka, hover `color-mix(in srgb, var(--l2p-primary) 85%, black)`), text tlačítka `var(--l2p-on-primary)`, okraj spinneru `var(--l2p-primary)`, pozadí dialogu `var(--l2p-surface-solid)`, scrim `var(--l2p-scrim)`.

- [ ] **Step 5: Ověř v aplikaci**

Stránky `/`, `/leaderboard`, `/week`, `/players/{id}`, `/admin/players`, `/admin/schedule`, oba režimy, 1440×900:
- „Tento týden“ je v aktuálním týdnu neviditelné a šipky neposkočily;
- skript kontrastu (Global Constraints) nehlásí nic v režimu na místě ani v nejhorším případě;
- Review Focus 5: na `/admin/players` přepni téma tlačítkem v app baru a otevři dialog Upravit; čipy, alert na `/admin/schedule` i dialog mají barvy aktuálního režimu.

Screenshot `/leaderboard` dark ulož jako `docs/superpowers/specs/2026-10-29-ui-ux-audit/after-02-leaderboard-dark.jpg`.

- [ ] **Step 6: Commit**

```bash
git add src/L2Points.Web docs/superpowers/specs/2026-10-29-ui-ux-audit
git commit -m "fix(web): use semantic tokens for me row, badges, errors and reconnect UI"
```

### Task 3: Neutrální nula, focus a omezení pohybu

Nálezy A7, G1, G2 a G4.

**Files:**
- Modify: `src/L2Points.Web/Formatting/DisplayFormat.cs:24-30`
- Modify: `tests/L2Points.Web.Tests/Formatting/DisplayFormatTests.cs:29-37`
- Modify: `src/L2Points.Web/wwwroot/app.css` (`.l2p-tone-zero`, focus, reduced motion)
- Modify: `src/L2Points.Web/Components/Stats/RankingTable.razor:12,17`

**Interfaces:**
- Produces: `DisplayFormat.PercentTone(double? value) -> string?` nově vrací `"zero"` pro hodnotu, která se zaokrouhlí na 0; CSS `.l2p-tone-zero`.

- [ ] **Step 1: Uprav test `PercentTone_follows_the_shown_number`**

- Řádek `[InlineData(0.0, "low")]` změň na `"zero"`.
- Přidej `[InlineData(0.4, "zero")]` a `[InlineData(0.5, "low")]` (0,5 se zaokrouhlí nahoru na 1).

- [ ] **Step 2: Spusť, musí padat**

Run: `dotnet test tests/L2Points.Web.Tests --filter FullyQualifiedName~PercentTone`
Expected: FAIL, `"low"` místo `"zero"`.

- [ ] **Step 3: Implementuj**

Do switche v `PercentTone` přidej `0 => "zero"` před `_ => "low"` a dopiš do XML komentáře, že nula je neutrální.

- [ ] **Step 4: Spusť, musí projít**

Run: `dotnet test tests/L2Points.Web.Tests --filter FullyQualifiedName~PercentTone`
Expected: PASS.

- [ ] **Step 5: CSS pro nulu, focus a pohyb v `app.css`**

- `.l2p-tone-zero { color: var(--l2p-muted); background: transparent; }`.
- Focus: `:focus-visible` na `.mud-button-root`, `.mud-nav-link`, `a`, `.mud-chip.mud-clickable`, `.mud-list-item-clickable`, `.mud-menu-item`, `.l2p-wk-day` dostane `outline: 2px solid var(--l2p-focus-ring); outline-offset: 2px; border-radius: var(--l2p-radius-md, 8px)`. Uvnitř `.mud-table`, `.l2p-grid`, `.l2p-wk-grid` a menu použij `outline-offset: -2px`, aby karta s `overflow: clip` focus neořízla (Review Focus 2).
- `@media (prefers-reduced-motion: reduce)`:
  - `.mud-progress-indeterminate .mud-progress-linear-bar { animation: none; left: 0; right: auto; width: 30%; }` (statický pruh);
  - `.mud-ripple::after { display: none; }`.
  - Selektory ověř v DOM MudBlazoru 9.11. Pokud se jmenují jinak, použij skutečné třídy a zapiš je do komentáře.

- [ ] **Step 6: Názvy prázdných hlaviček**

`RankingTable.razor:12`: `<span class="mud-sr-only">Pořadí</span>`. `:17`: `aria-hidden="true"` na `MudTh` i na buňkách pruhu (`l2p-rank-bar-cell`).

- [ ] **Step 7: Ověř v aplikaci**

Na `/leaderboard` a `/admin/players`, oba režimy:
- Hráč s nulou má šedé „0 %“ bez výplně.
- Projdi Tabem app bar, drawer, šipky týdne, odkazy nicků v první buňce tabulky, zaškrtávátko v levém horním rohu mřížky na `/` a icon buttony v posledním sloupci `/admin/players`.
- Focus je u každého prvku vidět celý a nic ho neořízne (Review Focus 2).

Omezení pohybu: browser pane ho emulovat neumí. Ověř přes `javascript_tool`, že načtený stylesheet obsahuje pravidlo `@media (prefers-reduced-motion: reduce)` se selektory, které v DOM opravdu existují (`document.querySelector('.mud-progress-linear-bar')` na stránce během načítání).

- [ ] **Step 8: Commit**

```bash
git add src/L2Points.Web tests/L2Points.Web.Tests
git commit -m "fix(web): neutral zero percent, visible focus ring, reduced motion"
```

### Task 4: Hlavička stránky a nadpisy sekcí

Nálezy E1, E2 a B2.

**Files:**
- Create: `src/L2Points.Web/Components/Common/PageHeader.razor`
- Create: `src/L2Points.Web/Components/Common/SectionTitle.razor`
- Modify: `src/L2Points.Web/Components/_Imports.razor` (`@using L2Points.Web.Components.Common`)
- Modify: `src/L2Points.Web/wwwroot/app.css:702-731` (`.l2p-page-head`, `.l2p-page-title`, sjednocený `.l2p-section-title`)
- Modify (titulky stránek): `Pages/MyWeek.razor:16`, `Pages/Leaderboard.razor:21`, `Pages/WeekOverview.razor:20`, `Pages/PlayerDetail.razor:23,29,34,41`, `Pages/Points.razor:11`, `Pages/Jewels.razor:12`, `Pages/NotFound.razor:6`, všechny `Pages/Admin/*.razor` s `Typo.h4`
- Modify (sekce): `Pages/Leaderboard.razor:94,103`, `Pages/WeekOverview.razor:69,78`, `Pages/PlayerDetail.razor:53,75,106`, `Pages/Points.razor:33`, `Pages/Admin/AdminActivities.razor:37`
- Modify (nadpisy karet na `h3`): `Pages/Jewels.razor:34`, `Pages/Admin/AdminSchedule.razor:36,55`
- Modify: `src/L2Points.Web/Components/Attendance/WeekGridView.razor:10` a `.razor.css:9-17` (titulek mřížky dostane třídu `l2p-section-title`, tečky zůstávají)
- Modify: `CLAUDE.md` (Struktura: `Components/Common`)

**Interfaces:**
- Produces:
  - `PageHeader`: `[Parameter, EditorRequired] string Title`, `[Parameter] RenderFragment? ChildContent` (ovládání vpravo). Vykreslí `<div class="l2p-page-head">` s `MudText Typo="Typo.h4" HtmlTag="h1" Class="l2p-page-title"`.
  - `SectionTitle`: `[Parameter, EditorRequired] string Text`, `[Parameter] string? Icon`, `[Parameter] RenderFragment? ChildContent` (ovládání v řádku nadpisu). Vykreslí `<div class="l2p-section-head">` s `MudText Typo="Typo.h6" HtmlTag="h2" Class="l2p-section-title">`.

- [ ] **Step 1: Vytvoř obě komponenty a CSS**

- `.l2p-page-head`: flex, `flex-wrap: wrap`, `align-items: center`, `justify-content: space-between`, `gap: 0.5rem 1.5rem`, `margin-bottom: 1.5rem` (Task 5 přepíše na `var(--l2p-page-head)`).
- `.mud-typography.l2p-page-title`: `margin: 0`, váha 500. Pod 600 px `font-size: 1.625rem`.
- `.l2p-section-title`: velikost `1.125rem` (dnes 1.05rem), váha 600, ikona v `--l2p-primary-text`.

- [ ] **Step 2: Nahraď titulky stránek**

Každý titulek stránky nahraď `PageHeader` a ovládání vpravo předej jako `ChildContent`. Na `/leaderboard` je to přepínač „Zobrazit neaktivní“ z `l2p-lb-head`; `l2p-lb-head` pak z `Leaderboard.razor.css` smaž. Na stránkách s `WeekNavigator` zůstává navigátor pod hlavičkou jako dnes.

- [ ] **Step 3: Nahraď nadpisy sekcí**

Ikony (`Icons.Material.Outlined.*`):

| Sekce | Ikona |
|---|---|
| Celkově (Přehled) | `EmojiEvents` (beze změny) |
| Týden (Přehled) | `DateRange` (beze změny) |
| Hráči (Týden) | `Groups` (beze změny) |
| Události týdne (Týden) | `Event` (beze změny) |
| Šperky (Detail hráče) | `Diamond` |
| Historie po týdnech (Detail hráče) | `History` |
| Účast v týdnu (Detail hráče) | `EventAvailable` |
| Normal epicy (`/points`, `/admin/activities`) | `LocalFireDepartment` |
| Chaotic epicy (`/points`, `/admin/activities`) | `Bolt` |
| Castle Siege (`/points`, `/admin/activities`) | `Castle` |

Na `/points` a `/admin/activities` se titulek bere z proměnné `title`. Ikonu přiřaď podle kategorie skupiny, ne podle textu.

- [ ] **Step 4: Nadpisy karet na `h3`**

`Jewels.razor:34` a `AdminSchedule.razor:36,55`: přidej `HtmlTag="h3"`, vzhled se nemění.

- [ ] **Step 5: Ověř v aplikaci**

Na každé stránce z Metodiky auditu proveď:
- `document.querySelectorAll('h1').length === 1`;
- posloupnost `[...document.querySelectorAll('h1,h2,h3,h4,h5,h6')].map(h => h.tagName)` nepřeskakuje úrovně;
- vizuálně jsou nadpisy sekcí stejné na Přehledu, Detailu hráče i Body (1440×900 a 375×812, dark);
- titulek na mobilu má 26 px.

- [ ] **Step 6: CLAUDE.md**

Do Struktury přidej: „`Components/Common`: `PageHeader` (titulek stránky jako `h1` a ovládání vpravo) a `SectionTitle` (nadpis sekce `h2` s ikonou), všechny stránky je používají.“

- [ ] **Step 7: Commit**

```bash
git add src/L2Points.Web CLAUDE.md
git commit -m "feat(web): PageHeader and SectionTitle with a proper heading outline"
```

### Task 5: Škály rozměrů, čáry, hloubka a dlaždice

Nálezy B1, B3, B4, C1, C2, D1, D2 a D3.

**Files:**
- Modify: `src/L2Points.Web/wwwroot/app.css` (škály v `:root`, sklo, dialogy, popovery, dlaždice)
- Modify: všechny `*.razor.css` z auditu (mezery, radiusy, velikosti písma)
- Modify: `src/L2Points.Web/Components/Stats/KpiCard.razor:2`, `Pages/Leaderboard.razor:78`, `Pages/Jewels.razor:28` (třída `l2p-tile`)
- Modify: `src/L2Points.Web/Components/Pages/Points.razor` a `.razor.css`, `Pages/Admin/AdminActivities.razor` a `.razor.css` (pevné šířky sloupců)
- Modify: `src/L2Points.Web/Components/Pages/Admin/AdminJewels.razor.css:9`
- Modify: `docs/superpowers/specs/2026-10-29-glass-ui-design.md` (sekce Sklo a Typografie), `CLAUDE.md` (Konvence)

**Interfaces:**
- Consumes: `--l2p-line*`, `--l2p-shadow-1..3`, `--l2p-scrim` z Task 1.
- Produces: tokeny `--l2p-space-1..7`, `--l2p-gap-title|tiles|section`, `--l2p-pad-card`, `--l2p-page-head`, `--l2p-radius-sm|md|lg|xl|full`, `--l2p-line-width|line-width-strong`, `--l2p-font-xs..3xl`, `--l2p-touch`, `--l2p-touch-gap` (hodnoty podle auditu); třída `.l2p-tile`.

- [ ] **Step 1: Přidej škály do `:root`**

Blok „Theme-independent scales“ z auditu doslova. `--l2p-glass-shadow` ponech jako alias `var(--l2p-shadow-1)`.

- [ ] **Step 2: Převeď mezery**

V `app.css` a v `*.razor.css` převáděj jen mezery mezi bloky (margin, gap, padding karet a sekcí):
- 0,15–0,25 rem → `--l2p-space-1`;
- 0,3–0,5 rem → `--l2p-space-2`;
- 0,6–0,75 rem → `--l2p-space-3`;
- 0,9–1 rem → `--l2p-space-4`;
- 1,25–1,5 rem → `--l2p-space-5`.

Vlastní tokeny dostanou výjimky, jejich hodnoty se nemění:
- buňky tabulek `--l2p-cell-y: 0.35rem`, `--l2p-cell-x: 0.75rem`, `--l2p-cell-edge: 1rem` (`app.css:281-311`);
- buňky mřížky `--l2p-grid-cell-y: 0.125rem` (`WeekGridView.razor.css:39`);
- štítky procent `0.05rem 0.5rem`.

Rytmus:
- `.l2p-section-head` má `margin-bottom: var(--l2p-gap-title)`;
- mezi sekcemi `var(--l2p-gap-section)` (třída `.l2p-section` na obalu sekce, nebo nahrazení `mb-4`/`mt-4` u sekcí, které používají `SectionTitle`);
- `.l2p-kpis`, `.l2p-podium`, `.l2p-jewel-gallery`, `.l2p-wk-cards` mají `gap: var(--l2p-gap-tiles)`.

- [ ] **Step 3: Radiusy a písmo**

Radiusy:
- karty a dialog → `--l2p-radius-xl`, vnořené plochy a dlaždice → `--l2p-radius-lg`;
- čipy, položky draweru, výběr dne a vstupy účtu → `--l2p-radius-md`;
- štítky, checkbox, legenda a `account-error` → `--l2p-radius-sm`;
- `.l2p-wk-meter`, `.l2p-rank-bar` (včetně `span`) → `--l2p-radius-full`;
- `.account-button` → `--l2p-radius-md`.

Písmo:
- `.l2p-wk-kind` → `var(--l2p-font-xs)`;
- `.l2p-kpi-value` → `font-weight: 600`, `var(--l2p-font-3xl)`, pod 600 px `var(--l2p-font-2xl)`;
- `.l2p-kpi-more`, legenda a hlavičky tabulek na nejbližší token (`--l2p-font-sm` / `--l2p-font-md`).

- [ ] **Step 4: Čáry, dialogy, popovery a dlaždice**

Čáry:
- spodní linka hlavičky tabulek (`app.css:269`, `WeekGridView.razor.css:57`, `WeekOverview.razor.css:191`) → `var(--l2p-line-width) solid var(--l2p-line-strong)`;
- oddělovač řádků v `AdminJewels.razor.css:9` → `--l2p-line-subtle`.

Dialogy a popovery:
- `.mud-dialog { border-radius: var(--l2p-radius-xl); border: 1px solid var(--l2p-line); box-shadow: var(--l2p-shadow-3); background: var(--l2p-surface-solid); }`;
- `.mud-popover { border-radius: var(--l2p-radius-md); border: 1px solid var(--l2p-line); box-shadow: var(--l2p-shadow-2); }`;
- `.mud-overlay-dialog .mud-overlay-scrim, .mud-overlay-drawer .mud-overlay-scrim { background-color: var(--l2p-scrim); }`.

Dlaždice:
- `.mud-main-content .mud-paper-outlined.l2p-tile { -webkit-backdrop-filter: none; backdrop-filter: none; border-radius: var(--l2p-radius-lg); }`;
- `l2p-tile` přidej do `KpiCard`, karet stupňů vítězů a karet šperků.

- [ ] **Step 5: Sloupce na `/points` a `/admin/activities`**

Tabulky dostanou třídu `l2p-catalog-table` se stejným pravidlem jako `RankingTable.razor.css:5-20`:
- `table-layout: fixed`;
- 1. sloupec (Název) `14rem`, bodové sloupce `6rem`, Rozpis zbytek;
- `min-width: 36rem`, pod ní karta scrolluje.

Na `/admin/activities` mají sloupce Aktivní a Upravit `6rem`.

- [ ] **Step 6: Ověř v aplikaci**

1440×900 a 375×812, dark i light:
- `/leaderboard` a `/jewels` mají nejvýš 4 prvky s computed `backdrop-filter` jiným než `none` (app bar, drawer a karty tabulek);
- v DOM nezůstal radius 4 px, 3,2 px ani 9 px mimo MudBlazor vnitřky (switch track může zůstat);
- nejmenší velikost písma textu je 12 px;
- sloupce Body na `/points` jsou pod sebou zarovnané;
- dialog na `/admin/players` má radius 12 px a hranu;
- skript kontrastu nic nehlásí.

Screenshot dialogu ulož jako `after-06-dialog-dark.jpg`.

- [ ] **Step 7: Dokumentace**

- Spec glass UI, sekce Sklo: „dlaždice v mřížce (`.l2p-tile`) bez blur“, škála radiusů a stínů a pravidlo ikon (I2): navigace a akce Filled, nadpisy sekcí Outlined.
- CLAUDE.md Konvence: „Mezery, radiusy, písmo a stíny berou hodnoty ze škál `--l2p-space-*`, `--l2p-radius-*`, `--l2p-font-*`, `--l2p-shadow-*`, ne pevné rem/px.“

- [ ] **Step 8: Commit**

```bash
git add src/L2Points.Web docs CLAUDE.md
git commit -m "feat(web): spacing, radius, type and elevation scales; tiles without blur"
```

### Task 6: Dotykové cíle, posun na dnešek a účtové formuláře

Nálezy H1, H3, H4, E8 a G3.

**Files:**
- Modify: `src/L2Points.Web/Components/Attendance/WeekGridView.razor.css:346-364`
- Modify: `src/L2Points.Web/Components/Attendance/WeekGridView.razor` (`@inject IJSRuntime JS`, `OnAfterRenderAsync`)
- Modify: `src/L2Points.Web/wwwroot/js/scroll.js`
- Modify: `src/L2Points.Web/Components/Attendance/WeekNavigator.razor.css:24-48`
- Modify: `src/L2Points.Web/Components/Pages/WeekOverview.razor.css:19-31,256-266`
- Modify: `src/L2Points.Web/wwwroot/app.css` (blok `@media (pointer: coarse), (max-width: 599.98px)`, `.account-field input`, `.account-password`)
- Create: `src/L2Points.Web/wwwroot/js/account.js`
- Modify: `src/L2Points.Web/Components/App.razor:53` (načtení `js/account.js`)
- Modify: `src/L2Points.Web/Components/Account/Login.razor:27-31`, `Components/Account/ChangePassword.razor:31-45`

**Interfaces:**
- Produces:
  - JS `l2pScroll.gridsToToday(): void`. Jen pro `window.innerWidth < 600`: v každém `.l2p-grid-scroll` najde `th.l2p-grid-today` a nastaví `scrollLeft = th.offsetLeft - th.parentElement.firstElementChild.offsetWidth`. Bez dnešního sloupce `scrollLeft = 0`. Bez animace.
  - JS `account.js`: delegovaný `click` na `button[data-l2p-password-toggle]`. Přepne `type` inputu se stejným `id`, jako je hodnota atributu, mezi `password` a `text`, přepne `aria-pressed` a `aria-label` „Zobrazit heslo“ / „Skrýt heslo“.

- [ ] **Step 1: Dotykové cíle v `app.css` a scoped CSS**

Blok `@media (pointer: coarse), (max-width: 599.98px)`:
- zaškrtávátko v mřížce `width/height: var(--l2p-touch)`, `padding: 12px`, `.l2p-grid-check + .l2p-grid-check { margin-top: 4px }`; dosavadní 36px pravidlo v `WeekGridView.razor.css:358-363` nahraď;
- šipky týdne `width/height: var(--l2p-touch)`, „Tento týden“ `min-height: var(--l2p-touch)`;
- výběr dne `min-height: var(--l2p-touch)`, `gap: var(--l2p-touch-gap)`;
- popisky Karty/Mřížka `min-height: var(--l2p-touch)` a `padding-inline: var(--l2p-space-2)`;
- přepínače (`.mud-switch`) ve `.mud-main-content` `min-height: var(--l2p-touch)`;
- řádky tabulek `padding-block: 0.6rem`; odkaz v `.l2p-nick-cell` `display: inline-block; padding-block: 0.25rem`;
- položky draweru `min-height: var(--l2p-touch)`.

- [ ] **Step 2: Posun mřížky na dnešek**

- Doplň `l2pScroll.gridsToToday` do `scroll.js` podle Interfaces.
- `WeekGridView` si pamatuje `Week? _scrolledWeek`. V `OnAfterRenderAsync` zavolá JS, jen když `Grid.Week != _scrolledWeek`, a pak `_scrolledWeek = Grid.Week`.
- `JSDisconnectedException` spolkni jako v `PlayerDetail.razor:224-229`.

- [ ] **Step 3: Účtové formuláře**

- `.account-field input` `padding: 0.75rem` (výška ≥ 44 px).
- Pole hesla obal `<div class="account-password">` s tlačítkem `<button type="button" class="account-password-toggle" data-l2p-password-toggle="login-password" aria-pressed="false" aria-label="Zobrazit heslo">` (ikona oka jako inline SVG, 44×44, absolutně vpravo v poli).
- Stejně pro `cp-current`, `cp-new` a třetí pole v `ChangePassword.razor`.
- Vytvoř `account.js` podle Interfaces a načti ho v `App.razor` za `scroll.js`.

- [ ] **Step 4: Ověř v aplikaci**

375×812 dark, skript z `l2p-tt` v auditu (vypiš interaktivní prvky menší než 44×44):
- na `/`, `/week` a `/leaderboard` nezbude nic kromě odkazů uvnitř textu;
- zaškrtávátka V a R jsou od sebe aspoň 4 px.

Review Focus 4:
- na `/` ve čtvrtek je dnešní sloupec hned za sloupcem bossů;
- šipka na předchozí týden vrátí mřížku na začátek;
- „Tento týden“ zase posune;
- zaškrtnutí políčka mřížkou nepohne;
- na 1440×900 se neposouvá.

Na `/profile` přepínač zobrazí a skryje heslo a čtečka hlásí stav (`aria-pressed`).

- [ ] **Step 5: Commit**

```bash
git add src/L2Points.Web
git commit -m "feat(web): 44px touch targets, scroll the grid to today, password toggle"
```

### Task 7: Akce hráčů v adminu

Nález H2.

**Files:**
- Create: `src/L2Points.Web/Components/Admin/PlayerActionState.cs`
- Create: `tests/L2Points.Web.Tests/Admin/PlayerActionStateTests.cs`
- Create: `src/L2Points.Web/Components/Admin/PlayerActions.razor` (+ `.razor.css`)
- Modify: `src/L2Points.Web/Components/Pages/Admin/AdminPlayers.razor:37-128,151-162`, `AdminPlayers.razor.css`

**Interfaces:**
- Produces:
  - `public sealed record PlayerActionState(bool IsSelf, string? ProtectedText, bool CanResetPassword, string ResetPasswordHint, bool CanDeactivate, bool CanDelete, bool ShowAdminToggle, bool CanRemoveAdmin)`.
  - `public static PlayerActionState For(AdminPlayer player, string? actorId, bool actorIsSuperAdmin)`.
  - Aktivace neaktivního hráče je povolená vždy (jako dnes), proto nemá vlastní příznak. `CanDeactivate` platí jen pro aktivního hráče.
  - Konstanty `PlayerActionState.SuperAdminProtectedText = "Super admina nelze měnit."`, `AdminProtectedText = "Jiného admina může měnit jen super admin."`, `OwnPasswordText = "Vlastní heslo změníš v profilu."`.
  - `PlayerActions.razor`:
    - parametry `[EditorRequired] AdminPlayer Player`, `[EditorRequired] PlayerActionState State`, `bool Busy`;
    - `EventCallback OnEdit`, `OnResetPassword`, `OnDelete`;
    - `EventCallback<bool> OnSetActive`, `OnSetAdmin`.

- [ ] **Step 1: Napiš padající testy `PlayerActionStateTests`**

Přenáší přesně dnešní pravidla z `AdminPlayers.razor:50-55` a `:84-117`. `AdminPlayer` sestavuj s `ActiveFrom = new DateOnly(2026, 10, 26)`.

| Test | Situace | Očekávání |
|---|---|---|
| `Self_cannot_reset_deactivate_delete_or_remove_admin` | `player.UserId == actorId` | `IsSelf`, `ProtectedText == null`, `CanResetPassword/CanDeactivate/CanDelete/CanRemoveAdmin == false`, `ResetPasswordHint == OwnPasswordText` |
| `Super_admin_is_protected_from_other_admins` | cíl `IsSuperAdmin`, aktér běžný admin | `ProtectedText == SuperAdminProtectedText`, nic z reset/deaktivace/mazání |
| `Super_admin_target_is_protected_from_any_other_account` | cíl `IsSuperAdmin`, `actorIsSuperAdmin = true`, jiný id | `ProtectedText == SuperAdminProtectedText` |
| `Other_admin_is_protected_from_a_regular_admin` | cíl `IsAdmin`, aktér ne super admin | `ProtectedText == AdminProtectedText`, `ShowAdminToggle == false` |
| `Super_admin_manages_other_admins` | cíl `IsAdmin`, `actorIsSuperAdmin = true` | `ProtectedText == null`, všechno `true`, `ShowAdminToggle == true` |
| `Regular_player_is_managed_by_any_admin` | cíl bez rolí, aktér běžný admin | `CanResetPassword/CanDeactivate/CanDelete == true`, `ShowAdminToggle == false`, `ResetPasswordHint == "Reset hesla"` |
| `Protected_hint_is_the_reset_hint` | chráněný cíl | `ResetPasswordHint == ProtectedText` |

- [ ] **Step 2: Spusť, musí padat**

Run: `dotnet test tests/L2Points.Web.Tests --filter FullyQualifiedName~PlayerActionStateTests`
Expected: FAIL (build, typ neexistuje).

- [ ] **Step 3: Implementuj `PlayerActionState.For`**

- [ ] **Step 4: Spusť, musí projít**

Run: `dotnet test tests/L2Points.Web.Tests --filter FullyQualifiedName~PlayerActionStateTests`
Expected: PASS.

- [ ] **Step 5: Komponenta `PlayerActions`**

- Viditelně `MudIconButton` Upravit a Účast (`Size.Medium`, 40×40 na desktopu, 44×44 na coarse) a `MudMenu` s ikonou `MoreVert` a `aria-label` „Další akce: {Nick}“.
- V menu postupně: Šperky (odkaz), Reset hesla, Deaktivovat/Aktivovat, Udělit/Odebrat admina (jen `ShowAdminToggle`), `MudDivider`, Smazat (text a ikona v `--l2p-danger`).
- Zakázaná položka má `Disabled="true"` a pod textem druhý řádek s důvodem (`ProtectedText`, u resetu `ResetPasswordHint`, u vlastního účtu „Vlastní účet“) ve třídě `l2p-menu-hint` (`--l2p-text-2`, `--l2p-font-xs`). Důvod tak není jen v tooltipu.

- [ ] **Step 6: `AdminPlayers.razor`**

- Výpočet stavu nahraď `PlayerActionState.For(player, _actorId, ActorIsSuperAdmin)` a konstanty z `:151-152` smaž.
- Tabulka dostane `Class="d-none d-sm-block"` a sloupec Akce vykreslí `PlayerActions`.
- Pod ní `d-sm-none` seznam karet (`MudPaper Outlined Class="l2p-tile l2p-ap-card"`):
  - 1. řádek nick (odkaz) a čipy stavu a role;
  - 2. řádek „Login · Aktivní od“ v `--l2p-text-2`;
  - 3. řádek `PlayerActions`;
  - mezera mezi kartami `var(--l2p-gap-tiles)`, nick s `overflow-wrap: anywhere`.

- [ ] **Step 7: Ověř v aplikaci**

1440×900:
- tabulka má v Akcích 3 prvky a menu obsahuje zbytek;
- pro vlastní účet a super admina jsou položky zakázané s důvodem;
- Smazat je oddělené čarou a červené;
- akce menu otevřou stejné dialogy jako dřív. Dialogy jen otevři a zavři, nic neukládej.

375×812:
- karty místo tabulky, žádný vodorovný scroll stránky;
- Review Focus 1: v DevTools dočasně přepiš text nicku v DOM na `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA` (32 znaků) a karta nepřeteče.

- [ ] **Step 8: Commit**

```bash
git add src/L2Points.Web tests/L2Points.Web.Tests
git commit -m "feat(web): admin player actions as a menu and mobile cards"
```

### Task 8: Formáty a texty

Nálezy E3, E4, E5, E6, E7 a H7.

**Files:**
- Modify: `src/L2Points.Web/Formatting/DisplayFormat.cs:117-127` (+ nová metoda)
- Modify: `tests/L2Points.Web.Tests/Formatting/DisplayFormatTests.cs:110-122` (+ nové testy)
- Modify: `src/L2Points.Web/Components/Pages/Points.razor:5,60`, `Pages/Admin/AdminActivities.razor:7,61` (`@inject TimeProvider Time`)
- Modify: `src/L2Points.Web/Components/Pages/WeekOverview.razor:251-253`
- Modify: `src/L2Points.Web/Components/Attendance/WeekSummaryCard.razor:11`, `Components/Stats/StatCells.razor:7`
- Modify: `src/L2Points.Web/Components/Pages/PlayerDetail.razor:98` (`Toned="true"`)
- Modify: `src/L2Points.Web/Components/Pages/Leaderboard.razor`, `Pages/WeekOverview.razor` (KPI „Hráč týdne“), `wwwroot/app.css` (`.l2p-kpi-name`)
- Modify: `src/L2Points.Web/Components/Pages/Admin/AdminSchedule.razor` (placeholder a label polí spawnu)

**Interfaces:**
- Produces:
  - `DisplayFormat.ScheduleLabel(Activity a, Week week, bool withUtc = false) -> string`. U `ScheduleType.Weekly` s `TimeUtc` vezme datum `week.Days.First(d => d.DayOfWeek == a.WeeklyDay)` a čas převede `AppTime.ToPragueTime`. Výstup „Týdně v sobotu 21:00“, s `withUtc` „Týdně v sobotu 21:00 (20:00 UTC)“. Ostatní typy beze změny.
  - `DisplayFormat.AverageAttendees(double average) -> string`. Číslo formátované `"0.#"` v cs-CZ. Desetinné číslo dostane „hráče“, 1 „hráč“, 2–4 „hráči“, jinak „hráčů“. Výstup „průměrně {n} {slovo} na epic“.

- [ ] **Step 1: Napiš a uprav padající testy**

V `Schedule_label_*` (`:110-122`) volej `ScheduleLabel(activity, week)` s `week = Week.Containing(new DateOnly(2026, 11, 2))` a očekávej:
- Baium „Týdně v sobotu 21:00“, Castle Siege „Týdně v neděli 19:00“, ostatní beze změny.

Nové testy:
- `Schedule_label_weekly_in_summer_time`: `Week.Containing(new DateOnly(2027, 6, 7))`, Baium „Týdně v sobotu 22:00“.
- `Schedule_label_weekly_across_the_dst_switch` (Review Focus 3): `Week.Containing(new DateOnly(2026, 10, 19))`, Baium „Týdně v sobotu 22:00“, Castle Siege „Týdně v neděli 19:00“.
- `Schedule_label_weekly_with_utc`: zimní týden, `withUtc: true`, Baium „Týdně v sobotu 21:00 (20:00 UTC)“.
- `Schedule_label_weekly_without_time`: `ScheduleType.Weekly`, `WeeklyDay = DayOfWeek.Saturday`, `TimeUtc = null`, očekávej „Týdně v sobotu“.
- `[Theory] AverageAttendees_czech_plural`: `(1.0, "průměrně 1 hráč na epic")`, `(1.5, "průměrně 1,5 hráče na epic")`, `(3.0, "průměrně 3 hráči na epic")`, `(5.0, "průměrně 5 hráčů na epic")`, `(0.33, "průměrně 0,3 hráče na epic")`, `(4.96, "průměrně 5 hráčů na epic")`.

- [ ] **Step 2: Spusť, musí padat**

Run: `dotnet test tests/L2Points.Web.Tests --filter FullyQualifiedName~DisplayFormatTests`
Expected: FAIL (signatura, chybějící metoda).

- [ ] **Step 3: Implementuj obě metody**

- [ ] **Step 4: Spusť, musí projít**

Run: `dotnet test tests/L2Points.Web.Tests`
Expected: PASS.

- [ ] **Step 5: Zapoj do stránek**

- `/points`: `ScheduleLabel(context, Week.Containing(AppTime.TodayInPrague(Time)))`.
- `/admin/activities`: totéž s `withUtc: true`.
- `WeekOverview.razor:251-253`: `AverageNote` volá `DisplayFormat.AverageAttendees(...)`.
- `WeekSummaryCard.razor:11` a `StatCells.razor:7`: `DisplayFormat.Points(...)`.
- `PlayerDetail.razor:98`: `<StatCells ... Toned="true" />`.

- [ ] **Step 6: KPI se jménem a rozpis**

KPI „Hráč týdne“ na Přehledu a Týdnu:
- hodnota jména dostane třídu `l2p-kpi-name` (`var(--l2p-font-xl)`, váha 600, `--l2p-text`, podtržení při hoveru a focusu) místo velkého primary čísla;
- čísla zůstávají `l2p-kpi-value`.

`/admin/schedule`: placeholder spawnu „HH:mm“, label pole „Večerní (UTC)“ / „Ranní (UTC)“, `inputmode="numeric"` (přes `InputMode` nebo `UserAttributes` u `MudTextField`).

- [ ] **Step 7: Ověř v aplikaci**

- `/points` ukazuje pražské časy;
- `/admin/activities` ukazuje pražský čas i s UTC;
- Můj týden a Detail hráče mají „1 450“;
- historie má tónová procenta;
- KPI „Hráč týdne“ je text 20 px;
- placeholder v rozpisu se neořízne (1440×900 i 375×812).

- [ ] **Step 8: Commit**

```bash
git add src/L2Points.Web tests/L2Points.Web.Tests
git commit -m "fix(web): Prague times on schedules, Czech plurals, consistent points and tones"
```

### Task 9: Načítání a prázdné stavy

Nálezy F1, F2, F3 a F4.

**Files:**
- Create: `src/L2Points.Web/Components/Common/LoadingSkeleton.razor`
- Create: `src/L2Points.Web/Components/Common/EmptyState.razor`
- Modify (`MudProgressLinear … Class="my-4"` → `LoadingSkeleton`):
  - `Pages/MyWeek.razor:32`, `Pages/Leaderboard.razor:33`, `Pages/WeekOverview.razor:41`, `Pages/PlayerDetail.razor:30,119`, `Pages/Points.razor:21`, `Pages/Jewels.razor:21`;
  - `Attendance/EditableWeekGrid.razor:16`;
  - `Pages/Admin/AdminActivities.razor:25`, `AdminAttendance.razor:28,58`, `AdminJewels.razor:25`, `AdminPlayers.razor:28`, `AdminSchedule.razor:29`, `AdminSettings.razor:22`.
  - Tenké `Size.Small` ukazatele při změně týdne zůstávají.
- Modify (prázdné stavy): `Pages/PlayerDetail.razor:56,78`, `Pages/WeekOverview.razor:106`, `Pages/Jewels.razor:40`, `Pages/Admin/AdminAttendance.razor`, `Pages/Admin/AdminJewels.razor` (bez vybraného hráče)
- Modify: `Pages/Jewels.razor.css:20-26` (smazat mrtvá pravidla), `Pages/WeekOverview.razor:173` a `.razor.css:241-243`
- Modify: `wwwroot/app.css` (`.l2p-skeleton`, `.l2p-empty`)
- Modify: `CLAUDE.md` (Struktura, `Components/Common`)

**Interfaces:**
- Produces:
  - `LoadingSkeleton`: parametry `int Kpis = 0`, `int Rows = 5`, `bool Grid = false`. Vykreslí `aria-busy="true"` obal s `MudSkeleton`:
    - `Kpis` dlaždic výšky 96 px v `.l2p-kpis`;
    - pak buď 2 karty mřížky výšky 240 px (`Grid`), nebo kartu s hlavičkou 34 px a `Rows` řádky výšky 33 px.
    - Barva `--l2p-glass-strong`, `Animation="Animation.Wave"`, při `prefers-reduced-motion` bez vlny (CSS).
  - `EmptyState`: parametry `[EditorRequired] string Text`, `string Icon = Icons.Material.Outlined.Inbox`, `RenderFragment? ChildContent` (akce). Ikona 24 px v `--l2p-muted`, text `--l2p-text-2`, padding `var(--l2p-space-4)`.

- [ ] **Step 1: Vytvoř obě komponenty a CSS**

- [ ] **Step 2: Nahraď načítání**

Parametry podle obsahu stránky:

| Stránka | `LoadingSkeleton` |
|---|---|
| Přehled | `Kpis="4" Rows="5"` |
| Týden | `Kpis="4" Rows="5"` |
| Můj týden, `EditableWeekGrid` | `Grid="true"` |
| Detail hráče | `Rows="4"` |
| ostatní stránky | výchozí |

- [ ] **Step 3: Nahraď prázdné stavy**

| Místo | Text | Ikona |
|---|---|---|
| Detail hráče, šperky | „Zatím žádný šperk.“ | `Diamond` |
| Detail hráče, historie | „Zatím tu žádná historie není.“ | `History` |
| Týden, den bez událostí | dnešní texty | `EventBusy` |
| Šperky, nikdo nedrží | „Zatím nikdo“ | výchozí |
| `/admin/attendance` bez hráče | „Vyber hráče, kterému chceš opravit účast.“ | `PersonSearch` |
| `/admin/jewels` bez hráče | „Vyber hráče, kterému chceš přidat nebo odebrat šperk.“ | `PersonSearch` |

U „Zatím nikdo“ na `/jewels` použij kompaktní variantu: komponenta dostane parametr `bool Compact` bez ikony a s menším paddingem.

Mrtvá pravidla `.l2p-jewel-total` a `.l2p-jewel-none` smaž. Jejich záměr (sekundární barva počtu) přesuň do `Class="mud-text-secondary"` u `MudText`.

`WeekOverview.razor:173`: znak „–“ a třída s `color: var(--l2p-muted)` místo `--mud-palette-text-disabled`.

- [ ] **Step 4: Ověř v aplikaci**

- Při načtení každé stránky se ukáže skeleton ve tvaru obsahu. Na ověření dočasně (necommitovat) přidej `await Task.Delay(1500);` na začátek načítání Přehledu a Můj týden, udělej screenshot a zpoždění zase odstraň.
- Obsah pak nastoupí bez skoku větším než jeden řádek.
- Prázdné stavy vypadají všude stejně, oba režimy.
- Pohled Mřížka na `/week` ukazuje „–“ čitelně; skript kontrastu nic nehlásí.

- [ ] **Step 5: CLAUDE.md**

Doplň do věty o `Components/Common`: „`LoadingSkeleton` (načítání ve tvaru obsahu) a `EmptyState` (prázdný stav s ikonou a textem).“

- [ ] **Step 6: Commit**

```bash
git add src/L2Points.Web CLAUDE.md
git commit -m "feat(web): loading skeletons and one empty state component"
```

### Task 10: Ikony šperků, přilepené buňky a uzavření auditu

Nálezy I1 a C3, odložené H5 a H6.

**Files:**
- Modify: `src/L2Points.Web/Components/Jewels/JewelIcon.razor`, `JewelIcon.razor.css`
- Modify: `src/L2Points.Web/wwwroot/app.css:255-258,621-629`, `Components/Attendance/WeekGridView.razor.css:66-78`, `Pages/WeekOverview.razor.css:203-214`, `Pages/PlayerDetail.razor.css:36-42`
- Modify: `src/L2Points.Web/wwwroot/js/scroll.js` (třída `l2p-scrolled`)
- Modify: `docs/superpowers/plans/2026-10-28-00-roadmap.md` (Poznámky / Odloženo), `docs/superpowers/specs/2026-10-29-ui-ux-audit.md` (stav nálezů), `CLAUDE.md` (Stav)

**Interfaces:**
- Consumes: `--l2p-icon-tile`, `--l2p-surface-pinned`, `--l2p-line`, `--l2p-radius-sm` z Task 1 a 5.
- Produces: JS posluchač na `document` (`scroll`, `capture: true`). Na prvcích `.l2p-grid-scroll`, `.l2p-wk-grid-scroll` a `.mud-table-container` přepíná třídu `l2p-scrolled` podle `scrollLeft > 0`.

- [ ] **Step 1: Rámeček ikony šperku**

`JewelIcon` obalí `<img>` do `<span class="l2p-jewel-tile">`: `background: var(--l2p-icon-tile)`, `border: 1px solid var(--l2p-line)`, `border-radius: var(--l2p-radius-sm)`, `padding: 2px`, `line-height: 0`. Počet kusů zůstává vedle rámečku. `Decorative` a `alt` se nemění.

- [ ] **Step 2: Přilepené buňky**

- Všechny přilepené buňky na nečlenském řádku → `var(--l2p-surface-pinned)`; řádek „já“ dál `--l2p-row-me-solid`.
- Do `scroll.js` doplň posluchač podle Interfaces.
- Pravidlo `.l2p-scrolled .l2p-nick-cell, .l2p-scrolled .l2p-grid-boss, .l2p-scrolled .l2p-wk-grid-nick, .l2p-scrolled .l2p-pd-week { box-shadow: 6px 0 8px -6px rgba(0, 0, 0, 0.35); }` (u řádku „já“ se zachová i proužek `inset 3px 0 0`: oba stíny v jednom `box-shadow`).

- [ ] **Step 3: Ověř v aplikaci**

375×812 dark i light:
- na `/leaderboard`, `/week` a `/` nepůsobí přilepený sloupec jako tmavá krabice;
- stín se objeví až po posunu tabulky;
- ikona šperku je v light rámeček, ne černý čtverec (Semtex v žebříčku, `/jewels`, `/admin/jewels`);
- Review Focus 1: nick o 32 znacích (dočasně v DOM) s ikonou v přilepené buňce se zalomí a nepřeteče.

Screenshot žebříčku v light ulož jako `after-07-leaderboard-light.jpg`.

- [ ] **Step 4: Uzavření auditu**

- V auditu do každé sekce připiš „Stav: opraveno v `feature/ui-audit`“ u řešených nálezů. H5 (žebříček na mobilu) a H6 (spodní navigace) označ „odloženo“.
- Do roadmapy (sekce s odloženými věcmi) přidej H5 a H6 s odkazem na audit.
- V CLAUDE.md doplň do řádku Stav „UI audit (spec `docs/superpowers/specs/2026-10-29-ui-ux-audit.md`)“.

- [ ] **Step 5: Celkové ověření**

- Run: `dotnet build` a `dotnet test`. Expected: build bez varování nových v upravených souborech, všechny testy PASS.
- Projdi všechny stránky z Metodiky auditu (1440×900 a 375×812, dark i light) se skriptem kontrastu v obou režimech měření. Expected: nic pod limitem kromě prvků výslovně vyjmutých v Global Constraints.

- [ ] **Step 6: Commit**

```bash
git add src/L2Points.Web docs CLAUDE.md
git commit -m "feat(web): framed jewel icons, softer pinned cells; close the UI audit"
```

## Mimo rozsah

- H5 (žebříček na mobilu jen s body a Celkem a rozbalením řádku) a H6 (spodní navigace na mobilu) jsou větší změny navigace a obsahu. Zapisují se do roadmapy (Task 10), ne sem.
- A9 (kontrast pruhu bodů) zůstává podle auditu beze změny.
- I2 (Filled a Outlined ikony) je jen pravidlo do specu, bez změny kódu: Task 5 Step 7 ho dopíše do sekce Sklo.
