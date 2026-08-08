@description('Azure region for all resources.')
param location string

@description('Name of the Log Analytics workspace.')
param name string

@description('Tags applied to the workspace.')
param tags object = {}

@description('Data retention in days. Devtest default: 30.')
param retentionInDays int = 30

@description('Daily ingestion cap in GB — protects against runaway ingestion cost; Azure notifies when the cap is hit. Devtest default: 0.5. Set to \'-1\' to disable. (String because Bicep has no decimal literals.)')
param dailyQuotaGb string = '0.5'

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: {
      dailyQuotaGb: json(dailyQuotaGb)
    }
  }
}

output id string = workspace.id
output customerId string = workspace.properties.customerId
