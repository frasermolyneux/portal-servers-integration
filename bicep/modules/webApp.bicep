targetScope = 'resourceGroup'

// Parameters
param parEnvironment string
param parLocation string
param parInstance string

param parWebAppName string
param parKeyVaultName string

param parStrategicServices object
param parFrontDoor object

param parRepositoryApi object

@description('The app insights reference')
param parAppInsightsRef object

param parTags object

param parServersApiAppId string

param parWorkloadSubscriptionId string
param parWorkloadResourceGroupName string

// Existing In-Scope Resources
resource appServicePlan 'Microsoft.Web/serverfarms@2020-10-01' existing = {
  name: parStrategicServices.AppServicePlanName
}

// Existing Out-Of-Scope Resources
resource frontDoor 'Microsoft.Cdn/profiles@2021-06-01' existing = {
  name: parFrontDoor.FrontDoorName
  scope: resourceGroup(parFrontDoor.SubscriptionId, parFrontDoor.FrontDoorResourceGroupName)
}

resource keyVault 'Microsoft.KeyVault/vaults@2021-11-01-preview' existing = {
  name: parKeyVaultName
  scope: resourceGroup(parWorkloadSubscriptionId, parWorkloadResourceGroupName)
}

resource apiManagement 'Microsoft.ApiManagement/service@2021-12-01-preview' existing = {
  name: parStrategicServices.ApiManagementName
  scope: resourceGroup(parStrategicServices.SubscriptionId, parStrategicServices.ApiManagementResourceGroupName)
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: parAppInsightsRef.Name
  scope: resourceGroup(parAppInsightsRef.SubscriptionId, parAppInsightsRef.ResourceGroupName)
}

// Module Resources
resource webApp 'Microsoft.Web/sites@2020-06-01' = {
  name: parWebAppName
  location: parLocation
  kind: 'app'
  tags: parTags

  identity: {
    type: 'SystemAssigned'
  }

  properties: {
    serverFarmId: appServicePlan.id

    httpsOnly: true

    siteConfig: {
      ftpsState: 'Disabled'

      alwaysOn: true
      linuxFxVersion: 'DOTNETCORE|7.0'
      netFrameworkVersion: 'v7.0'
      minTlsVersion: '1.2'

      ipSecurityRestrictions: [
        {
          ipAddress: 'AzureFrontDoor.Backend'
          action: 'Allow'
          tag: 'ServiceTag'
          priority: 1000
          name: 'RestrictToFrontDoor'
          headers: {
            'x-azure-fdid': [
              frontDoor.properties.frontDoorId
            ]
          }
        }
        {
          ipAddress: 'Any'
          action: 'Deny'
          priority: 2147483647
          name: 'Deny all'
          description: 'Deny all access'
        }
      ]

      appSettings: [
        {
          name: 'READ_ONLY_MODE'
          value: (parEnvironment == 'prd') ? 'true' : 'false'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=${appInsights.name}-instrumentationkey)'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=${appInsights.name}-connectionstring)'
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'AzureAd__TenantId'
          value: tenant().tenantId
        }
        {
          name: 'AzureAd__Instance'
          value: environment().authentication.loginEndpoint
        }
        {
          name: 'AzureAd__ClientId'
          value: parServersApiAppId
        }
        {
          name: 'AzureAd__ClientSecret'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=portal-servers-integration-${parEnvironment}-${parInstance}-clientsecret)'
        }
        {
          name: 'AzureAd__Audience'
          value: 'api://portal-servers-integration-${parEnvironment}-${parInstance}'
        }
        {
          name: 'apim_base_url'
          value: apiManagement.properties.gatewayUrl
        }
        {
          name: 'portal_repository_apim_subscription_key'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=${apiManagement.name}-${parWebAppName}-repository-subscription-apikey)'
        }
        {
          name: 'repository_api_application_audience'
          value: parRepositoryApi.ApplicationAudience
        }
        {
          name: 'xtremeidiots_ftp_certificate_thumbprint'
          value: '65173167144EA988088DA20915ABB83DB27645FA'
        }
        {
          name: 'repository_api_path_prefix'
          value: parRepositoryApi.ApiPath
        }
      ]
    }
  }
}

// Outputs
output outWebAppDefaultHostName string = webApp.properties.defaultHostName
output outWebAppIdentityPrincipalId string = webApp.identity.principalId
output outWebAppName string = webApp.name
