# Discord report bot – design

Datum: 30. 10. 2026. Doplňuje hlavní spec `2026-10-28-l2points-design.md` (evidence účasti).

## Proč

Účast se dnes zapisuje ručně ve webu: každý hráč si odklikne své bossy, nebo to za něj opraví admin. Po večerní sérii chaoticů je to pro sedm lidí několik desítek kliknutí a část lidí na zápis zapomene. Parta přitom ze hry stejně sdílí screen okna Command Channel Info s rozbalenou partou. Cíl je, aby jeden člověk v Discordu nahrál ten screen, bot z něj přečetl nicky a jedním potvrzením zapsal účast všem, kdo na screenu jsou.

Aplikace dnes eviduje jednu partu, takže jeden report = jeden screen = jedna parta.

## Cíle

- Slash command `/report` v Discordu s přiloženým screenem rozbalené party (okno *Command Channel Info*, pravý panel *Party Members*).
- Bot přečte nicky z panelu *Party Members*, spáruje je s hráči v aplikaci a nabídne:
  - **datum** (předvyplněné podle času reportu),
  - **epicy** (výběr více událostí toho dne, rozumně předvybraný podle rozpisu),
  - potvrzení nejistých shod nicků.
- Jedním tlačítkem zapíše účast všem potvrzeným hráčům na všech vybraných událostech.
- Do kanálu pošle veřejné shrnutí, co se zapsalo a kdo report poslal.
- Kdo smí `/report` používat, nastavuje správce Discord serveru v Discordu, ne v aplikaci.

## Ne-cíle

- Bot **neodebírá** účast. Opravy a odebrání se dělají ve webu (`/admin/attendance`).
- Bot **nezakládá** hráče z neznámých nicků a nepřidává hráče, kteří na screenu nejsou. Kdo na screenu chybí (spadl, odpojil se, byl v jiné partě), doplní si účast sám ve webu v rámci okna úprav.
- **Jeden screen na report.** Víc part ani víc screenů najednou se neřeší.
- Žádné propojení Discord účtů s účty v aplikaci. Oprávnění řeší Discord.
- Žádné aliasy nicků: nick v aplikaci je jméno postavy ve hře.
- Žádný prefix příkaz (`.report`), žádné trvalé spojení na Discord gateway, žádný samostatně hostovaný bot.
- Žádná nová stránka ve webu pro správu reportů (případně později).

## Anti-cíle (selhání, i kdyby to technicky fungovalo)

- Kdokoli mimo náš Discord server nebo bez přiděleného oprávnění dokáže přes endpoint zapsat účast (podvržený požadavek, jiný server, chybějící ověření podpisu).
- Bot zapíše účast hráči, který na screenu nebyl, kvůli volnému párování nicků bez potvrzení nebo kvůli tomu, že model „domyslí“ nick ze seznamu známých hráčů.
- Bot tiše zapíše jiný epic nebo den, než uživatel viděl na obrazovce (předvýběr se změní mezi zobrazením a zápisem, dvojí zápis, zápis po vypršení).
- Report přepíše nebo smaže existující účast.
- Klíč k Claude API nebo tajemství Discordu skončí v repu, v logu nebo ve zprávě.
- Výpadek nebo chyba čtení screenu shodí aplikaci nebo zablokuje web.

## Chování

### Průběh reportu

1. Uživatel s oprávněním napíše v Discordu `/report` a jako povinný parametr přiloží obrázek.
2. Bot do 3 sekund odpoví odloženou odpovědí („Zpracovávám…“), viditelnou jen pro autora (ephemeral).
3. Aplikace stáhne obrázek, pošle ho Claude API a dostane seznam nicků z panelu *Party Members*.
4. Aplikace nicky spáruje s hráči a nahradí odloženou odpověď zprávou (dál jen pro autora):
   - **Nalezení hráči** – přesná shoda.
   - **Nejisté shody** – podobný nick, např. „V0lume → Volume?“. Výchozí stav: **nepotvrzené**, uživatel je musí vědomě zaškrtnout.
   - **Neznámé nicky** – jen informačně, nezapisují se.
   - **Datum** – výběr jedné hodnoty.
   - **Epicy** – výběr více hodnot.
   - Tlačítka **Zapsat** a **Zrušit**.
