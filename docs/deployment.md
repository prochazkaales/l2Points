# Nasazení do Azure

Aplikace běží v Azure Container Apps s databází Azure SQL. Infrastruktura je popsaná v `infra/main.bicep`, zřizuje ji ručně skript `infra/deploy.ps1`. Nové verze aplikace nasazuje GitHub Actions (`.github/workflows/ci-cd.yml`) po pushi na `main`. Návrh a zdůvodnění rozhodnutí: [docs/superpowers/specs/2026-10-29-azure-deployment-design.md](superpowers/specs/2026-10-29-azure-deployment-design.md).

> Postupy v tomto dokumentu popisují, co dělá kód v repu. První reálné zřízení v Azure a první běh workflow na GitHubu zatím proběhnout nemusely, takže příkazy na obnovu a správu (zálohy, firewall) berte jako návod, který je potřeba při prvním použití ověřit.

## Architektura

Všechno leží v jedné resource group `rg-l2points`, výchozí region je Poland Central (`polandcentral`), nejbližší region k Praze s volnou kapacitou pro Container Apps (Germany West Central v říjnu 2026 nové Container Apps prostředí odmítal kvůli nedostatku kapacity). Názvy s příponou `<suffix>` mají šest znaků z `uniqueString` resource group, jsou tedy stabilní, dokud resource group existuje.

| Prostředek | Název | Poznámka |
|---|---|---|
| Container App | `ca-l2points` | image z `ghcr.io/prochazkaales/l2points`, 0,25 vCPU / 0,5 GiB, právě 1 replika (bez uspávání a škálování), externí HTTPS ingress na port 8080, režim jedné revize |
| Container Apps environment | `cae-l2points` | consumption, bez VNet, logy do Log Analytics |
| Azure SQL server | `sql-l2points-<suffix>` | TLS 1.2+, smíšená autentizace: SQL login `l2pointsadmin` s náhodným heslem (používá ho aplikace) a Entra admin (účet, který spustil `deploy.ps1`); firewall pravidlo „Allow Azure services“ |
| Azure SQL databáze | `sqldb-l2points` | Basic, 5 DTU, 2 GB, lokálně redundantní zálohy, point-in-time obnova 7 dní |
| Key Vault | `kv-l2points-<suffix>` | RBAC režim; tajemství `sql-connection-string` a `admin-password` (a po zapnutí Discord bota `discord-bot-token` a `anthropic-api-key`), klíč `dataprotection` pro šifrování Data Protection klíčů |
| Log Analytics | `log-l2points` | retence 30 dní, denní strop ingestu 0,1 GB |
| Identita aplikace | `id-l2points-app` | user-assigned; role Key Vault Secrets User a Key Vault Crypto User na Key Vaultu |
| Identita GitHub Actions | `id-l2points-github` | federované přihlášení (OIDC, žádný klientský secret) jen pro větev `main` repa `prochazkaales/l2Points` (subject `repo:prochazkaales@142387925/l2Points@1437856239:ref:refs/heads/main`); role Contributor jen na `rg-l2points` |
| Budget | `budget-l2points` | 20 měsíčně (v měně fakturace předplatného, u předplatných Visual Studio typicky EUR nebo USD), e-mail při 80 % a 100 % skutečné útraty a při prognóze 100 % |

Aplikace čte hesla z Key Vaultu přes svou identitu (Container Apps secrets odkazují na tajemství bez verze). Do repa ani do logů Actions se žádné tajemství nedostane.

### Konfigurace aplikace v Azure

Nastavuje ji `main.bicep`, ručně se nic nenastavuje.

