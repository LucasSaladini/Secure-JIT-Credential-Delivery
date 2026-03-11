param location string = resourceGroup().location
param projectName string = 'scd-gateway'
param sqlAdminLogin string = 'scdadmin'
@secure()
param sqlAdminPassword string

var uniqueSuffix = uniqueString(resourceGroup().id)
var vaultName = 'kv-${projectName}-${uniqueSuffix}'
var sqlServerName = 'sql-${projectName}-${uniqueSuffix}'
var funcAppName = 'func-${projectName}-${uniqueSuffix}'

// Key Vault
resource vault = 'Microsoft.KeyVault/vaults@2023-07-01' = {
    name: vaultName
    location = location
    properties: {
        sku: { family: 'A', name: 'standard' }
        tenantId: subscription().tenantId
        accessPolicies: []
        enableRbacAuthorization: true
    }
}

// SQL Server & Database
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
    name: sqlServerName
    location: location
    properties: {
        administratorLogin: sqlAdminLogin
        administratorLoginPassword: sqlAdminPassword
    }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
    parent: sqlServer
    name: 'SecureGatewayDB'
    location: location
    sku: { name: 'Basic', tier: 'Basic' }
}

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
    name: 'st${replace(projectName, '-', '')}${uniqueSuffix}'
    location: location
    sku: { name: 'Standard_LRS' }
    kind: 'StorageV2'
}

// Function App
resource funcApp 'Microsoft.Web/sites@2022-09-01' = {
    name: funcAppName
    location: location
    kind: 'functionapp'
    identity: { type: 'SystemAssigned' }
    properties: {
        serverFarmId: appServicePlan.id
        siteConfig: {
            appSettings: [
                { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}' }
                { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
                { name: 'KeyVaultUri', value: vault.properties.vaultUri }
                { name: 'SqlConnectionString', value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=SecureGatewayDB;Persist Security Info=False;User ID=${sqlAdminLogin};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;' }
            ]
        }
    }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: 'plan-${projectName}'
  location: location
  sku: { name: 'Y1', tier: 'Dynamic' } // Consumption Plan
}

// Setting Permission of Function on Key Vault (RBAC)
resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, funcApp.id, 'Key Vault Secrets User')
  scope: vault
  properties: {
    principalId: funcApp.identity.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalType: 'ServicePrincipal'
  }
}