@description('Azure region for all resources.')
param location string

@description('Logical SQL Server name.')
param serverName string

@description('Database name.')
param databaseName string

@description('SQL admin login.')
param administratorLogin string

@description('SQL admin password.')
@secure()
param administratorLoginPassword string

@description('Tags applied to the server and database.')
param tags object = {}

@description('Database SKU. Devtest default: Basic (5 DTU, 2 GB cap). Use S0+ when the demo dataset outgrows it.')
param databaseSku object = {
  name: 'Basic'
  tier: 'Basic'
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  tags: tags
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: databaseSku
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
  }
}

// Devtest convenience — allow Azure services to reach the server. README documents
// tightening this for production (private endpoints + VNet integration).
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// The SQL connection string is NOT exposed as an output by design — secrets are
// seeded into Key Vault manually (see infra/README.md). Consumers compose the
// connection string from serverFqdn + databaseName + the admin credentials they
// already hold.
output serverName string = sqlServer.name
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
