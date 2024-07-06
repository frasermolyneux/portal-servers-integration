targetScope = 'subscription'

// Parameters
@description('The location of the resource group.')
param parLocation string

@description('The environment name (e.g. dev, tst, prd).')
param parEnvironment string

@description('The instance of the environment.')
param parInstance string

@description('The API Management name')
param parApiManagementName string

@description('The repository API configuration.')
param parRepositoryApi object

@description('The tags to apply to the resources.')
param parTags object

// Dynamic params from pipeline invocation
param parKeyVaultCreateMode string = 'default'

@description('The user assigned identity to execute the deployment scripts under')
param parScriptIdentity string

// Variables
var varEnvironmentUniqueId = uniqueString('portal-servers-integration', parEnvironment, parInstance)

var varResourceGroupName = 'rg-portal-servers-integration-${parEnvironment}-${parLocation}-${parInstance}'
var varCoreResourceGroupName = 'rg-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
var varWebAppName = 'app-portal-servers-int-${parEnvironment}-${parLocation}-${parInstance}-${varEnvironmentUniqueId}'
var varKeyVaultName = 'kv-${varEnvironmentUniqueId}-${parLocation}'

// External Resource References
var varAppInsightsRef = {
  SubscriptionId: subscription().subscriptionId
  ResourceGroupName: 'rg-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
  Name: 'ai-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
}

var varAppServicePlanRef = {
  SubscriptionId: subscription().subscriptionId
  ResourceGroupName: 'rg-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
  Name: 'asp-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
}

var varApiManagementRef = {
  SubscriptionId: subscription().subscriptionId
  ResourceGroupName: 'rg-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
  Name: parApiManagementName
}

// Existing Out-Of-Scope Resources
resource apiManagement 'Microsoft.ApiManagement/service@2021-12-01-preview' existing = {
  name: varApiManagementRef.Name
  scope: resourceGroup(varApiManagementRef.SubscriptionId, varApiManagementRef.ResourceGroupName)
}

// Module Resources
resource defaultResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: varResourceGroupName
  location: parLocation
  tags: parTags

  properties: {}
}

module keyVault 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/keyvault:latest' = {
  name: '${varEnvironmentUniqueId}-keyVault'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    keyVaultName: varKeyVaultName
    keyVaultCreateMode: parKeyVaultCreateMode
    location: parLocation
    tags: parTags
  }
}

@description('https://learn.microsoft.com/en-gb/azure/role-based-access-control/built-in-roles#key-vault-secrets-user')
resource keyVaultSecretUserRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '4633458b-17de-408a-b874-0445c86b69e6'
}

module keyVaultSecretUserRoleAssignment 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/keyvaultroleassignment:latest' = {
  name: '${varEnvironmentUniqueId}-keyVaultSecretUserRoleAssignment'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    keyVaultName: keyVault.outputs.keyVaultRef.name
    principalId: apiManagement.identity.principalId
    roleDefinitionId: keyVaultSecretUserRoleDefinition.id
  }
}

module apiManagementLogger 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/apimanagementlogger:latest' = {
  name: '${varEnvironmentUniqueId}-apiManagementLogger'
  scope: resourceGroup(varApiManagementRef.SubscriptionId, varApiManagementRef.ResourceGroupName)
  dependsOn: [keyVaultSecretUserRoleAssignment]

  params: {
    apiManagementName: varApiManagementRef.Name
    appInsightsRef: varAppInsightsRef
  }
}

module platformScripts 'modules/platformScripts.bicep' = {
  name: '${varEnvironmentUniqueId}-platformScripts'
  scope: resourceGroup(defaultResourceGroup.name)
  dependsOn: [keyVaultSecretUserRoleAssignment]

  params: {
    parEnvironment: parEnvironment
    parLocation: parLocation
    parInstance: parInstance
    parScriptIdentity: parScriptIdentity
    parKeyVaultRef: keyVault.outputs.keyVaultRef
  }
}

// API Management subscription for the repository API that will be used by the integration tests
module repositoryApimSubscriptionForTests 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/apimanagementsubscription:latest' = {
  name: '${varEnvironmentUniqueId}-repositoryApimSubscriptionForTests'
  scope: resourceGroup(varApiManagementRef.SubscriptionId, varApiManagementRef.ResourceGroupName)

  params: {
    apiManagementName: apiManagement.name
    workloadName: '${varWebAppName}-tests'
    apiScope: parRepositoryApi.ApimApiName
    keyVaultRef: {
      Name: varKeyVaultName
      SubscriptionId: subscription().subscriptionId
      ResourceGroupName: defaultResourceGroup.name
    }
    tags: parTags
  }
}

// Main web app resource for the workload
module webApp 'modules/webApp.bicep' = {
  name: '${varEnvironmentUniqueId}-webApp'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    parWebAppName: varWebAppName
    parEnvironment: parEnvironment
    parInstance: parInstance
    parLocation: parLocation

    parScriptIdentity: parScriptIdentity

    parKeyVaultRef: keyVault.outputs.keyVaultRef

    parAppInsightsRef: varAppInsightsRef
    parAppServicePlanRef: varAppServicePlanRef
    parApiManagementRef: varApiManagementRef

    parRepositoryApi: parRepositoryApi

    parTags: parTags

    parServersIntegrationApiAppId: platformScripts.outputs.outServersIntegrationApiAppId
  }
}

module webAppKeyVaultRoleAssignment 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/keyvaultroleassignment:latest' = {
  name: '${varEnvironmentUniqueId}-webAppKeyVaultRoleAssignment'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    keyVaultName: varKeyVaultName
    principalId: webApp.outputs.outWebAppIdentityPrincipalId
    roleDefinitionId: keyVaultSecretUserRoleDefinition.id
  }
}

module apiManagementApi 'modules/apiManagementApi.bicep' = {
  name: '${varEnvironmentUniqueId}-apiManagementApi'
  scope: resourceGroup(varCoreResourceGroupName)

  params: {
    parEnvironment: parEnvironment
    parInstance: parInstance

    parApiManagementName: parApiManagementName
    parBackendHostname: webApp.outputs.outWebAppDefaultHostName

    parAppInsightsRef: varAppInsightsRef
  }
}

// Integration Test Resources
module testScripts 'modules/testScripts.bicep' = {
  name: '${varEnvironmentUniqueId}-testScripts'
  scope: resourceGroup(defaultResourceGroup.name)
  dependsOn: [keyVaultSecretUserRoleAssignment]

  params: {
    parEnvironment: parEnvironment
    parLocation: parLocation
    parInstance: parInstance
    parScriptIdentity: parScriptIdentity

    parApiAppRegistrationName: platformScripts.outputs.outAppRegistrationName

    parKeyVaultRef: keyVault.outputs.keyVaultRef
  }
}

// Outputs
output keyVaultName string = keyVault.outputs.keyVaultRef.name
output webAppName string = webApp.outputs.outWebAppName
output webAppResourceGroup string = webApp.outputs.outWebAppResourceGroup
output principalId string = webApp.outputs.outWebAppIdentityPrincipalId
