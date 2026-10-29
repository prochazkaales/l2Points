# Nasazení do Azure

Aplikace běží v Azure Container Apps s databází Azure SQL. Infrastruktura je popsaná v `infra/main.bicep`, zřizuje ji ručně skript `infra/deploy.ps1`. Nové verze aplikace nasazuje GitHub Actions (`.github/workflows/ci-cd.yml`) po pushi na `main`. Návrh a zdůvodnění rozhodnutí: [docs/superpowers/specs/2026-10-29-azure-deployment-design.md](superpowers/specs/2026-10-29-azure-deployment-design.md).

> Postupy v tomto dokumentu popisují, co dělá kód v repu. První reálné zřízení v Azure a první běh workflow na GitHubu zatím proběhnout nemusely, takže příkazy na obnovu a správu (zálohy, firewall) berte jako návod, který je potřeba při prvním použití ověřit.

## Architektura

Všechno leží v jedné resource group `rg-l2points`, výchozí region je Germany West Central (`germanywestcentral`). Názvy s příponou `<suffix>` mají šest znaků z `uniqueString` resource group, jsou tedy stabilní, dokud resource group existuje.

| Prostředek | Název | Poznámka |
|---|---|---|
| Container App | `ca-l2points` | image z `ghcr.io/prochazkaales/l2points`, 0,25 vCPU / 0,5 GiB, právě 1 replika (bez uspávání a škálování), externí HTTPS ingress na port 8080, režim jedné revize |
| Container Apps environment | `cae-l2points` | consumption, bez VNet, logy do Log Analytics |
| Azure SQL server | `sql-l2points-<suffix>` | TLS 1.2+, smíšená autentizace: SQL login `l2pointsadmin` s náhodným heslem (používá ho aplikace) a Entra admin (účet, který spustil `deploy.ps1`); firewall pravidlo „Allow Azure services“ |
| Azure SQL databáze | `sqldb-l2points` | Basic, 5 DTU, 2 GB, lokálně redundantní zálohy, point-in-time obnova 7 dní |
| Key Vault | `kv-l2points-<suffix>` | RBAC režim; tajemství `sql-connection-string` a `admin-password`, klíč `dataprotection` pro šifrování Data Protection klíčů |
| Log Analytics | `log-l2points` | retence 30 dní, denní strop ingestu 0,1 GB |
| Identita aplikace | `id-l2points-app` | user-assigned; role Key Vault Secrets User a Key Vault Crypto User na Key Vaultu |
| Identita GitHub Actions | `id-l2points-github` | federované přihlášení (OIDC, žádný klientský secret) jen pro `repo:prochazkaales/l2Points:ref:refs/heads/main`; role Contributor jen na `rg-l2points` |
| Budget | `budget-l2points` | 20 USD měsíčně, e-mail při 80 % a 100 % skutečné útraty a při prognóze 100 % |

Aplikace čte hesla z Key Vaultu přes svou identitu (Container Apps secrets odkazují na tajemství bez verze). Do repa ani do logů Actions se žádné tajemství nedostane.

### Konfigurace aplikace v Azure

Nastavuje ji `main.bicep`, ručně se nic nenastavuje.

| Proměnná | Význam |
|---|---|
| `ConnectionStrings__Default` | connection string do Azure SQL (secret z Key Vaultu) |
| `Admin__UserName`, `Admin__Nick`, `Admin__Password` | super admin, kterého aplikace při startu založí (heslo je secret z Key Vaultu) |
| `DataProtection__KeyVaultKeyUri` | klíč v Key Vaultu, kterým se šifrují Data Protection klíče uložené v DB. Bez něj se klíče ukládají nešifrované a aplikace při startu zaloguje varování |
| `ForwardedHeaders__TrustAllProxies=true` | viz níže |
| `AZURE_CLIENT_ID` | klient identity aplikace, aby `DefaultAzureCredential` použil správnou user-assigned identitu |

**`ForwardedHeaders__TrustAllProxies`** je potřeba vždy, když aplikace běží za reverzní proxy (Azure ingress, nginx apod.). Bez ní ASP.NET Core důvěřuje jen loopbacku, hlavičky `X-Forwarded-For` a `X-Forwarded-Proto` ignoruje a aplikace neví, že uživatel přišel přes HTTPS. IP ingressu v Container Apps není pevná, proto se důvěřuje všem proxy (`ForwardLimit` zůstává 1). Je to bezpečné jen proto, že ingress je jediná cesta ke kontejneru. Lokálně a v `docker-compose.yml` proxy není a proměnná se nenastavuje.

