#Requires -Version 7.0
<#
.SYNOPSIS
    Provisions or updates the Azure infrastructure of L2Points (infra/main.bicep).

.DESCRIPTION
    Run it yourself in your own terminal, signed in with `az login`. It never prints passwords.

    First run (container app ca-l2points does not exist yet = initial setup):
      - asks for the super admin password (twice), generates a random SQL admin password,
      - deploys everything with the image ghcr.io/prochazkaales/l2points:latest and creates the cost budget.
      The GHCR package must already be public, otherwise the container app cannot pull the image.

    Later runs (infrastructure update):
      - keep the passwords, the budget, the current image and the current super admin login and nick
        (-AdminUserName, -AdminNick and -BudgetEmail are ignored).

    At the end it prints the app URL and the values of the GitHub repository variables for the CI deploy.

.EXAMPLE
    ./infra/deploy.ps1 -SubscriptionId <subscription-id> -AdminUserName <login> -AdminNick <nick> -BudgetEmail <e-mail>
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $SubscriptionId,

    [string] $ResourceGroup = 'rg-l2points',

    [string] $Location = 'polandcentral',

    # Required only on the first provisioning; the script asks for missing values.
    [string] $AdminUserName,
    [string] $AdminNick,
    [string] $BudgetEmail
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ContainerAppName = 'ca-l2points'
$ContainerAppApiVersion = '2024-03-01'
$InitialImageRepository = 'prochazkaales/l2points'
$InitialImage = "ghcr.io/${InitialImageRepository}:latest"
$TemplateFile = Join-Path $PSScriptRoot 'main.bicep'
$RequiredProviders = @(
    'Microsoft.App',
    'Microsoft.Sql',
    'Microsoft.KeyVault',
    'Microsoft.OperationalInsights',
    'Microsoft.ManagedIdentity',
    'Microsoft.Consumption'
)

# Runs the Azure CLI and fails on a non-zero exit code. Arguments must never contain secrets
# (passwords go only through the temporary parameter file).
# Simple functions (no param block) on purpose: every argument, including --options, lands in $args unchanged.
function Invoke-Az {
    $output = & az @args
    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI command 'az $($args[0..1] -join ' ')' failed (exit code $LASTEXITCODE)."
    }
    return $output
}

function Invoke-AzJson {
    $output = Invoke-Az @args --output json
    if (-not $output) { return $null }
    return ($output -join "`n") | ConvertFrom-Json
}

function ConvertTo-PlainText([securestring] $Value) {
    return [System.Net.NetworkCredential]::new('', $Value).Password
}

function Read-RequiredValue([string] $Current, [string] $Prompt) {
    if (-not [string]::IsNullOrWhiteSpace($Current)) { return $Current.Trim() }
    while ($true) {
        $value = Read-Host -Prompt $Prompt
        if (-not [string]::IsNullOrWhiteSpace($value)) { return $value.Trim() }
    }
}

# Same rules as the app's Identity options: at least 8 characters, a digit and a lowercase letter.
function Read-AdminPassword {
    while ($true) {
        $first = ConvertTo-PlainText (Read-Host -Prompt 'Super admin password' -AsSecureString)
        $second = ConvertTo-PlainText (Read-Host -Prompt 'Super admin password (again)' -AsSecureString)
        if ($first -cne $second) {
            Write-Warning 'The passwords do not match, try again.'
        }
        elseif ($first.Length -lt 8 -or $first -notmatch '\d' -or $first -cnotmatch '[a-z]') {
            Write-Warning 'The password needs at least 8 characters, a digit and a lowercase letter.'
        }
        else {
            return $first
        }
    }
}

# 32 random characters with upper case, lower case, digits and symbols (Azure SQL password policy).
# Only symbols that are safe inside a connection string (no ; = ' " { } or spaces).
function New-SqlPassword {
    $upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ'
    $lower = 'abcdefghijkmnopqrstuvwxyz'
    $digits = '23456789'
    $symbols = '-_.!*'
    $all = $upper + $lower + $digits + $symbols
    while ($true) {
        $chars = for ($i = 0; $i -lt 32; $i++) {
            $all[[System.Security.Cryptography.RandomNumberGenerator]::GetInt32($all.Length)]
        }
        $password = -join $chars
        if ($password -cmatch '[A-Z]' -and $password -cmatch '[a-z]' -and $password -match '\d' -and $password -match '[-_.!*]') {
            return $password
        }
    }
}

