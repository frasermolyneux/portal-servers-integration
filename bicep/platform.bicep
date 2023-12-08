targetScope = 'subscription'

// Parameters
param parLocation string
param parEnvironment string
param parInstance string

param parLogging object
param parStrategicServices object

param parTags object

// Dynamic params from pipeline invocation
param parKeyVaultCreateMode string = 'default'

// Variables
var varEnvironmentUniqueId = uniqueString('portal-servers-integration', parEnvironment, parInstance)
var varDeploymentPrefix = 'platform-${varEnvironmentUniqueId}' //Prevent deployment naming conflicts

var varResourceGroupName = 'rg-portal-servers-integration-${parEnvironment}-${parLocation}-${parInstance}'
var varKeyVaultName = 'kv-${varEnvironmentUniqueId}-${parLocation}'

// External Resource References
var varAppInsightsRef = {
  Name: 'ai-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
  SubscriptionId: subscription().subscriptionId
  ResourceGroupName: 'rg-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
}

// Existing Out-Of-Scope Resources
resource apiManagement 'Microsoft.ApiManagement/service@2021-12-01-preview' existing = {
  name: parStrategicServices.ApiManagementName
  scope: resourceGroup(parStrategicServices.SubscriptionId, parStrategicServices.ApiManagementResourceGroupName)
}

// Module Resources
resource defaultResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: varResourceGroupName
  location: parLocation
  tags: parTags

  properties: {}
}

module keyVault 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/keyvault:latest' = {
  name: '${varDeploymentPrefix}-keyVault'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    parKeyVaultName: varKeyVaultName
    parLocation: parLocation

    parEnabledForRbacAuthorization: true
    parKeyVaultCreateMode: parKeyVaultCreateMode

    parTags: parTags
  }
}

@description('https://learn.microsoft.com/en-gb/azure/role-based-access-control/built-in-roles#key-vault-secrets-user')
resource keyVaultSecretUserRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '4633458b-17de-408a-b874-0445c86b69e6'
}

module keyVaultSecretUserRoleAssignment 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/keyvaultroleassignment:latest' = {
  name: '${varDeploymentPrefix}-keyVaultSecretUserRoleAssignment'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    parKeyVaultName: keyVault.outputs.outKeyVaultName
    parRoleDefinitionId: keyVaultSecretUserRoleDefinition.id
    parPrincipalId: apiManagement.identity.principalId
  }
}

module apiManagementLogger 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/apimanagementlogger:latest' = {
  name: '${varDeploymentPrefix}-apiManagementLogger'
  scope: resourceGroup(parStrategicServices.SubscriptionId, parStrategicServices.ApiManagementResourceGroupName)
  dependsOn: [ keyVaultSecretUserRoleAssignment ]

  params: {
    parApiManagementName: parStrategicServices.ApiManagementName
    parWorkloadSubscriptionId: subscription().subscriptionId
    parWorkloadResourceGroupName: defaultResourceGroup.name
    parAppInsightsName: varAppInsightsRef.Name
    parKeyVaultName: keyVault.outputs.outKeyVaultName
  }
}

// Outputs
output keyVaultName string = keyVault.outputs.outKeyVaultName