5. Změna data přepočítá nabídku epiců a jejich předvýběr (zpráva se aktualizuje).
6. **Zapsat** uloží účast každému potvrzenému hráči na každé vybrané události. Ephemeral zpráva se změní na výsledek (bez ovládacích prvků) a do kanálu odejde veřejné shrnutí, např.:
   > 30. 10. – Chaotic Queen Ant, Chaotic Core, Chaotic Orfen (Večerní): zapsáno 7 hráčů. Report: Hubkins
7. **Zrušit** nic nezapíše a ovládací prvky odstraní.

Pokud screen panel *Party Members* neobsahuje nebo z něj nejde přečíst žádný nick, bot to oznámí a nic nenabízí. Když na screenu není žádný hráč z aplikace, bot vypíše přečtené nicky a tlačítko Zapsat nenabídne.

### Čtení screenu

- Čte se **jen** panel *Party Members* (seznam členů vybrané party, max. 9 řádků, sloupec *Party Members*). Levý seznam leaderů, *Owner*, třídy, K/D čísla, nicky postav ve hře kolem a ostatní text se ignorují.
- Model nick **přepíše tak, jak je na screenu**, znak po znaku. Seznam nicků z aplikace modelu poslat lze jako nápovědu pro čitelnost (rozhodnutí uživatele padlo na „model + seznam“), ale prompt musí výslovně zakázat nahrazovat přečtený text nickem ze seznamu; párování dělá vždy kód.
- Odpověď modelu je strukturovaná (JSON se seznamem nicků a příznakem, zda panel na screenu byl).
- Screen se po zpracování neukládá, ukládají se jen přečtené nicky (*moje volba*: méně dat, nic citlivého navíc).

### Párování nicků (doménová logika, TDD)

- Nick v aplikaci je vždy jméno postavy ve hře, jen se nerozlišuje velikost písmen (potvrzeno uživatelem). Aliasy ani víc jmen postav na hráče nejsou potřeba.
- Kandidáti: aktivní účty, kterým admin může zapsat účast, tedy hráči a čtenáři zobrazovaní ve statistikách. Skrytí čtenáři a deaktivovaní hráči kandidáty nejsou (*moje volba*; skrytému čtenáři služba zápis stejně odmítne).
- **Přesná shoda**: shoda nicku bez ohledu na velikost písmen a okrajové mezery (stejné pravidlo, jako hlídá unikátnost nicku v `PlayerAdminService`).
- **Nejistá shoda**: po sjednocení zaměnitelných znaků (`0`↔`O`, `1`↔`l`↔`I`, `5`↔`S` a podobně, konkrétní sadu určí builder) se nick shoduje, nebo je editační vzdálenost malá (prahová hodnota na builderovi, typicky 1–2 podle délky nicku). Pokud jednomu přečtenému nicku odpovídá víc kandidátů, nabídne se nejbližší a remíza se nenabízí vůbec (neznámý nick).
- Jeden hráč aplikace může být spárován nejvýš s jedním přečteným nickem.
- Vše ostatní je **neznámý nick**.

### Datum a epicy

- **Čas reportu** = okamžik, kdy aplikace přijala `/report` (přes `TimeProvider`), převedený do Europe/Prague.
- **Nabídka data**: dnešek a 6 dní zpátky v Europe/Prague, nejdřív však začátek evidence (*moje volba*: stejný rozsah jako okno úprav hráče; starší opravy patří do webu).
- **Výchozí datum** (*moje volba*): datum nejpozdějšího výskytu, který spadá do předvýběru (viz níže). Když žádný takový není, dnešek. Report poslaný po půlnoci za večerní blok tak dostane správně včerejší datum i předvýběr.
- **Nabídka epiců** pro zvolené datum:
  - všechny pevné výskyty toho dne podle rozpisu z DB (`ScheduleCalculator.GetFixedOccurrences`): chaotic po slotech (např. „Chaotic Core – Večerní“), Baium, Antharas/Valakas podle kotvy, Castle Siege,
  - všechny aktivní flexibilní bossy (Queen Ant, Core, Orfen, Zaken, Frintezza), protože nemají pevný čas a mohou padnout kdykoli,
  - řazení jako v mřížce (`SortOrder`, pak slot).
