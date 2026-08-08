@description('Azure region for all resources.')
param location string

@description('Globally-unique web app name.')
param name string

@description('Resource ID of the Linux App Service Plan.')
param appServicePlanId string

@description('ACR login server, e.g. acrappointmedevtest.azurecr.io.')
param acrLoginServer string

@description('Initial container image reference. CI updates this to the per-SHA tag on deploy.')
param containerImage string

@description('Key Vault URI (with trailing slash, e.g. https://kv-foo.vault.azure.net/). Use the key-vault module output rather than constructing this here.')
param keyVaultUri string

@description('Application Insights connection string for OTLP exporter.')
param appInsightsConnectionString string

@description('ASP.NET Core environment name. Selects which appsettings.<Env>.json overlays appsettings.json.')
param aspNetCoreEnvironment string = 'Devtest'

@description('Tags applied to the web app.')
param tags object = {}

@description('Name of the Key Vault secret holding the SQL connection string.')
param sqlConnectionStringSecretName string = 'AppointMeSql'

@description('Name of the Key Vault secret holding the Storage connection string for Data Protection.')
param dataProtectionConnectionStringSecretName string = 'DataProtectionStorage'

@description('Name of the Key Vault secret holding the Entra External ID client secret (reused for EntraIdentity Graph client).')
param entraExternalIdClientSecretSecretName string = 'EntraExternalIdClientSecret'

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      linuxFxVersion: 'DOCKER|${containerImage}'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      acrUseManagedIdentityCreds: true
      healthCheckPath: '/'
      // App settings are intentionally minimal: only secrets, App Service plumbing, and the
      // environment selector. All non-secret configuration lives in appsettings.<Env>.json
      // (committed alongside the code), keyed by ASPNETCORE_ENVIRONMENT.
      appSettings: [
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acrLoginServer}'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: aspNetCoreEnvironment
        }
        {
          name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
          value: 'true'
        }
        {
          name: 'Authentication__EntraExternalId__ClientSecret'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/${entraExternalIdClientSecretSecretName}/)'
        }
        {
          name: 'EntraIdentity__ClientSecret'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/${entraExternalIdClientSecretSecretName}/)'
        }
        {
          name: 'ConnectionStrings__AppointMeSql'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/${sqlConnectionStringSecretName}/)'
        }
        {
          name: 'ConnectionStrings__DataProtectionStorage'
          value: '@Microsoft.KeyVault(SecretUri=${keyVaultUri}secrets/${dataProtectionConnectionStringSecretName}/)'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'OTEL_EXPORTER_OTLP_ENDPOINT'
          value: 'https://${location}.in.applicationinsights.azure.com/v2/track'
        }
      ]
    }
  }
}

output id string = webApp.id
output name string = webApp.name
output defaultHostname string = webApp.properties.defaultHostName
output principalId string = webApp.identity.principalId