### Health endpointy a start aplikace

- `/health/live` – bez kontrol, jen že proces běží (liveness a startup probe).
- `/health/ready` – s kontrolou databáze (readiness probe a kontroly v CI). Selhání jen vyřadí repliku z provozu, nerestartuje ji.
- `/health` – alias `/health/ready`.

Migrace a seed běží před spuštěním HTTP serveru. Při startu je serializuje aplikační zámek na SQL Serveru (`StartupLock`, čekání nejvýš 3 minuty), takže se při překryvu revizí nepřetahují dvě instance. `StartupLock` navíc vytvoří prázdnou databázi, pokud na serveru ještě neexistuje. Startup probe proto dává aplikaci zhruba 9 minut (30 s + 10 × 50 s). Stará revize mezitím dál obsluhuje provoz, takže pomalý start nové verze hráče neodstaví.

## První zřízení krok za krokem

Co je potřeba: účet s rolí Owner (nebo ekvivalentem, skript vytváří role assignmenty) v předplatném, Azure CLI (`az`), PowerShell 7 (`pwsh`) a Bicep (Azure CLI si ho doinstaluje samo).

1. **Zveřejnit repo.** Na GitHubu v Settings > General > Danger Zone > Change visibility přepnout `prochazkaales/l2Points` na veřejné. Předtím zkontrolovat, že v historii nejsou tajemství. Veřejné repo je nutné pro bezplatný veřejný balíček v GHCR a pro stahování image bez přihlašovacích údajů.
2. **První push.** Vzdálené repo má zatím jen úvodní commit s README, lokální historie ho při prvním pushi nahradí (force push, dělá se jen jednou a jen po vědomém souhlasu):
   ```bash
   git remote add origin https://github.com/prochazkaales/l2Points.git
   git push --force -u origin main
   ```
   Workflow `CI/CD` na `main` sestaví a otestuje řešení, sestaví image, projede ho smoke testem (`scripts/smoke-test.sh`) a pushne `ghcr.io/prochazkaales/l2points:sha-<commit>` i `:latest`. Job `deploy` se přeskočí (zelený), dokud neexistují GitHub variables z kroku 5. Průběh je na záložce Actions.
3. **Zveřejnit balíček v GHCR.** Po prvním pushi je balíček soukromý. Na GitHubu: profil > Packages > `l2points` > Package settings > Change visibility > Public. Container App pak image stahuje bez přihlašovacích údajů. `deploy.ps1` to při prvním zřízení anonymně ověří a pokud balíček není veřejný, skončí s návodem dřív, než nasadí šablonu. Do té doby ale už zaregistroval resource providery a vytvořil prázdnou resource group, jiné prostředky nevzniknou.
4. **Zřídit Azure.** V terminálu:
   ```powershell
   az login
   pwsh ./infra/deploy.ps1 -SubscriptionId <subscription-id>
   ```
   `az login` musí proběhnout uživatelským účtem tenantu předplatného, protože se z něj stane Entra admin SQL serveru (`az ad signed-in-user show`). Skript se zeptá na login a nick super admina (login ať není `admin`, to je jméno vývojového účtu), e-mail pro upozornění na útratu a dvakrát na heslo super admina (aspoň 8 znaků, číslice a malé písmeno). Login, nick a e-mail lze předat i parametry `-AdminUserName`, `-AdminNick`, `-BudgetEmail`. Heslo se zadává jen interaktivně a nikdy se nevypisuje. Skript:
   - přepne CLI do zadaného předplatného a ověří to,
   - zaregistruje resource providery (`Microsoft.App`, `Microsoft.Sql`, `Microsoft.KeyVault`, `Microsoft.OperationalInsights`, `Microsoft.ManagedIdentity`, `Microsoft.Consumption`), první registrace může trvat minuty,
   - vytvoří resource group (`-ResourceGroup`, výchozí `rg-l2points`; `-Location`, výchozí `germanywestcentral`),
   - vygeneruje náhodné heslo SQL admina a spolu s heslem super admina ho uloží do Key Vaultu,
   - nasadí `main.bicep` s image `ghcr.io/prochazkaales/l2points:latest` a založí budget. Pokud první nasazení selže (typicky kvůli zpoždění přidělení rolí k Key Vaultu), po dvou minutách ho jednou zopakuje se stejnými parametry.

   Nasazení trvá několik minut a první start aplikace spouští všechny migrace, což na SQL Basic zabere další minuty. Na konci skript vypíše `App URL` a tři hodnoty pro GitHub.
