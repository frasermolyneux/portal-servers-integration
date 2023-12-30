targetScope = 'resourceGroup'

// Parameters
@description('The environment name (e.g. dev, tst, prd).')
param parEnvironment string

@description('The instance of the environment.')
param parInstance string

@description('The location of the resource group.')
param parLocation string

@secure()
@description('The client id of the service principal to use.')
param parClientId string

@secure()
@description('The client secret of the service principal to use.')
param parClientSecret string

// Module Resources
resource deploymentScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'externalScriptCLI'
  location: parLocation
  kind: 'AzureCLI'
  properties: {
    azCliVersion: '2.52.0'
    primaryScriptUri: 'https://raw.githubusercontent.com/frasermolyneux/bicep-modules/main/scripts/CreateAppRegistration.sh'
    arguments: '-clientId \\"${parClientId}\\" -clientSecret \\"${parClientSecret}\\" -tenantId \\"${tenant().tenantId}\\"  -applicationName \\"portal-servers-integration-${parEnvironment}-${parInstance}\\" -appRoles \\"${loadJsonContent('./../../app-registration-manifests/portal-servers-integration-approles.json')}\\"'
    retentionInterval: 'P1D'
  }
}
