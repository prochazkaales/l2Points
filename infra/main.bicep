// Azure infrastructure for L2Points (resource group scope). Deployed manually by infra/deploy.ps1, never by CI.
//
// initialSetup = true  : first provisioning. Writes the passwords (SQL admin, super admin) to Key Vault and to the
//                        SQL server, and creates the cost budget.
// initialSetup = false : infrastructure update. No password is passed or changed, the budget is left alone and the
//                        caller passes the image and super admin values the container app currently runs with.
//
// The container app reads its secrets from Key Vault through its user-assigned identity (versionless secret URIs),
// so the secrets keep working even when this template does not (re)deploy them.

targetScope = 'resourceGroup'

@description('Region of all resources.')
param location string = resourceGroup().location

@description('True on the first provisioning: sets passwords and creates the budget. False on later updates.')
param initialSetup bool

@description('Container image of the app, e.g. ghcr.io/prochazkaales/l2points:latest.')
param image string

@description('Login of the super admin account the app seeds (Admin__UserName).')
param adminUserName string

@description('Nick of the super admin account the app seeds (Admin__Nick).')
param adminNick string

@secure()
@description('Super admin password (Admin__Password). Required when initialSetup is true, otherwise empty.')
param adminPassword string = ''

@secure()
@description('SQL server admin password used by the app. Required when initialSetup is true, otherwise empty.')
param sqlAdminPassword string = ''

@description('Display name (UPN) of the Entra user that becomes the SQL server Entra admin.')
param sqlEntraAdminLogin string

@description('Object id of the Entra user that becomes the SQL server Entra admin.')
param sqlEntraAdminObjectId string

@description('E-mail for budget notifications. Used only when initialSetup is true.')
param budgetEmail string = ''

@description('Monthly budget of the resource group in the billing currency.')
param budgetAmount int = 20

@description('First day of the budget, first day of a month (yyyy-MM-01). Used only when initialSetup is true.')
param budgetStartDate string = ''

@description('GitHub repository (owner/name) whose main branch may deploy the app.')
param githubRepository string = 'prochazkaales/l2Points'

// Short suffix for globally unique names (Key Vault, SQL server).
var suffix = substring(uniqueString(resourceGroup().id), 0, 6)

var keyVaultName = 'kv-l2points-${suffix}'
var sqlServerName = 'sql-l2points-${suffix}'
var sqlDatabaseName = 'sqldb-l2points'
var sqlAdminLogin = 'l2pointsadmin'
var containerPort = 8080

var sqlConnectionStringSecretName = 'sql-connection-string'
var adminPasswordSecretName = 'admin-password'

// Built-in role definitions (well-known ids, identical in every tenant).
var contributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
var keyVaultCryptoUserRoleId = '12338af0-0e69-4776-bea7-57ae8d297424'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ---------------------------------------------------------------------------------------------------------------
// Logs
// ---------------------------------------------------------------------------------------------------------------

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-l2points'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    workspaceCapping: {
      // Daily ingestion cap (GB) to protect the subscription credit; Bicep has no decimal literals.
      dailyQuotaGb: json('0.1')
    }
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Identities
// ---------------------------------------------------------------------------------------------------------------

// Identity of the container app. User-assigned, so its Key Vault roles exist before the first revision starts.
resource appIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-l2points-app'
  location: location
}

// Identity GitHub Actions signs in as (OIDC, no client secret) to deploy new images.
resource githubIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-l2points-github'
  location: location
}

resource githubFederatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: githubIdentity
  name: 'github-main'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepository}:ref:refs/heads/main'
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}

// Contributor on this resource group only (az containerapp update in CI).
resource githubContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, githubIdentity.id, contributorRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleId)
    principalId: githubIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Key Vault
// ---------------------------------------------------------------------------------------------------------------

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    // Soft delete stays at its default (on, 90 days); purge protection is deliberately not enabled.
    publicNetworkAccess: 'Enabled'
  }
}

// Wraps the Data Protection keys stored in the DB. ARM creates the key only if it does not exist yet,
// a redeploy does not add a new version.
resource dataProtectionKey 'Microsoft.KeyVault/vaults/keys@2023-07-01' = {
  parent: keyVault
  name: 'dataprotection'
  properties: {
    kty: 'RSA'
    keySize: 2048
    keyOps: [
      'wrapKey'
      'unwrapKey'
      'encrypt'
      'decrypt'
    ]
  }
}

