# L2Points – nasazení do Azure (část 7)

Navazuje na hlavní spec `2026-10-28-l2points-design.md` (sekce 6 Nasazení) a roadmapu `docs/superpowers/plans/2026-10-28-00-roadmap.md` (Poznámky pro další části → Část 7).

## 1. Záměr

Aplikace je funkčně hotová (části 1–6) a běží jen lokálně. Cílem je, aby ji parta mohla používat z internetu: stabilní adresa, HTTPS, data v Azure SQL, a aby se každá nová verze nasadila sama po pushi na `main`, bez ručního kopírování.

Úspěch = hráči otevřou adresu z mobilu, přihlásí se a zapisují účast; autor pushne změnu a do pár minut běží nová verze; provoz se vejde do měsíčního kreditu Visual Studio předplatného s velkou rezervou.

## 2. Cíle, ne-cíle, anti-cíle

**Cíle**
- Běh v Azure Container Apps + Azure SQL Database, infrastruktura popsaná v Bicep v repu (lze kdykoli znovu vytvořit).
- CI/CD v GitHub Actions: build + testy na každý push a PR, na `main` navíc build image, push do registru a nasazení.
- Úpravy aplikace z roadmapy pro provoz v Azure (health live/ready, forwarded headers, šifrování Data Protection klíčů, bezpečný start migrací a seedu).
- První reálný `docker build` a ověření image (tzdata `Europe/Prague`, ICU `cs-CZ`, servírování ikon šperků na Linuxu).
- Dokumentace nasazení (README, CLAUDE.md, postup prvního zřízení).

**Ne-cíle**
- Vlastní doména (zatím adresa od Azure `*.azurecontainerapps.io`, doménu lze doplnit později).
- Více prostředí (staging), více replik, autoscale.
- Přenos dat z lokální DB – produkce začíná prázdnou DB, seed založí katalog a super admina.
- Odložené položky z části 6 v roadmapě (validace `PlayerAdminService`, `AdminSchedule` atd.) – zůstávají v roadmapě. Výjimka: varování o překlepu v `Admin__UserName` patří do dokumentace nasazení.
- Přihlašování přes Entra ID – hráči mají dál vlastní účty aplikace.

**Anti-cíle** (i kdyby to technicky fungovalo, je to selhání)
- Tajemství (hesla, connection string) v repu, v logu Actions nebo v chatu. Repo bude veřejné.
- Útrata, která by vyčerpala kredit předplatného a zastavila ostatní projekty v něm.
- Aplikace, která se uspává a první hráč čeká na probuzení – rozhodnuto, že běží pořád.
- Nasazení, které po restartu odhlásí všechny hráče (Data Protection klíče musí přežít redeploy – už jsou v DB).

## 3. Rozhodnutá omezení (od uživatele)

- **Předplatné:** „Předplatné sady Visual Studio Enterprise – MPN“ uživatele. Má měsíční kredit a zapnutý spending limit (po vyčerpání se prostředky zastaví, nic se neúčtuje). Účet má roli Owner. Běží v něm i jiné projekty (22 prostředků) – L2Points se jich nesmí dotknout.
- **Aplikace běží pořád** (min. 1 replika, žádné scale-to-zero).
- **Databáze Azure SQL Basic** (DTU, 5 DTU, 2 GB), běží pořád. Ne serverless free offer – průběžné zapisování a revalidace přihlášení každou minutu by free limit vyčerpaly.
- **Repo:** `https://github.com/prochazkaales/l2Points` – uživatel ho přepne na **veřejné**. Na GitHubu je zatím jen úvodní commit s jednořádkovým README; lokální historie ho při prvním pushi nahradí (force push, před ním se uživatele zeptat).
- **Registr image:** GitHub Container Registry (`ghcr.io`), zdarma díky veřejnému repu. Ne Azure Container Registry.
- **CI/CD:** GitHub Actions, nasazení po pushi na `main`.
- **Hlídání útraty:** budget na `rg-l2points` 20 USD měsíčně, e-mailové upozornění na uživatelův Gmail (adresa se zadá jako parametr `deploy.ps1`, v repu není). Upozornění při skutečné útratě 80 % a 100 % a při prognóze 100 % (moje volba prahů).
- Docker na vývojovém stroji není; image se poprvé sestaví v GitHub Actions. `gh` CLI není nainstalované, Azure CLI ano (přihlášené).

## 4. Návrh (moje volby, uživatel je neschvaloval jednotlivě)

### 4.1 Azure prostředky

Vše v nové resource group **`rg-l2points`**, region **Germany West Central** (tam jsou i ostatní projekty uživatele). Region je parametr skriptu; kdyby předplatné v GWC odmítlo vytvořit SQL server, náhradní je West Europe (vše v jednom regionu, ne SQL zvlášť).

