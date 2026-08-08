@description('Azure region for all resources.')
param location string

@description('App Service Plan name.')
param name string

@description('Tags applied to the plan.')
param tags object = {}

@description('Plan SKU. Devtest default: F1 (free; no Always On, no custom-domain bindings — the Cloudflare Worker fronts the custom domain). Use B1+ for App Service-managed custom domains, S1+ for deployment slots.')
param sku object = {
  name: 'F1'
  tier: 'Free'
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: name
  location: location
  tags: tags
  sku: sku
  kind: 'linux'
  properties: {
    reserved: true
  }
}

output id string = plan.id
output name string = plan.name
