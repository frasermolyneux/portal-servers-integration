targetScope = 'resourceGroup'

// Parameters
param parLocation string
param parEnvironment string

param parFrontDoorSubscriptionId string
param parFrontDoorResourceGroupName string
param parFrontDoorName string

param parDnsSubscriptionId string
param parDnsResourceGroupName string
param parParentDnsName string

param parStrategicServicesSubscriptionId string
param parApiManagementResourceGroupName string
param parApiManagementName string
param parWebAppsResourceGroupName string
param parAppServicePlanName string

param parServersIntegrationApiAppId string

param parTags object

// Variables
var environmentUniqueId = uniqueString('portal-servers-integration', parEnvironment)
var varDeploymentPrefix = 'workload-${environmentUniqueId}' //Prevent deployment naming conflicts

var varWorkloadName = 'portal-svr-int-${environmentUniqueId}-${parEnvironment}'
var varWebAppName = 'webapi-portal-svr-int-${environmentUniqueId}-${parEnvironment}-${parLocation}'
var varAppInsightsName = 'ai-portal-svr-int-${environmentUniqueId}-${parEnvironment}-${parLocation}'
var varKeyVaultName = 'kv-${environmentUniqueId}-${parLocation}'

// Module Resources
module serversIntegrationApiManagementSubscription 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/apimanagementsubscription:latest' = {
  name: '${varDeploymentPrefix}-serversIntegrationApiManagementSub'
  scope: resourceGroup(parStrategicServicesSubscriptionId, parApiManagementResourceGroupName)

  params: {
    parDeploymentPrefix: varDeploymentPrefix
    parApiManagementName: parApiManagementName
    parWorkloadSubscriptionId: subscription().subscriptionId
    parWorkloadResourceGroupName: resourceGroup().name
    parWorkloadName: varWebAppName
    parKeyVaultName: varKeyVaultName
    parSubscriptionScopeIdentifier: 'portal-repository'
    parSubscriptionScope: '/apis/repository-api-v2'
    parTags: parTags
  }
}

module webApp 'modules/webApp.bicep' = {
  name: '${varDeploymentPrefix}-webApp'
  scope: resourceGroup(parStrategicServicesSubscriptionId, parWebAppsResourceGroupName)

  params: {
    parLocation: parLocation
    parEnvironment: parEnvironment
    parWebAppName: varWebAppName
    parKeyVaultName: varKeyVaultName
    parAppInsightsName: varAppInsightsName

    parServersApiAppId: parServersIntegrationApiAppId

    parStrategicServicesSubscriptionId: parStrategicServicesSubscriptionId
    parApiManagementResourceGroupName: parApiManagementResourceGroupName
    parApiManagementName: parApiManagementName
    parAppServicePlanName: parAppServicePlanName

    parFrontDoorSubscriptionId: parFrontDoorSubscriptionId
    parFrontDoorResourceGroupName: parFrontDoorResourceGroupName
    parFrontDoorName: parFrontDoorName

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

module keyVaultSecretUserRoleAssignment 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/keyvaultroleassignment:latest' = {
  name: '${varDeploymentPrefix}-keyVaultSecretUserRoleAssignment'

  params: {
    parKeyVaultName: varKeyVaultName
    parRoleDefinitionId: keyVaultSecretUserRoleDefinition.id
    parPrincipalId: webApp.outputs.outWebAppIdentityPrincipalId
  }
}

module keyVaultSecretUserRoleAssignmentSlot 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/keyvaultroleassignment:latest' = if (parEnvironment == 'prd') {
  name: '${varDeploymentPrefix}-keyVaultSecretUserRoleAssignmentSlot'

  params: {
    parKeyVaultName: varKeyVaultName
    parRoleDefinitionId: keyVaultSecretUserRoleDefinition.id
    parPrincipalId: webApp.outputs.outWebAppStagingIdentityPrincipalId
  }
}

module apiManagementApi 'modules/apiManagementApi.bicep' = {
  name: '${varDeploymentPrefix}-apiManagementApi'
  scope: resourceGroup(parStrategicServicesSubscriptionId, parApiManagementResourceGroupName)

  params: {
    parApiManagementName: parApiManagementName
    parFrontDoorDns: varWorkloadName
    parParentDnsName: parParentDnsName
    parEnvironment: parEnvironment
    parWorkloadSubscriptionId: subscription().subscriptionId
    parWorkloadResourceGroupName: resourceGroup().name
    parAppInsightsName: varAppInsightsName
  }
}

module frontDoorEndpoint 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/frontdoorendpoint:latest' = {
  name: '${varDeploymentPrefix}-frontDoorEndpoint'
  scope: resourceGroup(parFrontDoorSubscriptionId, parFrontDoorResourceGroupName)

  params: {
    parDeploymentPrefix: varDeploymentPrefix
    parFrontDoorName: parFrontDoorName
    parDnsSubscriptionId: parDnsSubscriptionId
    parParentDnsName: parParentDnsName
    parDnsResourceGroupName: parDnsResourceGroupName
    parWorkloadName: varWorkloadName
    parOriginHostName: webApp.outputs.outWebAppDefaultHostName
    parDnsZoneHostnamePrefix: varWorkloadName
    parCustomHostname: '${varWorkloadName}.${parParentDnsName}'
    parTags: parTags
  }
}

// Outputs
output webAppName string = webApp.outputs.outWebAppName