5. **Nastavit GitHub variables.** Na GitHubu v Settings > Secrets and variables > Actions > záložka Variables (ne Secrets) vytvořit `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` a `AZURE_SUBSCRIPTION_ID` s hodnotami z výstupu skriptu. Nejsou tajné, ale do repa se nepíšou.
6. **Spustit první nasazení.** Další push na `main` (nebo Re-run all jobs posledního běhu na Actions) nasadí image `sha-<commit>`. Od teď každý push na `main` nasazuje sám.
7. **Ověřit.** Otevřít `App URL`, `/health/ready` musí vrátit 200 a super admin se musí přihlásit loginem a heslem ze kroku 4. Přihlášení musí přežít další nasazení (to ověřuje, že Data Protection klíče v DB šifruje Key Vault a po restartu se čtou zpět).

### Region a náhradní region

Pokud předplatné v Germany West Central odmítne vytvořit SQL server nebo jiný prostředek, dá se zvolit jiný region (`-Location westeurope`), ale **jen na čistém prostředí**. Resource group má region pevný a šablona ho použije pro všechny prostředky, takže u už existující skupiny se `-Location` ignoruje (skript jen varuje). Pokud nasazení selhalo až poté, co vznikla resource group a Key Vault:

1. smazat resource group: `az group delete --name rg-l2points`,
2. purge smazaného Key Vaultu (viz Smazání prostředí), jinak nový Key Vault stejného jména narazí na soft-deleted vault,
3. spustit znovu: `pwsh ./infra/deploy.ps1 -SubscriptionId <subscription-id> -Location westeurope`.

## Běžné nasazení

Push na `main` spustí workflow `CI/CD`:

1. `build-test` – `dotnet build` a `dotnet test` (běží na každém pushi a pull requestu, na všech větvích).
2. `image` – jen push na `main`: sestaví image, projede ho smoke testem proti SQL Serveru v kontejneru (health live/ready, přihlašovací stránka, ikona šperku s rozlišením velikosti písmen, tzdata a ICU, žádné chyby v logu) a teprve pak ho pushne do GHCR. Nasazuje se přesně ten image, který prošel testem.
3. `deploy` – přihlásí se do Azure přes OIDC, `az containerapp update` nastaví image `sha-<commit>`, počká, až nejnovější revize běží a je zdravá s očekávaným image, a nakonec čeká na `200` z `/health/ready`. Pokud nejsou nastavené GitHub variables, job se přeskočí.

Při přepnutí revize se otevřené Blazor circuity odpojí a prohlížeč nabídne obnovení. Přihlášení zůstane. Pokud nasazení selže, stará revize dál obsluhuje provoz a workflow je červený.

**Vrácení verze:** revert commitu a push na `main`, nebo rychleji ručně
```bash
az containerapp update -g rg-l2points -n ca-l2points --image ghcr.io/prochazkaales/l2points:sha-<starsi-commit>
```
Migrace databáze se tím nevrací; starší kód musí s novějším schématem fungovat, nebo je potřeba obnova ze zálohy.

## Změna infrastruktury

Upravit `infra/main.bicep` a znovu spustit `pwsh ./infra/deploy.ps1 -SubscriptionId <subscription-id>`. Skript pozná, že `ca-l2points` už existuje, a spustí aktualizaci:

- **zachová:** hesla (SQL i super admina, v Key Vaultu zůstanou), budget, aktuální image aplikace, login a nick super admina (parametry `-AdminUserName`, `-AdminNick` a `-BudgetEmail` se ignorují s varováním), Data Protection klíč (znovu se nevytváří),
- **změní:** vše ostatní podle šablony.

Entra adminem SQL serveru se stane uživatel, který skript právě spustil. Pokud ho spustí někdo jiný než dřív, admin se přepíše.