- **Předvýběr** (*moje volba*, upřesnění „aktuální slot, co už proběhlo a není zapsané“):
  - pevné výskyty s časem, jejichž spawn (datum + `TimeUtc` v UTC) je v intervalu (čas reportu − 6 h, čas reportu],
  - a které zatím nemá zapsané nikdo ze spárovaných hráčů.
  - Okno 6 h spolehlivě oddělí ranní (02:30–04:00 Praha) a večerní (19:00–20:30 Praha) blok chaoticů. Hodnota je konstanta nebo konfigurace, ne pravidlo v DB.
  - Okno se počítá od času reportu bez ohledu na kalendářní den, takže může sahat do včerejška.
  - Výskyty bez času se nepředvybírají nikdy: flexibilní bossové a Antharas/Valakas (v katalogu mají `TimeUtc = null`). V nabídce jsou, uživatel je zaškrtne sám (*moje volba*: bez známého času by předvýběr hádal).
  - Když uživatel přepne na datum, ve kterém žádný výskyt z okna není, nepředvybírá se nic.
- Discord select menu má limit 25 položek; den v aktuálním katalogu jich má nejvýš 15. Kdyby nabídka limit přesáhla, builder ji zkrátí rozumně (např. flexibilní až za pevnými) a nesmí spadnout.

### Zápis

- Pro každou dvojici (potvrzený hráč, vybraná událost) se volá `AttendanceService.SetAttendanceAsync` s `actorIsAdmin: true`. Pravidla služby platí beze změny: unikátnost účasti (existující se nedotkne), platnost slotu podle rozpisu, týdenní limit flexibilních bossů, odmítnutí skrytého čtenáře.
- Výsledek po událostech: kolik hráčů nově zapsáno, kolik už účast mělo, a výjimky (např. „Mayu: Queen Ant – týdenní limit“). Rozlišení „nově“ vs. „už měl“ si builder zjistí, `SetAttendanceAsync` dnes vrací `Ok` v obou případech.
- Zápis proběhne **přesně jednou** na report: druhé kliknutí na Zapsat (nebo opakované doručení stejné interakce) nic nezapíše a ukáže stejný výsledek.
- Zapisuje se přesně to, co uživatel vidí ve zprávě (uložený stav reportu), ne nově přepočítaný předvýběr.
- Report vyprší po 15 minutách (platnost interakčního tokenu Discordu). Po vypršení tlačítka nic nezapíšou a bot vyzve k novému `/report`.

### Kdo zapisuje (identita a audit)

- Zápis provádí aplikace jako **technická identita bota** s adminskými právy k účasti. Ta se nikdy nepřihlásí do webu, není ve statistikách, v žebříčku, v Admin → Hráči ani v žádném výběru hráčů a nemá roli Reader (`SetAttendanceAsync` by zápis odmítl). Konkrétní podobu (technický `AppUser`, nebo jen pevné ID v `Attendance.CreatedByUserId`, které nemá FK) volí builder.
- Každý report se eviduje v DB: kdo ho poslal (Discord user ID a jméno), server a kanál, čas, přečtené nicky, výsledek párování, zvolené datum a události, stav (čeká / zapsáno / zrušeno / vypršelo / chyba) a souhrn výsledku. Účasti vzniklé reportem jdou dohledat k reportu (vazba nebo `CreatedByUserId` + čas, volí builder).

## Oprávnění a bezpečnost

