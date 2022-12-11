targetScope = 'subscription'

// Parameters
param parLocation string
param parEnvironment string

param parLoggingSubscriptionId string
param parLoggingResourceGroupName string
param parLoggingWorkspaceName string

param parStrategicServicesSubscriptionId string
param parApiManagementResourceGroupName string
param parApiManagementName string

param parTags object

// Variables
var environmentUniqueId = uniqueString('portal-servers-integration', parEnvironment)
var varDeploymentPrefix = 'platform-${environmentUniqueId}' //Prevent deployment naming conflicts

var varResourceGroupName = 'rg-webapi-portal-svr-int-${parEnvironment}-${parLocation}'
var varAppInsightsName = 'ai-webapi-portal-svr-int-${environmentUniqueId}-${parEnvironment}-${parLocation}'
var varKeyVaultName = 'kv-${environmentUniqueId}-${parLocation}'

// Existing Out-Of-Scope Resources
resource apiManagement 'Microsoft.ApiManagement/service@2021-12-01-preview' existing = {
  name: parApiManagementName
  scope: resourceGroup(parStrategicServicesSubscriptionId, parApiManagementResourceGroupName)
}

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

    parEnabledForRbacAuthorization: true
    parKeyVaultCreateMode: 'default'

    parTags: parTags
  }
}

@description('https://learn.microsoft.com/en-gb/azure/role-based-access-control/built-in-roles#key-vault-secrets-user')
resource keyVaultSecretUserRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '4633458b-17de-408a-b874-0445c86b69e6'
}

module keyVaultSecretUserRoleAssignment 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/keyvaultroleassignment:latest' = {
  name: '${varDeploymentPrefix}-keyVaultSecretUserRoleAssignment'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    parKeyVaultName: keyVault.outputs.outKeyVaultName
    parRoleDefinitionId: keyVaultSecretUserRoleDefinition.id
    parPrincipalId: apiManagement.identity.principalId
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

module apiManagementLogger 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/apimanagementlogger:latest' = {
  name: '${varDeploymentPrefix}-apiManagementLogger'
  scope: resourceGroup(parStrategicServicesSubscriptionId, parApiManagementResourceGroupName)
  dependsOn: [ keyVaultSecretUserRoleAssignment ]

  params: {
    parApiManagementName: parApiManagementName
    parWorkloadSubscriptionId: subscription().subscriptionId
    parWorkloadResourceGroupName: defaultResourceGroup.name
    parAppInsightsName: appInsights.outputs.outAppInsightsName
    parKeyVaultName: keyVault.outputs.outKeyVaultName
  }
}

// Outputs
output keyVaultName string = keyVault.outputs.outKeyVaultName
