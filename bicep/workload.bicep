targetScope = 'resourceGroup'

// Parameters
param parLocation string
param parEnvironment string
param parInstance string

param parFrontDoor object
param parDns object
param parStrategicServices object

param parRepositoryApi object

param parTags object

// Dynamic params from pipeline invocation
param parServersIntegrationApiAppId string

// Variables
var varEnvironmentUniqueId = uniqueString('portal-servers-integration', parEnvironment, parInstance)
var varDeploymentPrefix = 'workload-${varEnvironmentUniqueId}' //Prevent deployment naming conflicts

var varWorkloadName = 'app-portal-servers-int-${parEnvironment}-${parInstance}-${varEnvironmentUniqueId}'
var varWebAppName = 'app-portal-servers-int-${parEnvironment}-${parLocation}-${parInstance}-${varEnvironmentUniqueId}'
var varKeyVaultName = 'kv-${varEnvironmentUniqueId}-${parLocation}'

// External Resource References
var varAppInsightsRef = {
  Name: 'ai-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
  SubscriptionId: subscription().subscriptionId
  ResourceGroupName: 'rg-portal-core-${parEnvironment}-${parLocation}-${parInstance}'
}

// Module Resources
module serversIntegrationApiManagementSubscription 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/apimanagementsubscription:latest' = {
  name: '${varDeploymentPrefix}-serversIntegrationApiManagementSub'
  scope: resourceGroup(parStrategicServices.SubscriptionId, parStrategicServices.ApiManagementResourceGroupName)

  params: {
    parDeploymentPrefix: varDeploymentPrefix
    parApiManagementName: parStrategicServices.ApiManagementName
    parWorkloadSubscriptionId: subscription().subscriptionId
    parWorkloadResourceGroupName: resourceGroup().name
    parWorkloadName: varWebAppName
    parKeyVaultName: varKeyVaultName
    parSubscriptionScopeIdentifier: 'repository'
    parSubscriptionScope: '/apis/${parRepositoryApi.ApimApiName}'
    parTags: parTags
  }
}

module webApp 'modules/webApp.bicep' = {
  name: '${varDeploymentPrefix}-webApp'

  params: {
    parEnvironment: parEnvironment
    parLocation: parLocation
    parInstance: parInstance

    parWebAppName: varWebAppName
    parKeyVaultName: varKeyVaultName

    parStrategicServices: parStrategicServices
    parFrontDoor: parFrontDoor

    parRepositoryApi: parRepositoryApi

    parAppInsightsRef: varAppInsightsRef

    parServersApiAppId: parServersIntegrationApiAppId

    parWorkloadSubscriptionId: subscription().subscriptionId
    parWorkloadResourceGroupName: resourceGroup().name

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

  params: {
    parKeyVaultName: varKeyVaultName
    parRoleDefinitionId: keyVaultSecretUserRoleDefinition.id
    parPrincipalId: webApp.outputs.outWebAppIdentityPrincipalId
  }
}

module apiManagementApi 'modules/apiManagementApi.bicep' = {
  name: '${varDeploymentPrefix}-apiManagementApi'
  scope: resourceGroup(parStrategicServices.SubscriptionId, parStrategicServices.ApiManagementResourceGroupName)

  params: {
    parEnvironment: parEnvironment
    parInstance: parInstance

    parApiManagementName: parStrategicServices.ApiManagementName
    parFrontDoorDns: varWorkloadName
    parParentDnsName: parDns.ParentDnsName

    parAppInsightsRef: varAppInsightsRef
  }
}

module frontDoorEndpoint 'br:acrty7og2i6qpv3s.azurecr.io/bicep/modules/frontdoorendpoint:latest' = {
  name: '${varDeploymentPrefix}-frontDoorEndpoint'
  scope: resourceGroup(parFrontDoor.SubscriptionId, parFrontDoor.FrontDoorResourceGroupName)

  params: {
    parDeploymentPrefix: varDeploymentPrefix
    parFrontDoorName: parFrontDoor.FrontDoorName
    parDnsSubscriptionId: parDns.SubscriptionId
    parParentDnsName: parDns.ParentDnsName
    parDnsResourceGroupName: parDns.DnsResourceGroupName
    parWorkloadName: varWorkloadName
    parOriginHostName: webApp.outputs.outWebAppDefaultHostName
    parDnsZoneHostnamePrefix: varWorkloadName
    parCustomHostname: '${varWorkloadName}.${parDns.ParentDnsName}'
    parTags: parTags
  }
}

// Outputs
output webAppName string = webApp.outputs.outWebAppName

output principalId string = webApp.outputs.outWebAppIdentityPrincipalId