- **Ověření podpisu**: každý požadavek na endpoint se ověří podpisem Ed25519 (hlavičky `X-Signature-Ed25519`, `X-Signature-Timestamp`) proti veřejnému klíči aplikace z konfigurace. Nepodepsaný nebo špatně podepsaný požadavek dostane 401. Endpoint je jinak anonymní (výjimka z výchozí autorizace „přihlášený uživatel“, stejně jako `/health*`).
- **Jen náš server**: interakce z jiného `guild_id` než nakonfigurovaného (nebo z DM) se odmítne zprávou, nic se nezpracuje.
- **Kdo smí příkaz použít**: příkaz se registruje jako guild command s `default_member_permissions: "0"`, takže ho na začátku vidí jen správci serveru. Správce pak v *Nastavení serveru → Integrace → (aplikace) → Oprávnění příkazů* povolí konkrétní role a kanály. Aplikace oprávnění sama nekontroluje (Discord ho vynucuje před odesláním interakce).
- **Pokračování reportu** (výběry, tlačítka) smí jen autor reportu: interakce od jiného Discord uživatele se odmítne. Ephemeral zprávu stejně vidí jen autor.
- **Stažení obrázku**: jen z URL přílohy v ověřené interakci, jen z domén Discord CDN, jen typy `image/png`, `image/jpeg`, `image/webp`, s limitem velikosti (např. 10 MB) a timeoutem.
- **Tajemství** (veřejný klíč a token Discord aplikace, klíč Claude API) jsou jen v konfiguraci, v Azure v Key Vaultu; nikdy v repu, logu ani zprávě.
- Funkce je **vypnutá**, dokud konfigurace není kompletní: endpoint vrací 404 a nic se neregistruje. Aplikace bez Discord konfigurace (lokální vývoj, testy, smoke test) běží jako dnes.

## Technické řešení (rozhodnuto)

- **Bot běží uvnitř `L2Points.Web`**, žádná nová služba ani infrastruktura. Discord volá HTTP endpoint aplikace (*Interactions Endpoint URL*), např. `https://ca-l2points.graytree-049f5bba.polandcentral.azurecontainerapps.io/discord/interactions`. Container App má pevně 1 repliku (`minReplicas`/`maxReplicas` 1), endpoint je tedy vždy dostupný.
- **Interakce**: PING → PONG; `/report` → odložená ephemeral odpověď (typ 5 s příznakem ephemeral), zpracování na pozadí, úprava původní zprávy přes webhook (`PATCH /webhooks/{application_id}/{token}/messages/@original`); výběry a tlačítka → aktualizace zprávy (typ 7, případně odložená typ 6); veřejné shrnutí jako follow-up zpráva bez příznaku ephemeral.
- **Stav reportu mezi interakcemi** je v DB (tabulka reportů výše), `custom_id` komponent nese jen ID reportu a typ akce. Platí to i po restartu aplikace.
- **Zpracování na pozadí** nesmí blokovat požadavek ani web: fronta v procesu (např. `Channel` + `BackgroundService`) nebo jiný mechanismus dle buildera; chyba (Claude API nedostupné, timeout, nečitelný screen) skončí srozumitelnou zprávou uživateli a stavem „chyba“ v DB, ne výjimkou v logu bez odezvy.
- **Čtení screenu: Claude API** přes oficiální C# SDK (`Anthropic`). Model je v konfiguraci, výchozí `claude-haiku-5-5`; přepnutí na `claude-sonnet-5-5` je jen změna konfigurace. Volání je za rozhraním, aby testy běžely bez sítě s falešnou implementací.
- **Registrace příkazu**: idempotentní registrace guild commandu `/report` (bulk overwrite) přes Discord REST API s tokenem bota. Kdy se spouští (při startu aplikace, nebo jednorázový skript), volí builder; nesmí ale bránit startu aplikace, když Discord neodpovídá.
- **Doménová logika** (párování nicků, nabídka a předvýběr epiců) je čistá logika v `L2Points.Domain` s TDD testy. Služba reportu je v `L2Points.Infrastructure/Services` a volá `AttendanceService`. Endpoint, ověření podpisu a Discord REST klient patří do webu (nebo Infrastructure, dle buildera).
- Texty v Discordu jsou česky, časy v Europe/Prague, data ve formátu `d. M.` jako ve webu.

