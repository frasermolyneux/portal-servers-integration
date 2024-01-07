targetScope = 'subscription'

// Parameters
@description('The location of the resource group.')
param parLocation string

@description('The environment name (e.g. dev, tst, prd).')
param parEnvironment string

@description('The instance of the environment.')
param parInstance string

@description('The strategic services configuration.')
param parStrategicServices object

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
var varDeploymentPrefix = 'platform-${varEnvironmentUniqueId}' //Prevent deployment naming conflicts

var varResourceGroupName = 'rg-portal-servers-integration-${parEnvironment}-${parLocation}-${parInstance}'
var varWebAppName = 'app-portal-servers-int-${parEnvironment}-${parLocation}-${parInstance}-${varEnvironmentUniqueId}'
var varKeyVaultName = 'kv-${varEnvironmentUniqueId}-${parLocation}'

// External Resource References
var varAppInsightsRef = {
  Name: 'ai-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
  SubscriptionId: subscription().subscriptionId
  ResourceGroupName: 'rg-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
}

var varAppServicePlanRef = {
  Name: 'asp-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
  SubscriptionId: subscription().subscriptionId
  ResourceGroupName: 'rg-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
}

var varApiManagementRef = {
  Name: parStrategicServices.ApiManagementName
  SubscriptionId: parStrategicServices.SubscriptionId
  ResourceGroupName: parStrategicServices.ApiManagementResourceGroupName
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
  scope: resourceGroup(varApiManagementRef.SubscriptionId, varApiManagementRef.ResourceGroupName)
  dependsOn: [ keyVaultSecretUserRoleAssignment ]

  params: {
    parApiManagementName: varApiManagementRef.Name
    parAppInsightsRef: varAppInsightsRef
  }
}

module platformScripts 'modules/platformScripts.bicep' = {
  name: '${varDeploymentPrefix}-platformScripts'
  scope: resourceGroup(defaultResourceGroup.name)
  dependsOn: [ keyVaultSecretUserRoleAssignment ]

  params: {
    parEnvironment: parEnvironment
    parLocation: parLocation
    parInstance: parInstance
    parScriptIdentity: parScriptIdentity

    parKeyVaultRef: {
      name: keyVault.outputs.outKeyVaultName
      subscriptionId: subscription().subscriptionId
      resourceGroupName: defaultResourceGroup.name
    }
  }
}

// API Management subscription for the repository API that will be used by the webapp
module repositoryApimSubscription 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/apimanagementsubscription:latest' = {
  name: '${varDeploymentPrefix}-repositoryApimSubscription'
  scope: resourceGroup(varApiManagementRef.SubscriptionId, varApiManagementRef.ResourceGroupName)

  params: {
    parDeploymentPrefix: varDeploymentPrefix
    parApiManagementName: varApiManagementRef.Name
    parWorkloadSubscriptionId: subscription().subscriptionId
    parWorkloadResourceGroupName: defaultResourceGroup.name
    parWorkloadName: varWebAppName
    parKeyVaultName: varKeyVaultName
    parSubscriptionScopeIdentifier: 'repository'
    parSubscriptionScope: '/apis/${parRepositoryApi.ApimApiName}'
    parTags: parTags
  }
}

// Outputs
output keyVaultName string = keyVault.outputs.outKeyVaultName
