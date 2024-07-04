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

@description('The front door configuration.')
param parFrontDoorRef object

@description('The DNS configuration.')
param parDns object

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

var varResourceGroupName = 'rg-portal-servers-integration-${parEnvironment}-${parLocation}-${parInstance}'
var varCoreResourceGroupName = 'rg-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
var varWorkloadName = 'app-portal-servers-int-${parEnvironment}-${parInstance}-${varEnvironmentUniqueId}'
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
  name: '${varEnvironmentUniqueId}-keyVault'
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
  name: '${varEnvironmentUniqueId}-keyVaultSecretUserRoleAssignment'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    parKeyVaultName: keyVault.outputs.outKeyVaultName
    parRoleDefinitionId: keyVaultSecretUserRoleDefinition.id
    parPrincipalId: apiManagement.identity.principalId
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

    parKeyVaultRef: {
      name: keyVault.outputs.outKeyVaultName
      subscriptionId: subscription().subscriptionId
      resourceGroupName: defaultResourceGroup.name
    }
  }
}

// API Management subscription for the repository API that will be used by the webapp
module repositoryApimSubscriptionForWebApp 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/apimanagementsubscription:latest' = {
  name: '${varEnvironmentUniqueId}-repositoryApimSubscriptionForWebApp'
  scope: resourceGroup(varApiManagementRef.SubscriptionId, varApiManagementRef.ResourceGroupName)

  params: {
    apiManagementName: apiManagement.name
    subscriptionName: varWebAppName
    apiScope: parRepositoryApi.ApimApiName
    keyVaultRef: {
      Name: varKeyVaultName
      SubscriptionId: subscription().subscriptionId
      ResourceGroupName: defaultResourceGroup.name
    }
    tags: parTags
  }
}

// API Management subscription for the repository API that will be used by the integration tests
module repositoryApimSubscriptionForTests 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/apimanagementsubscription:latest' = {
  name: '${varEnvironmentUniqueId}-repositoryApimSubscriptionForTests'
  scope: resourceGroup(varApiManagementRef.SubscriptionId, varApiManagementRef.ResourceGroupName)

  params: {
    apiManagementName: apiManagement.name
    subscriptionName: '${varWebAppName}-tests'
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

    parKeyVaultRef: {
      name: keyVault.outputs.outKeyVaultName
      subscriptionId: subscription().subscriptionId
      resourceGroupName: defaultResourceGroup.name
    }

    parAppInsightsRef: varAppInsightsRef
    parAppServicePlanRef: varAppServicePlanRef
    parApiManagementRef: varApiManagementRef
    parFrontDoorRef: parFrontDoorRef

    parRepositoryApi: parRepositoryApi

    parTags: parTags

    parServersIntegrationApiAppId: platformScripts.outputs.outServersIntegrationApiAppId
  }
}

module webAppKeyVaultRoleAssignment 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/keyvaultroleassignment:latest' = {
  name: '${varEnvironmentUniqueId}-webAppKeyVaultRoleAssignment'
  scope: resourceGroup(defaultResourceGroup.name)

  params: {
    parKeyVaultName: varKeyVaultName
    parRoleDefinitionId: keyVaultSecretUserRoleDefinition.id
    parPrincipalId: webApp.outputs.outWebAppIdentityPrincipalId
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

module legacy_apiManagementApi 'modules/legacy_apiManagementApi.bicep' = {
  name: '${varEnvironmentUniqueId}-apiManagementApi'
  scope: resourceGroup(varApiManagementRef.SubscriptionId, varApiManagementRef.ResourceGroupName)

  params: {
    parEnvironment: parEnvironment
    parInstance: parInstance

    parApiManagementName: varApiManagementRef.Name
    parFrontDoorDns: varWorkloadName
    parParentDnsName: parDns.Domain

    parAppInsightsRef: varAppInsightsRef
  }
}

module frontDoorEndpoint 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/frontdoorendpoint:latest' = {
  name: '${varEnvironmentUniqueId}-frontDoorEndpoint'
  scope: resourceGroup(parFrontDoorRef.SubscriptionId, parFrontDoorRef.ResourceGroupName)

  params: {
    frontDoorName: parFrontDoorRef.Name
    dnsZoneRef: {
      SubscriptionId: parDns.SubscriptionId
      ResourceGroupName: parDns.ResourceGroupName
      Name: parDns.Domain
    }
    subdomain: parDns.Subdomain
    originHostName: webApp.outputs.outWebAppDefaultHostName
    tags: parTags
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

    parKeyVaultRef: {
      name: keyVault.outputs.outKeyVaultName
      subscriptionId: subscription().subscriptionId
      resourceGroupName: defaultResourceGroup.name
    }
  }
}

// Outputs
output keyVaultName string = keyVault.outputs.outKeyVaultName
output webAppName string = webApp.outputs.outWebAppName

output principalId string = webApp.outputs.outWebAppIdentityPrincipalId
