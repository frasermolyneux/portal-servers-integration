targetScope = 'subscription'

// Parameters
@description('The location to deploy the resources')
param location string

@description('The environment name (e.g. dev, tst, prd).')
param environment string

@description('The instance of the environment.')
param instance string

@description('The api management resource name')
param apiManagementName string

@description('The repository API configuration.')
param repositoryApi object

@description('The tags to apply to the resources.')
param tags object

// Dynamic params from pipeline invocation
param keyVaultCreateMode string = 'default'

@description('The user assigned identity to execute the deployment scripts under')
param scriptIdentity string

// Variables
var environmentUniqueId = uniqueString('portal-servers-integration', environment, instance)

var resourceGroupName = 'rg-portal-servers-integration-${environment}-${location}-${instance}'
var coreResourceGroupName = 'rg-portal-core-${environment}-${location}-${instance}'
var webAppName = 'app-portal-servers-int-${environment}-${location}-${instance}-${environmentUniqueId}'
var keyVaultName = 'kv-${environmentUniqueId}-${location}'

// External Resource References
var appInsightsRef = {
  SubscriptionId: subscription().subscriptionId
  ResourceGroupName: 'rg-portal-core-${environment}-${location}-${instance}'
  Name: 'ai-portal-core-${environment}-${location}-${instance}'
}

var appServicePlanRef = {
  SubscriptionId: subscription().subscriptionId
  ResourceGroupName: 'rg-portal-core-${environment}-${location}-${instance}'
  Name: 'asp-portal-core-${environment}-${location}-${instance}'
}

var apiManagementRef = {
  SubscriptionId: subscription().subscriptionId
  ResourceGroupName: 'rg-portal-core-${environment}-${location}-${instance}'
  Name: apiManagementName
}

// Existing Out-Of-Scope Resources
resource apiManagement 'Microsoft.ApiManagement/service@2021-12-01-preview' existing = {
  name: apiManagementRef.Name
  scope: resourceGroup(apiManagementRef.SubscriptionId, apiManagementRef.ResourceGroupName)
}

// Module Resources
resource defaultResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resourceGroupName
  location: location
  tags: tags

  properties: {}
}

module keyVault 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/keyvault:latest' = {
  name: '${environmentUniqueId}-keyVault'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    keyVaultName: keyVaultName
    keyVaultCreateMode: keyVaultCreateMode
    location: location
    tags: tags
  }
}

@description('https://learn.microsoft.com/en-gb/azure/role-based-access-control/built-in-roles#key-vault-secrets-user')
resource keyVaultSecretUserRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '4633458b-17de-408a-b874-0445c86b69e6'
}

module keyVaultSecretUserRoleAssignment 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/keyvaultroleassignment:latest' = {
  name: '${environmentUniqueId}-keyVaultSecretUserRoleAssignment'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    keyVaultName: keyVault.outputs.keyVaultRef.name
    principalId: apiManagement.identity.principalId
    roleDefinitionId: keyVaultSecretUserRoleDefinition.id
  }
}

module apiManagementLogger 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/apimanagementlogger:latest' = {
  name: '${environmentUniqueId}-apiManagementLogger'
  scope: resourceGroup(apiManagementRef.SubscriptionId, apiManagementRef.ResourceGroupName)
  dependsOn: [keyVaultSecretUserRoleAssignment]

  params: {
    apiManagementName: apiManagementRef.Name
    appInsightsRef: appInsightsRef
  }
}

module platformScripts 'modules/platformScripts.bicep' = {
  name: '${environmentUniqueId}-platformScripts'
  scope: resourceGroup(defaultResourceGroup.name)
  dependsOn: [keyVaultSecretUserRoleAssignment]

  params: {
    environment: environment
    location: location
    instance: instance
    scriptIdentity: scriptIdentity
    keyVaultRef: keyVault.outputs.keyVaultRef
  }
}

// API Management subscription for the repository API that will be used by the integration tests
module repositoryApimSubscriptionForTests 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/apimanagementsubscription:latest' = {
  name: '${environmentUniqueId}-repositoryApimSubscriptionForTests'
  scope: resourceGroup(apiManagementRef.SubscriptionId, apiManagementRef.ResourceGroupName)

  params: {
    apiManagementName: apiManagement.name
    workloadName: '${webAppName}-tests'
    apiScope: repositoryApi.ApimApiName
    keyVaultRef: {
      Name: keyVaultName
      SubscriptionId: subscription().subscriptionId
      ResourceGroupName: defaultResourceGroup.name
    }
    tags: tags
  }
}

// Main web app resource for the workload
module webApp 'modules/webApp.bicep' = {
  name: '${environmentUniqueId}-webApp'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    webAppName: webAppName
    environment: environment
    instance: instance
    location: location

    scriptIdentity: scriptIdentity

    keyVaultRef: keyVault.outputs.keyVaultRef

    appInsightsRef: appInsightsRef
    appServicePlanRef: appServicePlanRef
    apiManagementRef: apiManagementRef

    repositoryApi: repositoryApi

    tags: tags

    serversIntegrationApiAppId: platformScripts.outputs.outServersIntegrationApiAppId
  }
}

module webAppKeyVaultRoleAssignment 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/keyvaultroleassignment:latest' = {
  name: '${environmentUniqueId}-webAppKeyVaultRoleAssignment'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    keyVaultName: keyVaultName
    principalId: webApp.outputs.webAppIdentityPrincipalId
    roleDefinitionId: keyVaultSecretUserRoleDefinition.id
  }
}

module apiManagementApi 'modules/apiManagementApi.bicep' = {
  name: '${environmentUniqueId}-apiManagementApi'
  scope: resourceGroup(coreResourceGroupName)

  params: {
    environment: environment
    instance: instance

    apiManagementName: apiManagementName
    backendHostname: webApp.outputs.outWebAppDefaultHostName

    appInsightsRef: appInsightsRef
  }
}

// Integration Test Resources
module testScripts 'modules/testScripts.bicep' = {
  name: '${environmentUniqueId}-testScripts'
  scope: resourceGroup(defaultResourceGroup.name)
  dependsOn: [keyVaultSecretUserRoleAssignment]

  params: {
    environment: environment
    location: location
    instance: instance
    scriptIdentity: scriptIdentity

    apiAppRegistrationName: platformScripts.outputs.outAppRegistrationName

    keyVaultRef: keyVault.outputs.keyVaultRef
  }
}

// Outputs
output keyVaultName string = keyVault.outputs.keyVaultRef.name
output webAppName string = webApp.outputs.webAppName
output webAppResourceGroup string = webApp.outputs.outWebAppResourceGroup
output principalId string = webApp.outputs.webAppIdentityPrincipalId