| Prostředek | Nastavení |
|---|---|
| Log Analytics workspace | retence 30 dní, denní strop ingestu (např. 0,1 GB) kvůli kreditu |
| Container Apps environment | consumption, bez VNet |
| Container App | image z `ghcr.io/prochazkaales/l2points`, 0,25 vCPU / 0,5 GiB, min = max = 1 replika, externí ingress HTTPS na port 8080, single revision mode, system-assigned managed identity |
| Azure SQL server + databáze | Basic, smíšená autentizace: SQL login pro aplikaci (náhodné heslo) + Entra admin = účet, který spouští `deploy.ps1`; firewall „Allow Azure services“, TLS 1.2+ |
| Key Vault | RBAC režim, jeden RSA klíč pro šifrování Data Protection klíčů; identita Container App má roli *Key Vault Crypto User* |
| User-assigned managed identity pro GitHub Actions | federated credential na `repo:prochazkaales/l2Points:ref:refs/heads/main`, role *Contributor* jen na `rg-l2points` |

- Globálně unikátní názvy (SQL server, Key Vault) dostanou krátký suffix z `uniqueString(resourceGroup().id)`.
- Nutné resource providery (`Microsoft.App`, `Microsoft.Sql`, `Microsoft.KeyVault`) zatím nejsou registrované – zřizovací skript je zaregistruje.
- Jedna replika = Blazor Server circuit nepotřebuje sticky sessions. Při nasazení nové revize se otevřené circuity odpojí a Blazor nabídne obnovení – akceptovatelné.
- Odhad nákladů (ověřit při nasazení podle ceníku): Container App převážně v idle sazbě po odečtení měsíčního free grantu ~5–15 USD, SQL Basic ~5 USD, Key Vault a Log Analytics pod 1 USD. Celkem cca 10–20 USD měsíčně.
- Zálohy: Azure SQL Basic má v ceně 7denní point-in-time restore, nic dalšího se nezřizuje.
- Přístup autora k produkční DB: přes Entra účet (Query editor v portálu nebo SSMS/Azure Data Studio s dočasným firewall pravidlem na vlastní IP, které portál nabídne). Heslo SQL loginu aplikace člověk nepotřebuje.
- Hlídání útraty: Azure budget na `rg-l2points` s e-mailovým upozorněním (částka a e-mail jsou parametry skriptu, v repu nejsou). Hodnoty viz sekce 3.

### 4.2 Tajemství a konfigurace v Azure

Container App dostane env proměnné, citlivé jako Container Apps secrets:
- `ConnectionStrings__Default` (secret) – SQL auth, `Encrypt=True`.
- `Admin__UserName`, `Admin__Nick`, `Admin__Password` (heslo jako secret) – super admin z konfigurace (viz CLAUDE.md, `IdentitySeeder`).
- `DataProtection__KeyVaultKeyUri` – URI klíče v Key Vaultu (viz 4.4).
- `ForwardedHeaders__TrustAllProxies=true` (viz 4.5).
- `ASPNETCORE_ENVIRONMENT=Production` (výchozí).

**Kdo tajemství zadává:** Claude hesla nezadává ani nevidí. První zřízení spouští uživatel ve svém terminálu skriptem `infra/deploy.ps1`:
- heslo SQL admina skript vygeneruje náhodně a předá rovnou do Bicep (nikde se nevypisuje; aplikace ho má jen v connection stringu),
- heslo super admina si skript vyžádá interaktivně (`Read-Host -AsSecureString`),
- login a nick super admina jsou povinné parametry bez výchozí hodnoty (produkční login nemá být `admin` jako dev účet).
- ID předplatného je povinný parametr; skript ověří, že CLI je přepnuté do něj. V repu nejsou ID předplatného, tenantu ani e-maily.

Opakované spuštění skriptu (změna infrastruktury) nesmí měnit existující hesla ani přepsat aktuální image na placeholder – skript/Bicep to musí ošetřit (např. hesla povinná jen při prvním zřízení nebo zachovaná z existujících secrets, image předávaná jako parametr s aktuální hodnotou). Přesný mechanismus nechávám builderovi.

### 4.3 CI/CD (GitHub Actions)

Jeden workflow (`.github/workflows/ci-cd.yml`):
1. **build-test** – na každý push a PR: `dotnet build` + `dotnet test` (SDK podle `global.json`).
2. **image** – jen push na `main`, po úspěšném build-test: `docker build`, push do `ghcr.io/prochazkaales/l2points` s tagy `sha-<commit>` a `latest` (přihlášení přes `GITHUB_TOKEN`, `packages: write`).
3. **smoke** – ověření image před nasazením: spustit image proti SQL Serveru v kontejneru (service container nebo `docker compose`), počkat na `/health/ready`, stáhnout jednu ikonu šperku (`/images/jewels/{Code}.png`) a ověřit, že v image je `Europe/Prague` a ICU. Konkrétní podobu nechávám builderovi; cílem je, aby nefunkční image nikdy nedošla do Azure.
4. **deploy** – přihlášení do Azure přes OIDC (`azure/login`, user-assigned identity z 4.1, žádný klientský secret), `az containerapp update --image ghcr.io/...:sha-<commit>`, potom čekání na ready novou revizi.

