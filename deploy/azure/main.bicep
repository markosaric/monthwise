// Monthwise on Azure (FR-10): Container Apps (scale to zero) with the release image, PostgreSQL
// Flexible Server, Key Vault for the secrets, Log Analytics and a monthly budget alert.
// Deployed by deploy.sh into one resource group; see the user manual, "Installing Monthwise".

@description('EU region for every resource.')
param location string = resourceGroup().location

@description('Release version to run, e.g. 1.2.0 (image ghcr.io/markosaric/monthwise:<version>).')
param version string

@description('GitHub user that owns the read-only package token.')
param ghcrUsername string

@secure()
@description('GitHub token with only the read:packages scope, so Azure can pull the private image.')
param ghcrToken string

@secure()
param postgresPassword string

@secure()
param encryptionKey string

@secure()
param hashKey string

@description('Object id of the person deploying, who may read and write the Key Vault secrets.')
param deployerObjectId string

@description('Email address for the budget alert.')
param budgetEmail string

@description('Monthly budget in the billing currency.')
param budgetAmount int = 25

@description('First day of the budget, yyyy-MM-01. Kept the same on every deployment.')
param budgetStartDate string

var suffix = uniqueString(resourceGroup().id)
var postgresAdmin = 'monthwise'
var image = 'ghcr.io/markosaric/monthwise:${version}'
var keyVaultSecretsUser = '4633458b-17de-408a-b874-0445c86b69e6'
var keyVaultSecretsOfficer = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-monthwise-${suffix}'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-monthwise-${suffix}'
  location: location
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-monthwise-${take(suffix, 10)}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 30
  }
}

resource appReadsSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, identity.id, keyVaultSecretsUser)
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUser)
  }
}

resource deployerManagesSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: vault
  name: guid(vault.id, deployerObjectId, keyVaultSecretsOfficer)
  properties: {
    principalId: deployerObjectId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficer)
  }
}

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: 'psql-monthwise-${suffix}'
  location: location
  sku: { name: 'Standard_B1ms', tier: 'Burstable' }
  properties: {
    version: '17'
    administratorLogin: postgresAdmin
    administratorLoginPassword: postgresPassword
    storage: { storageSizeGB: 32, autoGrow: 'Disabled' }
    backup: { backupRetentionDays: 7, geoRedundantBackup: 'Disabled' }
    highAvailability: { mode: 'Disabled' }
    network: { publicNetworkAccess: 'Enabled' }
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: 'monthwise'
  properties: { charset: 'UTF8', collation: 'en_US.utf8' }
}

// 0.0.0.0 allows connections from Azure services only (the Container App), not the internet.
resource allowAzure 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

var connectionString = 'Host=${postgres.properties.fullyQualifiedDomainName};Port=5432;Database=monthwise;Username=${postgresAdmin};Password=${postgresPassword};SSL Mode=Require'

resource connectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'connection-string'
  properties: { value: connectionString }
}

resource postgresPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'postgres-password'
  properties: { value: postgresPassword }
}

resource encryptionKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'encryption-key-k1'
  properties: { value: encryptionKey }
}

resource hashKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'hash-key'
  properties: { value: hashKey }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-monthwise-${suffix}'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-monthwise'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  dependsOn: [appReadsSecrets, database, allowAzure]
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        { server: 'ghcr.io', username: ghcrUsername, passwordSecretRef: 'ghcr-token' }
      ]
      secrets: [
        { name: 'ghcr-token', value: ghcrToken }
        { name: 'connection-string', keyVaultUrl: connectionStringSecret.properties.secretUri, identity: identity.id }
        { name: 'encryption-key-k1', keyVaultUrl: encryptionKeySecret.properties.secretUri, identity: identity.id }
        { name: 'hash-key', keyVaultUrl: hashKeySecret.properties.secretUri, identity: identity.id }
      ]
    }
    template: {
      containers: [
        {
          name: 'monthwise'
          image: image
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'Hosting__BehindProxy', value: 'true' }
            { name: 'SensitiveData__ActiveKeyId', value: 'k1' }
          ]
          // The secrets become files in /config/secrets, read like on the other targets (FR-6).
          volumeMounts: [
            { volumeName: 'secrets', mountPath: '/config/secrets' }
          ]
          probes: [
            {
              type: 'Startup'
              httpGet: { path: '/api/health', port: 8080 }
              periodSeconds: 5
              failureThreshold: 24
            }
            {
              type: 'Liveness'
              httpGet: { path: '/api/health', port: 8080 }
              periodSeconds: 30
              failureThreshold: 3
            }
          ]
        }
      ]
      volumes: [
        {
          name: 'secrets'
          storageType: 'Secret'
          secrets: [
            { secretRef: 'connection-string', path: 'ConnectionStrings__Monthwise' }
            { secretRef: 'encryption-key-k1', path: 'SensitiveData__EncryptionKeys__k1' }
            { secretRef: 'hash-key', path: 'SensitiveData__HashKey' }
          ]
        }
      ]
      // Scale to zero when unused: no cost while idle, a few seconds for the first request after.
      scale: { minReplicas: 0, maxReplicas: 1 }
    }
  }
}

resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: 'monthwise-monthly'
  properties: {
    category: 'Cost'
    amount: budgetAmount
    timeGrain: 'Monthly'
    timePeriod: { startDate: budgetStartDate }
    notifications: {
      actual80: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [budgetEmail]
      }
      forecast100: {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [budgetEmail]
      }
    }
  }
}

output url string = 'https://${app.properties.configuration.ingress.fqdn}'
output keyVaultName string = vault.name