resource appKeyVaultCryptoUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, appIdentity.id, keyVaultCryptoUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultCryptoUserRoleId)
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource appKeyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVault
  name: guid(keyVault.id, appIdentity.id, keyVaultSecretsUserRoleId)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: appIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Secrets are written only on the first provisioning; a later run keeps the existing values.
resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (initialSetup) {
  parent: keyVault
  name: sqlConnectionStringSecretName
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

resource adminPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (initialSetup) {
  parent: keyVault
  name: adminPasswordSecretName
  properties: {
    value: adminPassword
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Azure SQL
// ---------------------------------------------------------------------------------------------------------------

resource sqlServer 'Microsoft.Sql/servers@2023-08-01' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    // The password is sent only on the first provisioning; omitting it on update keeps the current one.
    ...(initialSetup ? { administratorLoginPassword: sqlAdminPassword } : {})
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: 'User'
      login: sqlEntraAdminLogin
      sid: sqlEntraAdminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: false
    }
  }
}

// "Allow Azure services and resources to access this server" (0.0.0.0 - 0.0.0.0).
resource sqlAllowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Created here as Basic: a database created by the app itself would get the default (expensive) tier.
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    maxSizeBytes: 2147483648
    requestedBackupStorageRedundancy: 'Local'
  }
}

// ---------------------------------------------------------------------------------------------------------------
// Container Apps
// ---------------------------------------------------------------------------------------------------------------

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-l2points'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-l2points'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${appIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: containerPort
        allowInsecure: false
        transport: 'auto'
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      // Versionless Key Vault references: the app always gets the current secret version.
      secrets: [
        {
          name: sqlConnectionStringSecretName
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${sqlConnectionStringSecretName}'
          identity: appIdentity.id
        }
        {
          name: adminPasswordSecretName
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/${adminPasswordSecretName}'
          identity: appIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'l2points'
          image: image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ConnectionStrings__Default'
              secretRef: sqlConnectionStringSecretName
            }
            {
              name: 'Admin__UserName'
              value: adminUserName
            }
            {
              name: 'Admin__Nick'
              value: adminNick
            }
            {
              name: 'Admin__Password'
              secretRef: adminPasswordSecretName
            }
            {
              name: 'DataProtection__KeyVaultKeyUri'
              value: dataProtectionKey.properties.keyUri
            }
            {
              // The app connects as server admin; never let it create a missing database (default tier is expensive).
              name: 'Database__CreateIfMissing'
              value: 'false'
            }
            {
              name: 'ForwardedHeaders__TrustAllProxies'
              value: 'true'
            }
            {
              name: 'AZURE_CLIENT_ID'
              value: appIdentity.properties.clientId
            }
          ]
          probes: [
            {
              // The app listens only after migrations and seeding, and may first wait up to 3 minutes on the
              // startup lock held by another replica. Window: 30 s + 10 x 50 s = ~9 minutes.
              type: 'Startup'
              httpGet: {
                path: '/health/live'
                port: containerPort
              }
              initialDelaySeconds: 30
              periodSeconds: 50
              timeoutSeconds: 5
              failureThreshold: 10
            }
            {
              type: 'Liveness'
              httpGet: {
                path: '/health/live'
                port: containerPort
              }
              periodSeconds: 30
              timeoutSeconds: 5
              failureThreshold: 3
            }
            {
              // Checks the DB; a failure only takes the replica out of traffic, it does not restart it.
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: containerPort
              }
              periodSeconds: 15
              timeoutSeconds: 5
              failureThreshold: 3
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
  dependsOn: [
    // The first revision reads Key Vault secrets and the key with the app identity, and connects to the
    // pre-created Basic database through the Azure services firewall rule.
    appKeyVaultCryptoUser
    appKeyVaultSecretsUser
    sqlConnectionStringSecret
    adminPasswordSecret
    sqlDatabase
    sqlAllowAzureServices
  ]
}

// ---------------------------------------------------------------------------------------------------------------
// Cost budget (first provisioning only, later changes are made in the portal)
// ---------------------------------------------------------------------------------------------------------------

resource budget 'Microsoft.Consumption/budgets@2023-05-01' = if (initialSetup) {
  name: 'budget-l2points'
  properties: {
    category: 'Cost'
    amount: budgetAmount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: '${budgetStartDate}T00:00:00Z'
    }
    notifications: {
      actual80: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [
          budgetEmail
        ]
      }
      actual100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [
          budgetEmail
        ]
      }
      forecasted100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [
          budgetEmail
        ]
      }
    }
  }
}

// No secrets in outputs.
output appUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output githubClientId string = githubIdentity.properties.clientId
output tenantId string = subscription().tenantId
output subscriptionId string = subscription().subscriptionId
