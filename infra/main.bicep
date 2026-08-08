targetScope = 'resourceGroup'

@description('Environment slug (e.g. devtest, staging, prod). Used in resource names.')
@minLength(3)
@maxLength(10)
param environmentName string

@description('Azure region. Default: uksouth.')
param location string = resourceGroup().location

@description('SQL admin login.')
param sqlAdminLogin string

@description('SQL admin password.')
@secure()
param sqlAdminPassword string

@description('Container image reference to deploy initially. CI updates this per-SHA.')
param initialContainerImage string

@description('Optional custom hostname to bind to the App Service (e.g. app.appointme.dev). Leave empty to skip custom-domain provisioning. See infra/modules/custom-domain.bicep for DNS preconditions.')
param customHostname string = ''

var defaultTags = {
  application: 'appointme'
  environment: environmentName
  managedBy: 'bicep'
}

// uniqueString is deterministic per resource group — keeps names stable across deployments
// while ensuring globally-unique resources.
var nameSuffix = uniqueString(resourceGroup().id)
var shortSuffix = take(nameSuffix, 6)

var names = {
  logAnalytics: 'log-appointme-${environmentName}'
  appInsights: 'appi-appointme-${environmentName}'
  keyVault: take('kv-appointme-${shortSuffix}', 24)
  sqlServer: take('sql-appointme-${environmentName}-${shortSuffix}', 63)
  sqlDatabase: 'appointme'
  containerRegistry: take('acrappointme${environmentName}${shortSuffix}', 50)
  storageAccount: take('stappointme${environmentName}${shortSuffix}', 24)
  appServicePlan: 'asp-appointme-${environmentName}'
  appService: take('app-appointme-${environmentName}-${shortSuffix}', 60)
}

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'log-analytics'
  params: {
    location: location
    name: names.logAnalytics
    tags: defaultTags
  }
}

module appInsights 'modules/app-insights.bicep' = {
  name: 'app-insights'
  params: {
    location: location
    name: names.appInsights
    workspaceId: logAnalytics.outputs.id
    tags: defaultTags
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'key-vault'
  params: {
    location: location
    name: names.keyVault
    tags: defaultTags
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    serverName: names.sqlServer
    databaseName: names.sqlDatabase
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    tags: defaultTags
  }
}

module registry 'modules/container-registry.bicep' = {
  name: 'container-registry'
  params: {
    location: location
    name: names.containerRegistry
    tags: defaultTags
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    name: names.storageAccount
    tags: defaultTags
  }
}

module appServicePlan 'modules/app-service-plan.bicep' = {
  name: 'app-service-plan'
  params: {
    location: location
    name: names.appServicePlan
    tags: defaultTags
  }
}

module appService 'modules/app-service.bicep' = {
  name: 'app-service'
  params: {
    location: location
    name: names.appService
    appServicePlanId: appServicePlan.outputs.id
    acrLoginServer: registry.outputs.loginServer
    containerImage: initialContainerImage
    keyVaultUri: keyVault.outputs.uri
    appInsightsConnectionString: appInsights.outputs.connectionString
    aspNetCoreEnvironment: environmentName == 'devtest' ? 'Devtest' : environmentName
    tags: defaultTags
  }
}

module roleAssignments 'modules/role-assignments.bicep' = {
  name: 'role-assignments'
  params: {
    principalId: appService.outputs.principalId
    containerRegistryName: registry.outputs.name
    keyVaultName: keyVault.outputs.name
  }
}

module customDomain 'modules/custom-domain.bicep' = if (!empty(customHostname)) {
  name: 'custom-domain'
  params: {
    siteName: appService.outputs.name
    appServicePlanId: appServicePlan.outputs.id
    customHostname: customHostname
    location: location
    tags: defaultTags
  }
}

output appServiceUrl string = 'https://${appService.outputs.defaultHostname}'
output containerRegistryLoginServer string = registry.outputs.loginServer
output containerRegistryName string = registry.outputs.name
output keyVaultName string = keyVault.outputs.name
output keyVaultUri string = keyVault.outputs.uri
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output storageAccountName string = storage.outputs.accountName
output appInsightsConnectionString string = appInsights.outputs.connectionString
