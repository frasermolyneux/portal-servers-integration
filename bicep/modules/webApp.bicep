targetScope = 'resourceGroup'

// Parameters
@description('The name of the web app.')
param webAppName string

@description('The environment for the resources')
param environment string

@description('The instance of the environment.')
param instance string

@description('The location to deploy the resources')
param location string

@description('The user assigned identity to use to execute the script')
param scriptIdentity string

// -- References
@description('A reference to the key vault resource')
param keyVaultRef object

@description('A reference to the app insights resource')
param appInsightsRef object

@description('A reference to the app service plan resource')
param appServicePlanRef object

@description('A reference to the api management resource')
param apiManagementRef object

// -- Apis

@description('The repository api object.')
param repositoryApi object

// -- Common
@description('The tags to apply to the resources.')
param tags object

// Dynamic params from pipeline invocation
param serversIntegrationApiAppId string

// Variables
@description('Script is idempotent; execute each deployment to prevent drift')
param updateTag string = newGuid()

// Existing Out-Of-Scope Resources
resource keyVault 'Microsoft.KeyVault/vaults@2021-11-01-preview' existing = {
  name: keyVaultRef.Name
  scope: resourceGroup(keyVaultRef.SubscriptionId, keyVaultRef.ResourceGroupName)
}

resource apiManagement 'Microsoft.ApiManagement/service@2021-12-01-preview' existing = {
  name: apiManagementRef.Name
  scope: resourceGroup(apiManagementRef.SubscriptionId, apiManagementRef.ResourceGroupName)
}

resource appServicePlan 'Microsoft.Web/serverfarms@2020-10-01' existing = {
  name: appServicePlanRef.Name
  scope: resourceGroup(appServicePlanRef.SubscriptionId, appServicePlanRef.ResourceGroupName)
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsRef.Name
  scope: resourceGroup(appInsightsRef.SubscriptionId, appInsightsRef.ResourceGroupName)
}

// Module Resources
module repositoryApimSubscription 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/apimanagementsubscription:latest' = {
  name: 'repositoryApimSubscription'
  scope: resourceGroup(apiManagementRef.SubscriptionId, apiManagementRef.ResourceGroupName)

  params: {
    apiManagementName: apiManagement.name
    workloadName: webAppName
    apiScope: repositoryApi.ApimApiName
    keyVaultRef: keyVaultRef
    tags: tags
  }
}

resource webApp 'Microsoft.Web/sites@2020-06-01' = {
  name: webAppName
  location: location
  kind: 'app'
  tags: tags

  identity: {
    type: 'SystemAssigned'
  }

  properties: {
    serverFarmId: appServicePlan.id

    httpsOnly: true

    siteConfig: {
      ftpsState: 'Disabled'

      alwaysOn: true
      linuxFxVersion: 'DOTNETCORE|8.0'
      netFrameworkVersion: 'v8.0'
      minTlsVersion: '1.2'

      healthCheckPath: '/api/health'

      appSettings: [
        {
          name: 'READ_ONLY_MODE'
          value: (environment == 'prd') ? 'true' : 'false'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
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
          value: serversIntegrationApiAppId
        }
        {
          name: 'AzureAd__ClientSecret'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=portal-servers-integration-${environment}-${instance}-client-secret)'
        }
        {
          name: 'AzureAd__Audience'
          value: 'api://portal-servers-integration-${environment}-${instance}'
        }
        {
          name: 'apim_base_url'
          value: apiManagement.properties.gatewayUrl
        }
        {
          name: 'portal_repository_apim_subscription_key_primary'
          value: '@Microsoft.KeyVault(SecretUri=${repositoryApimSubscription.outputs.primaryKeySecretRef.secretUri})'
        }
        {
          name: 'portal_repository_apim_subscription_key_secondary'
          value: '@Microsoft.KeyVault(SecretUri=${repositoryApimSubscription.outputs.secondaryKeySecretRef.secretUri})'
        }
        {
          name: 'repository_api_application_audience'
          value: repositoryApi.ApplicationAudience
        }
        {
          name: 'xtremeidiots_ftp_certificate_thumbprint'
          value: '65173167144EA988088DA20915ABB83DB27645FA'
        }
        {
          name: 'repository_api_path_prefix'
          value: repositoryApi.ApiPath
        }
        {
          name: 'APPINSIGHTS_PROFILERFEATURE_VERSION'
          value: '1.0.0'
        }
        {
          name: 'DiagnosticServices_EXTENSION_VERSION'
          value: '~3'
        }
      ]
    }
  }
}

module webTest 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/webtest:latest' = {
  name: '${deployment().name}-webtest'
  scope: resourceGroup(appInsightsRef.SubscriptionId, appInsightsRef.ResourceGroupName)

  params: {
    workloadName: webApp.name
    testUrl: 'https://${webApp.properties.defaultHostName}/api/health'
    appInsightsRef: appInsightsRef
    location: location
    tags: tags
  }
}

resource webAppAppRole 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'script-webapp-approle-${environment}-${instance}'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${scriptIdentity}': {}
    }
  }
  properties: {
    azCliVersion: '2.52.0'
    primaryScriptUri: 'https://raw.githubusercontent.com/frasermolyneux/bicep-modules/main/scripts/GrantPrincipalAppRole.sh'
    arguments: '"${webApp.identity.principalId}" "${repositoryApi.ApplicationName}" "ServiceAccount'
    retentionInterval: 'P1D'
    forceUpdateTag: updateTag
  }
}

// Outputs
output outWebAppDefaultHostName string = webApp.properties.defaultHostName
output webAppIdentityPrincipalId string = webApp.identity.principalId
output webAppName string = webApp.name
output outWebAppResourceGroup string = resourceGroup().name