# Anonymous pull check, so the first deployment does not hang on an image the container app cannot pull.
function Test-PublicImage {
    try {
        $token = (Invoke-RestMethod -Uri "https://ghcr.io/token?scope=repository:${InitialImageRepository}:pull").token
        $headers = @{
            Authorization = "Bearer $token"
            Accept        = 'application/vnd.oci.image.index.v1+json, application/vnd.oci.image.manifest.v1+json, application/vnd.docker.distribution.manifest.list.v2+json, application/vnd.docker.distribution.manifest.v2+json'
        }
        Invoke-WebRequest -Method Head -Uri "https://ghcr.io/v2/$InitialImageRepository/manifests/latest" -Headers $headers | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

function Wait-ProviderRegistration([string] $Namespace) {
    $state = Invoke-Az provider show --namespace $Namespace --query registrationState --output tsv
    if ($state -eq 'Registered') { return }

    Write-Host "Registering resource provider $Namespace ..."
    Invoke-Az provider register --namespace $Namespace --output none | Out-Null
    $deadline = (Get-Date).AddMinutes(15)
    while ($true) {
        $state = Invoke-Az provider show --namespace $Namespace --query registrationState --output tsv
        if ($state -eq 'Registered') { return }
        if ((Get-Date) -gt $deadline) { throw "Resource provider $Namespace is still '$state' after 15 minutes." }
        Start-Sleep -Seconds 10
    }
}

function Get-EnvValue($Container, [string] $Name) {
    $item = @($Container.env | Where-Object { $_.name -eq $Name }) | Select-Object -First 1
    if ($null -eq $item -or -not ($item.PSObject.Properties.Name -contains 'value')) { return $null }
    return $item.value
}

# --- 1. Subscription -----------------------------------------------------------------------------------------

Invoke-Az account set --subscription $SubscriptionId | Out-Null
$account = Invoke-AzJson account show
if ($account.id -ne $SubscriptionId) {
    throw 'The Azure CLI did not switch to the requested subscription.'
}
Write-Host "Subscription: $($account.name)"

# SQL server Entra admin = the signed-in user. Looked up early, before any password prompt.
try {
    $signedInUser = Invoke-AzJson ad signed-in-user show
}
catch {
    $signedInUser = $null
}
if ($null -eq $signedInUser -or [string]::IsNullOrWhiteSpace($signedInUser.id)) {
    throw ('Could not read the signed-in Entra user (az ad signed-in-user show). It becomes the SQL server ' +
        'Entra admin, so sign in with a user account of the subscription tenant (az login --tenant <tenant>) and run the script again.')
}
Write-Host "SQL Entra admin: $($signedInUser.userPrincipalName)"

# --- 2. Resource providers -----------------------------------------------------------------------------------

foreach ($namespace in $RequiredProviders) {
    Wait-ProviderRegistration $namespace
}

# --- 3. Resource group ---------------------------------------------------------------------------------------

$groupExists = (Invoke-Az group exists --name $ResourceGroup) -eq 'true'
if ($groupExists) {
    $groupLocation = Invoke-Az group show --name $ResourceGroup --query location --output tsv
    if ($groupLocation -ne $Location) {
        Write-Warning "Resource group $ResourceGroup already exists in '$groupLocation'; its resources stay there (-Location '$Location' is ignored)."
    }
}
else {
    Write-Host "Creating resource group $ResourceGroup in $Location ..."
    Invoke-Az group create --name $ResourceGroup --location $Location --output none | Out-Null
}

# --- 4. Initial setup or update ------------------------------------------------------------------------------

$existingApps = @(Invoke-AzJson resource list --resource-group $ResourceGroup --resource-type Microsoft.App/containerApps --name $ContainerAppName)
$initialSetup = $existingApps.Count -eq 0

$adminPassword = ''
$sqlAdminPassword = ''
$budgetStartDate = ''

if ($initialSetup) {
    Write-Host 'Container app not found: initial setup.'

    # --- 5. Values for the first provisioning ---
    $AdminUserName = Read-RequiredValue $AdminUserName 'Super admin login'
    if ($AdminUserName -notmatch '^[A-Za-z0-9\-._@+]+$') {
        throw 'The super admin login may contain only letters, digits and - . _ @ +.'
    }
    if ($AdminUserName -eq 'admin') {
        Write-Warning "The super admin login 'admin' is the development account name; consider a different one."
    }
    $AdminNick = Read-RequiredValue $AdminNick 'Super admin nick'
    if ($AdminNick.Length -gt 32) {
        throw 'The super admin nick may have at most 32 characters.'
    }
    $BudgetEmail = Read-RequiredValue $BudgetEmail 'E-mail for budget notifications'

    # A typo in the login would later create a second super admin (see docs/deployment.md), so confirm it first.
    Write-Host ''
    Write-Host "Super admin login: $AdminUserName"
    Write-Host "Super admin nick:  $AdminNick"
    $confirmation = Read-Host -Prompt 'Deploy with these values? (y/N)'
    if ($confirmation -notmatch '^[yY]$') {
        throw 'Aborted: the super admin login and nick were not confirmed.'
    }

    $image = $InitialImage
    if (-not (Test-PublicImage)) {
        throw "Image $image cannot be pulled anonymously. Make the GHCR package public (GitHub > Packages > l2points > Package settings > Change visibility) and run the script again."
    }

    $adminPassword = Read-AdminPassword
    $sqlAdminPassword = New-SqlPassword
    $budgetStartDate = '{0:yyyy-MM}-01' -f [DateTime]::UtcNow
}
else {
    Write-Host 'Container app exists: updating the infrastructure, passwords, budget and image stay as they are.'

    $app = Invoke-AzJson resource show --resource-group $ResourceGroup --resource-type Microsoft.App/containerApps --name $ContainerAppName --api-version $ContainerAppApiVersion
    $container = $app.properties.template.containers[0]
    $image = $container.image
    $currentUserName = Get-EnvValue $container 'Admin__UserName'
    $currentNick = Get-EnvValue $container 'Admin__Nick'
    if ([string]::IsNullOrWhiteSpace($image) -or [string]::IsNullOrWhiteSpace($currentUserName) -or [string]::IsNullOrWhiteSpace($currentNick)) {
        throw "Could not read the current image or super admin settings of $ContainerAppName."
    }

    if ($AdminUserName -and $AdminUserName -ne $currentUserName) {
        Write-Warning "-AdminUserName is ignored on update; keeping '$currentUserName'."
    }
    if ($AdminNick -and $AdminNick -ne $currentNick) {
        Write-Warning "-AdminNick is ignored on update; keeping '$currentNick'."
    }
    if ($BudgetEmail) {
        Write-Warning '-BudgetEmail is ignored on update; change the budget in the Azure portal.'
    }
    $AdminUserName = $currentUserName
    $AdminNick = $currentNick
    $BudgetEmail = ''

    # The budget is created only on the initial setup, so an update cannot restore a missing one. Warn only;
    # a failing check (preview command, permissions) must not stop the update.
    $budgetFound = $false
    try {
        & az consumption budget show --budget-name budget-l2points --resource-group $ResourceGroup --output none 2>$null
        $budgetFound = ($LASTEXITCODE -eq 0)
    }
    catch {
        $budgetFound = $false
    }
    if (-not $budgetFound) {
        Write-Warning "Budget 'budget-l2points' was not found on resource group $ResourceGroup (or the check failed). Re-running this script does not create it; create it in the Azure portal (Cost Management > Budgets) with a monthly amount and e-mail alerts."
    }
}
Write-Host "Image: $image"

# --- 7. Deployment -------------------------------------------------------------------------------------------

$parameters = [ordered]@{
    '$schema'      = 'https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
    contentVersion = '1.0.0.0'
    parameters     = [ordered]@{
        initialSetup          = @{ value = $initialSetup }
        image                 = @{ value = $image }
        adminUserName         = @{ value = $AdminUserName }
        adminNick             = @{ value = $AdminNick }
        adminPassword         = @{ value = $adminPassword }
        sqlAdminPassword      = @{ value = $sqlAdminPassword }
        sqlEntraAdminLogin    = @{ value = $signedInUser.userPrincipalName }
        sqlEntraAdminObjectId = @{ value = $signedInUser.id }
        budgetEmail           = @{ value = $BudgetEmail }
        budgetStartDate       = @{ value = $budgetStartDate }
    }
}

$tempFile = $null
$parameterFile = $null
try {
    $tempFile = New-TemporaryFile
    $parameterFile = [System.IO.Path]::ChangeExtension($tempFile.FullName, '.json')
    Move-Item -LiteralPath $tempFile.FullName -Destination $parameterFile
    $parameters | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $parameterFile -Encoding utf8NoBOM

    $deploy = {
        $deploymentName = 'l2points-{0:yyyyMMdd-HHmmss}' -f [DateTime]::UtcNow
        Write-Host "Deploying $deploymentName (this takes several minutes) ..."
        Invoke-AzJson deployment group create `
            --resource-group $ResourceGroup `
            --name $deploymentName `
            --template-file $TemplateFile `
            --parameters "@$parameterFile" `
            --query properties.outputs
    }

    try {
        $outputs = & $deploy
    }
    catch {
        if (-not $initialSetup) { throw }
        # On the first run the container app may be created before its new Key Vault role assignments have
        # propagated, so it cannot read the secrets yet. The same parameters are safe to deploy again.
        Write-Warning 'The initial deployment failed. If the error above is about Key Vault secret access (role assignment propagation), a retry fixes it. Retrying once in 2 minutes ...'
        Start-Sleep -Seconds 120
        $outputs = & $deploy
    }
}
finally {
    $tempPath = if ($tempFile) { $tempFile.FullName } else { $null }
    foreach ($path in @($parameterFile, $tempPath)) {
        if ($path -and (Test-Path -LiteralPath $path)) {
            Remove-Item -LiteralPath $path -Force
        }
    }
    $adminPassword = $null
    $sqlAdminPassword = $null
    $parameters = $null
}

# --- 8. Result -----------------------------------------------------------------------------------------------

Write-Host ''
Write-Host "App URL: $($outputs.appUrl.value)" -ForegroundColor Green
Write-Host ''
Write-Host 'GitHub > repository Settings > Secrets and variables > Actions > Variables (not secrets):'
Write-Host "  AZURE_CLIENT_ID       = $($outputs.githubClientId.value)"
Write-Host "  AZURE_TENANT_ID       = $($outputs.tenantId.value)"
Write-Host "  AZURE_SUBSCRIPTION_ID = $($outputs.subscriptionId.value)"
if ($initialSetup) {
    Write-Host ''
    Write-Host "Super admin login: $AdminUserName (password as entered). The first start runs the migrations, give it a few minutes."
}