### Konfigurace

Nové klíče (env proměnné):

| Klíč | Obsah | Tajemství |
|---|---|---|
| `Discord__ApplicationId` | ID Discord aplikace | ne |
| `Discord__PublicKey` | veřejný klíč pro ověření podpisu | ne |
| `Discord__GuildId` | ID našeho Discord serveru | ne |
| `Discord__BotToken` | token bota (registrace příkazu) | ano |
| `Anthropic__ApiKey` | klíč Claude API | ano |
| `Anthropic__Model` | volitelné, výchozí `claude-haiku-5-5` | ne |

V Azure: tajemství v Key Vaultu, Container App na ně odkazuje přes `secretRef` s Key Vault URI bez verze jako dnes (`main.bicep`). Hodnoty zadává uživatel ručně (postup v `docs/deployment.md`), `deploy.ps1` je nepřepisuje. Netajné hodnoty mohou být parametry Bicepu.

### Ruční kroky uživatele (do `docs/deployment.md`)

1. Discord Developer Portal: založit aplikaci, přidat bota, zkopírovat Application ID, Public Key a Bot Token.
2. console.anthropic.com: založit API klíč, nahrát kredit, nastavit měsíční limit útraty.
3. Uložit tajemství do Key Vaultu a nasadit (`deploy.ps1`).
4. V Developer Portalu nastavit *Interactions Endpoint URL* na adresu endpointu (Discord ji při uložení ověří PINGem, aplikace už musí běžet s konfigurací).
5. Pozvat aplikaci na server (scope `applications.commands`, případně `bot`).
6. V Nastavení serveru → Integrace povolit `/report` vybraným rolím a kanálům.

## Náklady

Odhad pro 10 screenů denně (~300 měsíčně), celá obrazovka 1920×1080 (~2 800 tokenů obrázek + ~1 500 instrukce a seznam nicků, ~500 výstup):

| Model | 1 screen | měsíc |
|---|---|---|
| Claude Haiku 5.5 | ~$0,0007 | ~$0,20 |
| Claude Sonnet 5.5 | ~$0,014 | ~$4 |

Oříznutý screen (jen okno) je zhruba o polovinu levnější. Azure náklady se nemění.

## Testy

- Domain (TDD): párování nicků (přesná shoda, velikost písmen, zaměnitelné znaky, vzdálenost, remíza, jeden hráč max. jednou), nabídka epiců pro datum (pevné výskyty, Antharas/Valakas podle kotvy, flexibilní, řazení), předvýběr (okno 6 h, ranní vs. večerní blok, už zapsané, výskyty bez času, report po půlnoci za večerní blok včetně výchozího data, jiné datum).
- Infrastructure (SQLite in-memory, falešné čtení screenu): celý průběh reportu – vytvoření, výběr, zápis přesně jednou, výsledky (nově / už měl / týdenní limit / skrytý čtenář), vypršení, zrušení, cizí uživatel.
- Web: ověření podpisu (platný, neplatný, chybějící, jiný timestamp) se známým párem klíčů; PING; odmítnutí jiného serveru; endpoint vypnutý bez konfigurace.
- Smoke test v CI běží bez Discord konfigurace a nesmí se zhoršit.

## Na builderovi

- Knihovny: Ed25519 (vestavěná podpora v .NET 10, nebo NSec/BouncyCastle), Discord REST (vlastní `HttpClient`, nebo `Discord.Net.Rest`), Anthropic C# SDK.
- Schéma tabulky reportů a jejich vazba na účasti, technická identita bota.
- Přesné texty zpráv, prompt pro model, sada zaměnitelných znaků a práh vzdálenosti.
- Rozložení komponent ve zprávě (Discord dovoluje max. 5 řádků komponent: datum, epicy, nejisté shody, tlačítka).
- Kdy a jak se registruje příkaz.
