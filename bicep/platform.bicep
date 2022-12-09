targetScope = 'subscription'

// Parameters
param parLocation string
param parEnvironment string

param parLoggingSubscriptionId string
param parLoggingResourceGroupName string
param parLoggingWorkspaceName string

param parKeyVaultCreateMode string = 'recover'

param parTags object

// Variables
var environmentUniqueId = substring(base64('portal-servers-integration-${parEnvironment}'), 0, 12)
var varDeploymentPrefix = 'portal-servers-integration-platform-${environmentUniqueId}' //Prevent deployment naming conflicts

var varResourceGroupName = 'rg-portal-servers-integration-${parEnvironment}-${parLocation}'
var varAppInsightsName = 'ai-${environmentUniqueId}-${parEnvironment}-${parLocation}'
var varKeyVaultName = 'kv-${environmentUniqueId}-${parLocation}'

// Module Resources
resource defaultResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: varResourceGroupName
  location: parLocation
  tags: parTags

  properties: {}
}

module keyVault 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/keyvault:latest' = {
  name: '${varDeploymentPrefix}-keyVault'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    parKeyVaultName: varKeyVaultName
    parLocation: parLocation

    parKeyVaultCreateMode: parKeyVaultCreateMode

    parTags: parTags
  }
}

module appInsights 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/appinsights:latest' = {
  name: '${varDeploymentPrefix}-appInsights'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    parAppInsightsName: varAppInsightsName
    parKeyVaultName: keyVault.outputs.outKeyVaultName
    parLocation: parLocation
    parLoggingSubscriptionId: parLoggingSubscriptionId
    parLoggingResourceGroupName: parLoggingResourceGroupName
    parLoggingWorkspaceName: parLoggingWorkspaceName
    parTags: parTags
  }
}