- Infrastrukturu (Bicep) workflow **nemění** – mění se jen ručně skriptem. CI tak potřebuje jen Contributor na `rg-l2points`.
- GitHub repository variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` (nejsou tajné) nastaví uživatel ručně ve webu GitHubu podle výstupu `deploy.ps1` (bez `gh` CLI).
- Balíček v GHCR je po prvním pushi soukromý; uživatel ho jednorázově přepne na veřejný (Container App pak stahuje bez přihlašovacích údajů). Pokud by se ukázalo, že to nejde, alternativa je registry credential s PAT `read:packages` jako secret Container App.
- Pořadí prvního nasazení: push → workflow sestaví a otestuje image (deploy selže/přeskočí, dokud není Azure) → uživatel zveřejní balíček a spustí `deploy.ps1` (Container App vytvoří už s image `latest`) → nastaví GitHub variables → další push nasazuje automaticky. Builder může pořadí upravit, ale musí být popsané v dokumentaci krok za krokem.

### 4.4 Úpravy aplikace

**Health live/ready**
- `/health/live` – bez kontrol (proces běží). Liveness a startup probe.
- `/health/ready` – s kontrolou DB (`AddDbContextCheck<AppDbContext>`). Readiness probe.
- `/health` zůstává jako alias ready (dokumentace a docker-compose ho zmiňují).
- Migrace a seed běží před `app.Run()`, takže HTTP server do jejich dokončení neposlouchá – startup probe musí mít dost času (SQL Basic, první migrace na prázdné DB).

**Data Protection – šifrování klíčů**
- Když je nastavené `DataProtection__KeyVaultKeyUri`, klíče v DB se šifrují přes Azure Key Vault (`ProtectKeysWithAzureKeyVault`, `DefaultAzureCredential` → managed identity Container App).
- Když není (lokální vývoj, docker-compose), zůstává současné chování a aplikace zaloguje varování.
- Produkční DB začíná prázdná, žádné stávající nešifrované klíče se nemigrují.

**Forwarded headers**
- Dnes se důvěřuje všem proxy natvrdo. Nově podle konfigurace: výchozí chování ASP.NET Core (jen loopback), `ForwardedHeaders__TrustAllProxies=true` vymaže `KnownProxies`/`KnownIPNetworks` (v Azure je ingress jediná cesta ke kontejneru a jeho IP není pevná). `ForwardLimit` zůstává 1.
- Pokud builder najde spolehlivý pevný rozsah ingressu Container Apps bez VNet, může ho použít místo trust-all.

**Bezpečný start více instancí**
- `DatabaseInitializer.MigrateAndSeedAsync` obalit aplikačním zámkem na SQL Serveru (`sp_getapplock`, session lock s timeoutem), aby migrace a oba seedery (`IdentitySeeder`, `ReferenceDataSeeder` – unikátní `Activities.Code`, `JewelTypes.Code`, role) nikdy neběžely souběžně. Týká se překryvu revizí při nasazení i budoucího škálování.
- Na SQLite (testy) se zámek přeskočí.

**Dockerfile**
- Ověřit při prvním buildu v Actions (TODO komentář v Dockerfile vyřešit). Image má být non-root (už je), port 8080.

### 4.5 Dokumentace

- `docs/deployment.md` (nebo sekce README): architektura, první zřízení krok za krokem (zveřejnění repa, force push, `az login`, `deploy.ps1`, GitHub variables, zveřejnění balíčku), běžné nasazení, jak změnit heslo super admina, jak se podívat do logů, jak obnovit DB ze zálohy, náklady.
- Varování: překlep v `Admin__UserName` se zadaným heslem založí nového super admina a původnímu účtu roli `SuperAdmin` odebere (role Admin mu zůstane).
- CLAUDE.md: stav (část 7 hotová), nové soubory (`infra/`, `.github/workflows/`), health endpointy, konfigurace `DataProtection__KeyVaultKeyUri` a `ForwardedHeaders__TrustAllProxies`, odstranit vyřešené položky ze „Známé TODO“.
- Roadmapa: část 7 označit jako hotovou, nové odložené položky do poznámek.

## 5. Nechává se na builderovi

- Struktura Bicep (moduly vs. jeden soubor), přesné názvy prostředků v rámci konvence `<typ>-l2points`.
- Přesné hodnoty probe (intervaly, timeouty), denní strop Log Analytics.
- Podoba smoke testu v CI a jestli použít `docker compose` nebo service containers.
- Mechanismus zachování hesel a image při opakovaném spuštění `deploy.ps1`.
- Verze NuGet balíčků (`Azure.Extensions.AspNetCore.DataProtection.Keys`, `Azure.Identity`) a GitHub Actions.
- Zda `deploy.ps1` registruje providery a ověřuje přihlášení do správného předplatného (měl by – výchozí předplatné CLI se může lišit).

## 6. Co musí udělat uživatel (Claude to dělat nesmí nebo nemůže)

- Přepnout repo na veřejné; povolit force push lokální historie na GitHub.
- Spustit `infra/deploy.ps1` ve svém terminálu a zadat heslo super admina.
- Nastavit GitHub repository variables a přepnout GHCR balíček na veřejný.
- Schválit vytvoření Azure prostředků (stojí kredit).
