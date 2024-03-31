targetScope = 'resourceGroup'

// Parameters
@description('The name of the web app.')
param parWebAppName string

@description('The environment name (e.g. dev, tst, prd).')
param parEnvironment string

@description('The instance of the environment.')
param parInstance string

@description('The location of the resource group.')
param parLocation string

@description('The user assigned identity to use to execute the script')
param parScriptIdentity string

// -- References
@description('The key vault reference')
param parKeyVaultRef object

@description('The app insights reference')
param parAppInsightsRef object

@description('The app service plan reference')
param parAppServicePlanRef object

@description('The api management reference')
param parApiManagementRef object

@description('The front door reference')
param parFrontDoorRef object

// -- Apis

@description('The repository api object.')
param parRepositoryApi object

// -- Common
@description('The tags to apply to the resources.')
param parTags object

// Dynamic params from pipeline invocation
param parServersIntegrationApiAppId string

// Variables
@description('Script is idempotent; execute each deployment to prevent drift')
param updateTag string = newGuid()

// Existing Out-Of-Scope Resources
resource keyVault 'Microsoft.KeyVault/vaults@2021-11-01-preview' existing = {
  name: parKeyVaultRef.Name
  scope: resourceGroup(parKeyVaultRef.SubscriptionId, parKeyVaultRef.ResourceGroupName)
}

resource frontDoor 'Microsoft.Cdn/profiles@2021-06-01' existing = {
  name: parFrontDoorRef.Name
  scope: resourceGroup(parFrontDoorRef.SubscriptionId, parFrontDoorRef.ResourceGroupName)
}

resource apiManagement 'Microsoft.ApiManagement/service@2021-12-01-preview' existing = {
  name: parApiManagementRef.Name
  scope: resourceGroup(parApiManagementRef.SubscriptionId, parApiManagementRef.ResourceGroupName)
}

resource appServicePlan 'Microsoft.Web/serverfarms@2020-10-01' existing = {
  name: parAppServicePlanRef.Name
  scope: resourceGroup(parAppServicePlanRef.SubscriptionId, parAppServicePlanRef.ResourceGroupName)
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
      linuxFxVersion: 'DOTNETCORE|8.0'
      netFrameworkVersion: 'v8.0'
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
          value: parServersIntegrationApiAppId
        }
        {
          name: 'AzureAd__ClientSecret'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=portal-servers-integration-${parEnvironment}-${parInstance}-client-secret)'
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
          name: 'portal_repository_apim_subscription_key_primary'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=${parWebAppName}-repository-api-key-primary)'
        }
        {
          name: 'portal_repository_apim_subscription_key_secondary'
          value: '@Microsoft.KeyVault(VaultName=${keyVault.name};SecretName=-${parWebAppName}-repository-api-key-secondary)'
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

resource webAppAppRole 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'script-webapp-approle-${parEnvironment}-${parInstance}'
  location: parLocation
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${parScriptIdentity}': {}
    }
  }
  properties: {
    azCliVersion: '2.52.0'
    primaryScriptUri: 'https://raw.githubusercontent.com/frasermolyneux/bicep-modules/main/scripts/GrantPrincipalAppRole.sh'
    arguments: '"${webApp.identity.principalId}" "${parRepositoryApi.ApplicationName}" "ServiceAccount'
    retentionInterval: 'P1D'
    forceUpdateTag: updateTag
  }
}

// Outputs
output outWebAppDefaultHostName string = webApp.properties.defaultHostName
output outWebAppIdentityPrincipalId string = webApp.identity.principalId
output outWebAppName string = webApp.name