Rozpočet se po zřízení mění v portálu (Cost Management > Budgets), skript ho při aktualizaci nechává být. Stejný skript lze spustit i opakovaně bez změny šablony, nic se nezmění.

## Heslo super admina

Heslo se mění v aplikaci na stránce `/profile`. Heslo v Key Vaultu (`admin-password`) se po prvním startu už nepoužívá: `IdentitySeeder` existujícímu účtu heslo nikdy nemění. Tajemství v Key Vaultu proto s časem zastará a nedá se z něj heslo zjistit. Neodstraňovat ho, na tajemství odkazuje Container App a bez něj by revize nenastartovala. Ostatní admini heslo super admina resetovat nesmějí, zapomenuté heslo tedy v aplikaci vyřešit nejde.

### Varování: překlep v `Admin__UserName`

`Admin__UserName` je login super admina, kterého aplikace při každém startu udržuje. Pokud se zadá jiný login (typicky překlep při prvním zřízení, nebo změna hodnoty později) a je k němu nastavené heslo, aplikace při dalším startu **založí nového super admina** s tímto loginem a původnímu účtu **odebere roli `SuperAdmin`** (role Admin mu zůstane). Původní účet tak nepřijde o data ani o přihlášení, ale přestane být super adminem a jeho práva (správa adminů, ochrana před deaktivací) přejdou na nový účet. Před změnou této hodnoty si proto ověřte login, který zadáváte. `deploy.ps1` login při aktualizaci nemění (bere ho z běžící aplikace), takže riziko je hlavně při prvním zřízení a při ručním zásahu do konfigurace Container App.

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
Logy se drží 30 dní. Denní strop ingestu je 0,1 GB; po jeho vyčerpání se logy do konce dne neukládají (kvůli kreditu předplatného).

Stav revizí: `az containerapp revision list -g rg-l2points -n ca-l2points -o table`.

## Obnova databáze ze zálohy

Azure SQL Basic zálohuje automaticky a dovoluje obnovu k libovolnému okamžiku za posledních 7 dní (point-in-time restore, v ceně). Obnova vytvoří novou databázi vedle původní:

```bash
az sql db restore -g rg-l2points -s <sql-server-name> -n sqldb-l2points \
  --dest-name sqldb-l2points-restored --time "2026-11-05T10:00:00Z"    # čas v UTC
```

Pak je potřeba obnovenou databázi podstrčit aplikaci. Connection string v Key Vaultu míří na jméno `sqldb-l2points`, nejjednodušší je tedy databáze prohodit přejmenováním a restartovat aplikaci, aby zahodila navázaná spojení:

```bash
az sql db rename -g rg-l2points -s <sql-server-name> -n sqldb-l2points --new-name sqldb-l2points-old
az sql db rename -g rg-l2points -s <sql-server-name> -n sqldb-l2points-restored --new-name sqldb-l2points
az containerapp revision restart -g rg-l2points -n ca-l2points --revision <revision-name>
```

Název revize je v `az containerapp revision list`. Po ověření aplikace smazat `sqldb-l2points-old`. Obnovená databáze obsahuje i Data Protection klíče, které šifruje stejný klíč v Key Vaultu, takže přihlášení zůstanou platná. Zkontrolovat, že obnovená databáze má tarif Basic (jinak by se účtovala vyšší sazba), případně ji přepnout v portálu.

## Náklady a budget

Orientační odhad je 10 až 20 USD měsíčně (Container App v idle sazbě po odečtení měsíčního free grantu, SQL Basic přibližně 5 USD, Key Vault a Log Analytics pod 1 USD). Odhad je potřeba ověřit v Cost Management po prvním měsíci provozu. Pojistky:

- budget `budget-l2points` na resource group, 20 USD měsíčně, e-mail při 80 % a 100 % skutečné útraty a při prognóze 100 % (e-mail se zadává při prvním zřízení a v repu není),
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
| `azure/login` v Actions odmítne token | Federované přihlášení vyžaduje přesný subject `repo:prochazkaales/l2Points:ref:refs/heads/main`. Zkontrolovat velikost písmen názvu repa a že workflow běží na větvi `main` |
| Krok „Wait for the new revision“ skončí chybou při dlouhé první migraci | Při dlouhém prvním startu může revize krátce hlásit stav Unhealthy, i když ještě běží startup probe. Pustit workflow znovu, případně zkontrolovat logy |
