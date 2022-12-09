targetScope = 'resourceGroup'

// Parameters
param parLocation string
param parEnvironment string

param parConnectivitySubscriptionId string
param parFrontDoorResourceGroupName string
param parDnsResourceGroupName string
param parFrontDoorName string
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
var varDeploymentPrefix = 'portal-servers-integration-workload-${environmentUniqueId}' //Prevent deployment naming conflicts

var varWorkloadName = 'webapi-${environmentUniqueId}-${parEnvironment}'
var varWebAppName = 'webapi-${environmentUniqueId}-${parEnvironment}-${parLocation}'

// Module Resources
module serversIntegrationApiManagementSubscription 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/apimanagementsubscription:latest' = {
  name: '${varDeploymentPrefix}-serversIntegrationApiManagementSubscription'
  scope: resourceGroup(parStrategicServicesSubscriptionId, parApiManagementResourceGroupName)

  params: {
    parDeploymentPrefix: varDeploymentPrefix
    parApiManagementName: parApiManagementName
    parWorkloadSubscriptionId: subscription().subscriptionId
    parWorkloadResourceGroupName: defaultResourceGroup.name
    parWorkloadName: varWebAppName
    parKeyVaultName: keyVault.name
    parSubscriptionScopeIdentifier: 'portal-repository'
    parSubscriptionScope: '/apis/repository-api'
    parTags: parTags
  }
}

module webApp 'modules/webApp.bicep' = {
  name: '${varDeploymentPrefix}-webApp'
  scope: resourceGroup(parStrategicServicesSubscriptionId, parWebAppsResourceGroupName)

  params: {
    parLocation: parLocation
    parEnvironment: parEnvironment
    parKeyVaultName: keyVault.name
    parAppInsightsName: appInsights.name

    parServersApiAppId: parServersIntegrationApiAppId

    parStrategicServicesSubscriptionId: parStrategicServicesSubscriptionId
    parApiManagementResourceGroupName: parApiManagementResourceGroupName
    parApiManagementName: parApiManagementName
    parAppServicePlanName: parAppServicePlanName

    parConnectivitySubscriptionId: parConnectivitySubscriptionId
    parFrontDoorResourceGroupName: parFrontDoorResourceGroupName
    parFrontDoorName: parFrontDoorName

    parWorkloadSubscriptionId: subscription().subscriptionId
    parWorkloadResourceGroupName: defaultResourceGroup.name

    parTags: parTags
  }
}

module keyVaultAccessPolicy 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/keyvaultaccesspolicy:latest' = {
  name: '${varDeploymentPrefix}-keyVaultAccessPolicy'
  scope: defaultResourceGroup

  params: {
    parKeyVaultName: keyVault.name
    parPrincipalId: webApp.outputs.outWebAppIdentityPrincipalId
  }
}

module slotKeyVaultAccessPolicy 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/keyvaultaccesspolicy:latest' = if (parEnvironment == 'prd') {
  name: '${varDeploymentPrefix}-slotKeyVaultAccessPolicy'
  scope: defaultResourceGroup

  params: {
    parKeyVaultName: keyVault.name
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
    parWorkloadResourceGroupName: defaultResourceGroup.name
    parAppInsightsName: appInsights.name
  }
}

module frontDoorEndpoint 'br:acrmxplatformprduksouth.azurecr.io/bicep/modules/frontdoorendpoint:latest' = {
  name: '${varDeploymentPrefix}-frontDoorEndpoint'
  scope: resourceGroup(parConnectivitySubscriptionId, parFrontDoorResourceGroupName)

  params: {
    parDeploymentPrefix: varDeploymentPrefix
    parFrontDoorName: parFrontDoorName
    parParentDnsName: parParentDnsName
    parDnsResourceGroupName: parDnsResourceGroupName
    parWorkloadName: varWorkloadName
    parOriginHostName: webApp.outputs.outWebAppDefaultHostName
    parDnsZoneHostnamePrefix: varWorkloadName
    parCustomHostname: '${varWorkloadName}.${parParentDnsName}'
    parTags: parTags
  }
}