| Proměnná | Význam |
|---|---|
| `ConnectionStrings__Default` | connection string do Azure SQL (secret z Key Vaultu) |
| `Admin__UserName`, `Admin__Nick`, `Admin__Password` | super admin, kterého aplikace při startu založí (heslo je secret z Key Vaultu) |
| `DataProtection__KeyVaultKeyUri` | klíč v Key Vaultu, kterým se šifrují Data Protection klíče uložené v DB. Bez něj se klíče ukládají nešifrované a aplikace při startu zaloguje varování |
| `Database__CreateIfMissing=false` | aplikace nikdy sama nevytvoří chybějící databázi. Aplikace se připojuje jako admin SQL serveru, takže holé `CREATE DATABASE` by vytvořilo drahou databázi ve výchozím tarifu a vyčerpalo kredit. Chybějící `sqldb-l2points` místo toho zastaví start s jasnou chybou. Bez proměnné (lokálně, `docker-compose.yml`, smoke test) se prázdná databáze vytvoří |
| `ForwardedHeaders__TrustAllProxies=true` | viz níže |
| `Discord__ApplicationId`, `Discord__PublicKey`, `Discord__GuildId`, `Discord__BotToken`, `Anthropic__ApiKey`, `Anthropic__Model` | jen když je zapnutý Discord bot, viz [Discord bot](#discord-bot). Token bota a klíč Claude API jsou secrety z Key Vaultu, `Anthropic__Model` je volitelný |
| `AZURE_CLIENT_ID` | klient identity aplikace, aby `DefaultAzureCredential` použil správnou user-assigned identitu |

**`ForwardedHeaders__TrustAllProxies`** je potřeba vždy, když aplikace běží za reverzní proxy (Azure ingress, nginx apod.). Bez ní ASP.NET Core důvěřuje jen loopbacku, hlavičky `X-Forwarded-For` a `X-Forwarded-Proto` ignoruje a aplikace neví, že uživatel přišel přes HTTPS. IP ingressu v Container Apps není pevná, proto se důvěřuje všem proxy (`ForwardLimit` zůstává 1). Je to bezpečné jen proto, že ingress je jediná cesta ke kontejneru. Lokálně a v `docker-compose.yml` proxy není a proměnná se nenastavuje.

### Health endpointy a start aplikace

- `/health/live` – bez kontrol, jen že proces běží (liveness a startup probe).
- `/health/ready` – s kontrolou databáze (readiness probe a kontroly v CI). Selhání jen vyřadí repliku z provozu, nerestartuje ji.
- `/health` – alias `/health/ready`.

Migrace a seed běží před spuštěním HTTP serveru. Při startu je serializuje aplikační zámek na SQL Serveru (`StartupLock`, čekání nejvýš 3 minuty), takže se při překryvu revizí nepřetahují dvě instance. `StartupLock` navíc vytvoří prázdnou databázi, pokud na serveru ještě neexistuje a `Database__CreateIfMissing` není `false` (v Azure je, takže tam chybějící databáze start zastaví). Startup probe proto dává aplikaci zhruba 9 minut (30 s + 10 × 50 s). Stará revize mezitím dál obsluhuje provoz, takže pomalý start nové verze hráče neodstaví.

## První zřízení krok za krokem

Co je potřeba: účet s rolí Owner (nebo ekvivalentem, skript vytváří role assignmenty) v předplatném, Azure CLI (`az`), PowerShell 7 (`pwsh`) a Bicep (Azure CLI si ho doinstaluje samo).

1. **Zveřejnit repo.** Na GitHubu v Settings > General > Danger Zone > Change visibility přepnout `prochazkaales/l2Points` na veřejné. Předtím zkontrolovat, že v historii nejsou tajemství. Veřejné repo je nutné pro bezplatný veřejný balíček v GHCR a pro stahování image bez přihlašovacích údajů.
2. **První push.** Vzdálené repo má zatím jen úvodní commit s README, lokální historie ho při prvním pushi nahradí (force push, dělá se jen jednou a jen po vědomém souhlasu):
   ```bash
   git remote add origin https://github.com/prochazkaales/l2Points.git
   git push --force -u origin main
   ```
   Workflow `CI/CD` na `main` sestaví a otestuje řešení, sestaví image, projede ho smoke testem (`scripts/smoke-test.sh`) a pushne `ghcr.io/prochazkaales/l2points:sha-<commit>` i `:latest`. Job `deploy` se přeskočí (zelený), dokud neexistují GitHub variables z kroku 5. Průběh je na záložce Actions.
3. **Zkontrolovat, že balíček v GHCR je veřejný.** U veřejného repa převzal balíček při prvním pushi viditelnost repa a byl rovnou veřejný (ověřeno 29. 10. 2026). Pokud by byl soukromý, na GitHubu: profil > Packages > `l2points` > Package settings > Change visibility > Public. Container App pak image stahuje bez přihlašovacích údajů. `deploy.ps1` to při prvním zřízení anonymně ověří a pokud balíček není veřejný, skončí s návodem dřív, než nasadí šablonu. Do té doby ale už zaregistroval resource providery a vytvořil prázdnou resource group, jiné prostředky nevzniknou.
4. **Zřídit Azure.** V terminálu:
   ```powershell
   az login
   pwsh ./infra/deploy.ps1 -SubscriptionId <subscription-id>
   ```
   `az login` musí proběhnout uživatelským účtem tenantu předplatného, protože se z něj stane Entra admin SQL serveru (`az ad signed-in-user show`). Skript se zeptá na login a nick super admina (login ať není `admin`, to je jméno vývojového účtu), e-mail pro upozornění na útratu, vypíše login a nick k potvrzení (`y`, cokoli jiného skript ukončí) a dvakrát se zeptá na heslo super admina (aspoň 8 znaků, číslice a malé písmeno). Login, nick a e-mail lze předat i parametry `-AdminUserName`, `-AdminNick`, `-BudgetEmail`. Heslo se zadává jen interaktivně a nikdy se nevypisuje. Skript:
   - přepne CLI do zadaného předplatného a ověří to,
   - zaregistruje resource providery (`Microsoft.App`, `Microsoft.Sql`, `Microsoft.KeyVault`, `Microsoft.OperationalInsights`, `Microsoft.ManagedIdentity`, `Microsoft.Consumption`), první registrace může trvat minuty,
   - vytvoří resource group (`-ResourceGroup`, výchozí `rg-l2points`; `-Location`, výchozí `polandcentral`),
   - vygeneruje náhodné heslo SQL admina a spolu s heslem super admina ho uloží do Key Vaultu,
   - nasadí `main.bicep` s image `ghcr.io/prochazkaales/l2points:latest` a založí budget. Pokud první nasazení selže (typicky kvůli zpoždění přidělení rolí k Key Vaultu), po dvou minutách ho jednou zopakuje se stejnými parametry.

   Nasazení trvá několik minut a první start aplikace spouští všechny migrace, což na SQL Basic zabere další minuty. Na konci skript vypíše `App URL` a tři hodnoty pro GitHub.
5. **Nastavit GitHub variables.** Na GitHubu v Settings > Secrets and variables > Actions > záložka Variables (ne Secrets) vytvořit `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` a `AZURE_SUBSCRIPTION_ID` s hodnotami z výstupu skriptu. Nejsou tajné, ale do repa se nepíšou.
6. **Spustit první nasazení.** Další push na `main` (nebo Re-run all jobs posledního běhu na Actions) nasadí image `sha-<commit>`. Od teď každý push na `main` nasazuje sám.
7. **Ověřit.** Otevřít `App URL`, `/health/ready` musí vrátit 200 a super admin se musí přihlásit loginem a heslem ze kroku 4. Přihlášení musí přežít další nasazení (to ověřuje, že Data Protection klíče v DB šifruje Key Vault a po restartu se čtou zpět).

### Region a náhradní region

Pokud předplatné ve výchozím regionu odmítne vytvořit SQL server nebo jiný prostředek, dá se zvolit jiný region (třeba `-Location swedencentral`), ale **jen na čistém prostředí**. Typická chyba je `ManagedEnvironmentCapacityHeavyUsageError` / `AKSCapacityHeavyUsage` u `cae-l2points`: region dočasně nezakládá nová Container Apps prostředí a opakování nepomůže. Volnou kapacitu lze předem ověřit prázdným prostředím v dočasné resource group (prázdné nic nestojí): `az group create -n rg-probe -l <region>`, `az containerapp env create -g rg-probe -n cae-probe -l <region> --logs-destination none`, výsledek zjistí `az containerapp env show -g rg-probe -n cae-probe --query properties.provisioningState` (`Succeeded` / `Failed`), pak `az group delete -n rg-probe --yes`. Resource group má region pevný a šablona ho použije pro všechny prostředky, takže u už existující skupiny se `-Location` ignoruje (skript jen varuje). Pokud nasazení selhalo až poté, co vznikla resource group a Key Vault:

1. smazat resource group: `az group delete --name rg-l2points`,
2. purge smazaného Key Vaultu (viz Smazání prostředí), jinak nový Key Vault stejného jména narazí na soft-deleted vault,
3. spustit znovu: `pwsh ./infra/deploy.ps1 -SubscriptionId <subscription-id> -Location <region>`.

## Běžné nasazení

Push na `main` spustí workflow `CI/CD`:

1. `build-test` – `dotnet build` a `dotnet test` (běží na každém pushi a pull requestu, na všech větvích).
2. `image` – jen push na `main`: sestaví image, projede ho smoke testem proti SQL Serveru v kontejneru (health live/ready, přihlašovací stránka, ikona šperku s rozlišením velikosti písmen, `POST /discord/interactions` bez konfigurace vrací 404, tzdata a ICU, žádné chyby v logu) a teprve pak ho pushne do GHCR. Nasazuje se přesně ten image, který prošel testem.
3. `deploy` – přihlásí se do Azure přes OIDC, `az containerapp update` nastaví image `sha-<commit>`, počká, až nejnovější revize běží a je zdravá s očekávaným image, a nakonec čeká na `200` z `/health/ready`. Pokud nejsou nastavené GitHub variables, job se přeskočí.

Při přepnutí revize se otevřené Blazor circuity odpojí a prohlížeč nabídne obnovení. Přihlášení zůstane. Pokud nasazení selže, stará revize dál obsluhuje provoz a workflow je červený.

**Vrácení verze:** revert commitu a push na `main`, nebo rychleji ručně
```bash
az containerapp update -g rg-l2points -n ca-l2points --image ghcr.io/prochazkaales/l2points:sha-<starsi-commit>
```
Migrace databáze se tím nevrací; starší kód musí s novějším schématem fungovat, nebo je potřeba obnova ze zálohy.

## Změna infrastruktury

Upravit `infra/main.bicep` a znovu spustit `pwsh ./infra/deploy.ps1 -SubscriptionId <subscription-id>`. Skript pozná, že `ca-l2points` už existuje, a spustí aktualizaci:

- **zachová:** hesla (SQL i super admina, v Key Vaultu zůstanou), tajemství Discord bota (token a klíč Claude API, pokud se nepředá `-SetDiscordSecrets`), Discord hodnoty a model (čtou se z běžící aplikace), budget, aktuální image aplikace, login a nick super admina (parametry `-AdminUserName`, `-AdminNick` a `-BudgetEmail` se ignorují s varováním), Data Protection klíč (znovu se nevytváří),
- **změní:** vše ostatní podle šablony.

Entra admin SQL serveru se nastavuje jen při vytvoření serveru (vlastnost `administrators` se při aktualizaci spolehlivě neaplikuje), opakované spuštění jiným uživatelem ho tedy nepřepíše. Změna Entra admina:
```bash
az sql server ad-admin create -g rg-l2points --server-name <sql-server-name> --display-name <upn> --object-id <object-id>
```

Rozpočet se po zřízení mění v portálu (Cost Management > Budgets), skript ho při aktualizaci nechává být. Stejný skript lze spustit i opakovaně bez změny šablony, nic se nezmění.

## Discord bot

Bot `/report` čte screen party a zapisuje účast na epicích (návrh: [docs/superpowers/specs/2026-10-30-discord-report-bot-design.md](superpowers/specs/2026-10-30-discord-report-bot-design.md)). Běží uvnitř aplikace, žádná nová služba v Azure není. Discord volá endpoint `POST /discord/interactions` aplikace a na screen se aplikace dívá přes Claude API. Dokud konfigurace není kompletní (`Discord__ApplicationId`, `Discord__PublicKey`, `Discord__GuildId`, `Discord__BotToken` a `Anthropic__ApiKey`), je funkce vypnutá: endpoint vrací 404 a nic se nevolá. Aplikace bez ní (lokální vývoj, testy, smoke test v CI) běží jako dřív.

Tajemství (token bota, klíč Claude API) leží v Key Vaultu jako `discord-bot-token` a `anthropic-api-key`, Container App na ně odkazuje stejně jako na ostatní (versionless URI, identita aplikace). Veřejný klíč, Application ID a Guild ID nejsou tajné a jsou přímo v proměnných Container App.

Ruční kroky, v tomto pořadí:

1. **Discord Developer Portal** ([discord.com/developers/applications](https://discord.com/developers/applications)): New Application, záložka Bot (přidat bota, Reset Token a token zkopírovat), na General Information zkopírovat Application ID a Public Key. Guild ID získat v Discordu (Nastavení > Pokročilé > Vývojářský režim, pak pravé tlačítko na server > Kopírovat ID serveru).
2. **console.anthropic.com**: založit API klíč, nahrát kredit a v nastavení organizace nastavit měsíční limit útraty (Limits). Odhad při ~10 screenech denně je jednotky dolarů měsíčně i se Sonnetem.
3. **Nasadit konfiguraci:**
   ```powershell
   pwsh ./infra/deploy.ps1 -SubscriptionId <subscription-id> `
       -DiscordApplicationId <application-id> -DiscordPublicKey <public-key> -DiscordGuildId <guild-id> `
       -SetDiscordSecrets
   ```
   Přepínač `-SetDiscordSecrets` se zeptá na token bota a klíč Claude API (skrytý vstup), uloží je do Key Vaultu a nasadí aplikaci s novou konfigurací. Tajemství nikdy nejdou na příkazovou řádku, jen do dočasného souboru s parametry, který se po nasazení smaže. Při prvním zapnutí je přepínač povinný (jinak by revize odkazovala na neexistující tajemství a nenastartovala, skript proto skončí s chybou). Při pozdějších spuštěních skriptu se Discord hodnoty přečtou z běžící aplikace a tajemství v Key Vaultu zůstanou, takže běžná aktualizace infrastruktury bota nevypne. `-SetDiscordSecrets` je potřeba jen pro výměnu tokenu nebo klíče.
4. **Interactions Endpoint URL**: v Developer Portalu na General Information nastavit `https://ca-l2points.graytree-049f5bba.polandcentral.azurecontainerapps.io/discord/interactions` a uložit. Discord při uložení pošle PING podepsaný Ed25519, aplikace proto musí už běžet s konfigurací z kroku 3 (správný Public Key); jinak uložení selže.
5. **Pozvat aplikaci na server**: OAuth2 > URL Generator, scope `applications.commands` (příkaz se při startu aplikace zaregistruje jako guild command přes REST API s tokenem bota; registrace při selhání jen zaloguje varování a start nezastaví). Otevřít vygenerovanou adresu a vybrat náš server.
6. **Oprávnění příkazu**: příkaz je ve výchozím stavu vidět jen správcům serveru. V Discordu: Nastavení serveru > Integrace > aplikace > `/report` a povolit vybrané role a kanály.

Aplikace sama oprávnění nekontroluje, vynucuje je Discord. Interakce z jiného serveru, než je `Discord__GuildId`, a z DM se odmítnou.

**Změna modelu.** Výchozí je `claude-haiku-5-5` (levný, ~0,2 USD měsíčně). Přesnější `claude-sonnet-5-5` (~4 USD měsíčně) se zapne jedním spuštěním skriptu, které model předá do `Anthropic__Model` (ostatní Discord hodnoty skript přečte z běžící aplikace):
```powershell
pwsh ./infra/deploy.ps1 -SubscriptionId <subscription-id> -AnthropicModel claude-sonnet-5-5
```
Návrat na výchozí model: `az containerapp update -g rg-l2points -n ca-l2points --remove-env-vars Anthropic__Model`, a protože další spuštění `deploy.ps1` hodnotu čte z běžící aplikace, odebraná hodnota se nevrátí.

**Dohledání reportu.** Každý report je v tabulce `DiscordReports` (kdo ho poslal, kanál, čas, stav `Status`: 0 zpracovává se, 1 čeká, 2 zapisuje se, 3 zapsáno, 4 zrušeno, 5 vypršelo, 6 chyba; `StateJson` obsahuje přečtené nicky, párování, zvolené datum a události a výsledek). Účasti vzniklé reportem mají `CreatedByUserId = 'discord-report:<id>'`, kde `<id>` je `Id` reportu bez pomlček:
```sql
SELECT Id, DiscordUserName, CreatedAtUtc, Status, StateJson FROM DiscordReports ORDER BY CreatedAtUtc DESC;

SELECT r.Id AS ReportId, r.DiscordUserName, a.*
FROM Attendances a
JOIN DiscordReports r ON a.CreatedByUserId = 'discord-report:' + LOWER(REPLACE(CONVERT(varchar(36), r.Id), '-', ''))
ORDER BY r.CreatedAtUtc DESC;
```
Přístup do databáze viz níže. Report lze zrušit nebo zapsané účasti opravit jako každou jinou účast v aplikaci (Admin > Oprava účasti).

## Heslo super admina

Heslo se mění v aplikaci na stránce `/profile`. Heslo v Key Vaultu (`admin-password`) se po prvním startu už nepoužívá: `IdentitySeeder` existujícímu účtu heslo nikdy nemění. Tajemství v Key Vaultu proto s časem zastará a nedá se z něj heslo zjistit. Neodstraňovat ho, na tajemství odkazuje Container App a bez něj by revize nenastartovala. Ostatní admini heslo super admina resetovat nesmějí, zapomenuté heslo tedy v aplikaci vyřešit nejde.

### Varování: překlep v `Admin__UserName`

`Admin__UserName` je login super admina, kterého aplikace při každém startu udržuje. Pokud se zadá jiný login (typicky překlep při prvním zřízení, nebo změna hodnoty později) a je k němu nastavené heslo, aplikace při dalším startu **založí nového super admina** s tímto loginem a původnímu účtu **odebere roli `SuperAdmin`** (role Admin mu zůstane). Původní účet tak nepřijde o data ani o přihlášení, ale přestane být super adminem a jeho práva (správa adminů, ochrana před deaktivací) přejdou na nový účet. Před změnou této hodnoty si proto ověřte login, který zadáváte. `deploy.ps1` login při aktualizaci nemění (bere ho z běžící aplikace) a při prvním zřízení ho vypíše k potvrzení, takže riziko je hlavně při ručním zásahu do konfigurace Container App.

Login lze později změnit takto, s výše popsaným přenosem role `SuperAdmin` na nový účet (nový účet dostane heslo z `Admin__Password`, tedy z tajemství `admin-password`):
```bash
az containerapp update -g rg-l2points -n ca-l2points --set-env-vars Admin__UserName=<new-login>
```
Pozor: další spuštění `deploy.ps1` login čte z běžící aplikace, takže novou hodnotu zachová.

## Přístup do databáze

Heslo SQL loginu aplikace člověk nepotřebuje. K databázi se připojuje Entra účet, který spustil `deploy.ps1`:

- **Portál:** SQL database `sqldb-l2points` > Query editor > přihlášení Microsoft Entra. Portál nabídne přidat vlastní IP do firewallu.
- **SSMS / Azure Data Studio:** server `sql-l2points-<suffix>.database.windows.net`, ověření Microsoft Entra MFA. Z vlastní IP je potřeba firewall pravidlo:
  ```bash
  az sql server list -g rg-l2points --query "[0].name" -o tsv      # název serveru
  az sql server firewall-rule create -g rg-l2points -s <sql-server-name> -n my-ip --start-ip-address <moje-ip> --end-ip-address <moje-ip>
  az sql server firewall-rule delete -g rg-l2points -s <sql-server-name> -n my-ip     # po skončení
  ```

Do produkční databáze se zasahuje jen výjimečně. Běžná správa (hráči, aktivity, rozpis) je v aplikaci.

## Logy

```bash
az containerapp logs show -g rg-l2points -n ca-l2points --follow --tail 100
```

Starší logy jsou v Log Analytics workspace `log-l2points` (portál: workspace > Logs), například
```
ContainerAppConsoleLogs_CL
| where ContainerAppName_s == "ca-l2points"
| order by TimeGenerated desc
```
Logy se drží 30 dní. Produkce (`appsettings.Production.json`) neloguje jednotlivé SQL příkazy (EF Core `Database.Command` jen od úrovně Warning). Denní strop ingestu je 0,1 GB; po jeho vyčerpání se logy do konce dne neukládají (kvůli kreditu předplatného).

Stav revizí: `az containerapp revision list -g rg-l2points -n ca-l2points -o table`.

## Obnova databáze ze zálohy

Azure SQL Basic zálohuje automaticky a dovoluje obnovu k libovolnému okamžiku za posledních 7 dní (point-in-time restore, v ceně). Obnova vytvoří novou databázi vedle původní. Databáze `sqldb-l2points` nesmí během běhu aplikace nikdy chybět (aplikace je s `Database__CreateIfMissing=false` při startu odmítne vytvořit), proto se **neprohazují názvy přejmenováním**. Obnovuje se do nového názvu a aplikace se na něj přepne změnou connection stringu:

1. Obnovit do nové databáze:
   ```bash
   az sql db restore -g rg-l2points -s <sql-server-name> -n sqldb-l2points      --dest-name sqldb-l2points-restored --time "2026-11-05T10:00:00Z"    # čas v UTC
   ```
   Zkontrolovat, že obnovená databáze má tarif Basic (jinak by se účtovala vyšší sazba), případně ji přepnout v portálu.
2. Přečíst současný connection string, nahradit v něm `Initial Catalog=sqldb-l2points` (resp. `Database=...`) novým názvem a uložit ho jako novou verzi tajemství `sql-connection-string`:
   ```bash
   az keyvault secret show --vault-name <kv-name> --name sql-connection-string --query value -o tsv
   az keyvault secret set  --vault-name <kv-name> --name sql-connection-string --value "<upraveny-connection-string>"
   ```
   Uživatel, který to spouští, potřebuje na Key Vaultu roli Key Vault Secrets Officer (Owner předplatného ji nemá automaticky, RBAC režim vyžaduje přidělení, např. `az role assignment create --assignee <upn> --role "Key Vault Secrets Officer" --scope <kv-resource-id>`). Hodnotu connection stringu nikam nezapisovat ani nevkládat do repa, obsahuje heslo.
3. Container App čte tajemství Key Vaultu bez verze, ale změnu zachytí až nová revize. Restartovat revizi nebo vytvořit novou:
   ```bash
   az containerapp revision restart -g rg-l2points -n ca-l2points --revision <revision-name>
   ```
   Název revize je v `az containerapp revision list`. Nová revize se po startu přihlásí k obnovené databázi.
4. Ověřit aplikaci (`/health/ready`, přihlášení). Obnovená databáze obsahuje i Data Protection klíče, které šifruje stejný klíč v Key Vaultu, takže přihlášení zůstanou platná.
5. Až je vše v pořádku, původní databázi smazat: `az sql db delete -g rg-l2points -s <sql-server-name> -n sqldb-l2points`. Pozor: `main.bicep` počítá s databází `sqldb-l2points`, takže aktualizace přes `deploy.ps1` po smazání vytvoří novou prázdnou Basic databázi (stojí cca 5 USD měsíčně a nikdo ji nepoužívá). Na starý název se lze vrátit stejným postupem: smazat starou databázi, obnovit z aktuální databáze do `sqldb-l2points` (krok 1) a přepnout tajemství zpět (kroky 2 a 3). Aplikace přitom celou dobu ukazuje na existující databázi.

## Náklady a budget

Orientační odhad je 10 až 20 USD měsíčně (Container App v idle sazbě po odečtení měsíčního free grantu, SQL Basic přibližně 5 USD, Key Vault a Log Analytics pod 1 USD). Odhad je potřeba ověřit v Cost Management po prvním měsíci provozu. Pojistky:

- budget `budget-l2points` na resource group, částka 20 měsíčně v měně fakturace předplatného (nemusí to být USD), e-mail při 80 % a 100 % skutečné útraty a při prognóze 100 % (e-mail se zadává při prvním zřízení a v repu není),
- denní strop logů 0,1 GB,
- Container App běží právě v jedné replice, bez autoscale,
- předplatné má spending limit, po vyčerpání kreditu se prostředky zastaví, nic se neúčtuje navíc (zastaví se ale všechny prostředky v předplatném, ne jen L2Points).

## Smazání prostředí

```bash
az group delete --name rg-l2points
```

Tím zmizí všechna data včetně databáze. **Key Vault zůstává v soft-delete** (90 dní, purge protection se nezapíná) a jeho jméno je obsazené. Název vaultu je odvozený od resource group, takže nové zřízení se stejnou resource group má stejný název a narazilo by na smazaný vault. Před novým zřízením ho proto vyčistit:

```bash
az keyvault list-deleted --query "[].{name:name, location:properties.location}" -o table
az keyvault purge --name kv-l2points-<suffix> --location <region>
```

Po novém zřízení se vytvoří nová identita GitHub Actions, takže je potřeba aktualizovat GitHub variables `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` a `AZURE_SUBSCRIPTION_ID` podle výstupu `deploy.ps1`.

## Řešení potíží při prvním zřízení

| Příznak | Příčina a postup |
|---|---|
| `deploy.ps1` skončí hned s hláškou, že image nejde stáhnout anonymně | Balíček v GHCR není veřejný (krok 3) nebo první push ještě nedoběhl. Napravit a spustit skript znovu. Vznikla jen prázdná resource group (a registrace providerů), žádný Key Vault ani jiný prostředek. Chcete-li v tuto chvíli jiný region, nejdřív prázdnou skupinu smažte (`az group delete --name rg-l2points`), jinak skript `-Location` ignoruje a zůstane v původním regionu. Purge Key Vaultu není potřeba, žádný nevznikl |
| Skript nenajde přihlášeného Entra uživatele | `az login` bez uživatelského účtu tenantu (osobní Microsoft účet, host). Přihlásit se `az login --tenant <tenant-id>` účtem z tenantu |
| Nasazení selže na přístupu ke Key Vault tajemstvím i po automatickém opakování | Přidělení rolí se ještě nerozšířilo. Počkat pár minut a spustit skript znovu. Pokud Container App nevznikla, je to znovu první zřízení; pokud vznikla, jde o aktualizaci a opraví ji |
| Selže vytvoření budgetu | Zbytek je nasazený. Další spuštění je aktualizace a budget přeskočí, vytvořit ho ručně v portálu (Cost Management > Budgets) |
| `azure/login` v Actions odmítne token (`AADSTS700213: No matching federated identity record`) | Federované přihlášení vyžaduje přesně ten subject, který GitHub vydá; chybová hláška ho uvádí (`presented assertion subject '...'`). GitHub ho pro toto repo vydává s neměnnými ID vlastníka a repa (`repo:prochazkaales@142387925/l2Points@1437856239:ref:refs/heads/main`), ne jen `repo:prochazkaales/l2Points:...`. Hodnotu z hlášky dát do parametru `githubOidcRepository` v `main.bicep` (nebo rovnou `az identity federated-credential update -g rg-l2points --identity-name id-l2points-github -n github-main --subject '<subject>'`) a ověřit, že workflow běží na větvi `main` |
| Krok „Wait for the new revision“ skončí chybou při dlouhé první migraci | Při dlouhém prvním startu může revize krátce hlásit stav Unhealthy, i když ještě běží startup probe. Pustit workflow znovu, případně zkontrolovat logy |
