targetScope = 'resourceGroup'

// Parameters
@description('The environment name (e.g. dev, tst, prd).')
param parEnvironment string

@description('The instance of the environment.')
param parInstance string

@description('The location of the resource group.')
param parLocation string

// Module Resources
resource deploymentScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'externalScriptCLI'
  location: parLocation
  kind: 'AzurePowerShell'
  properties: {
    azPowerShellVersion: '10.0'
    primaryScriptUri: 'https://raw.githubusercontent.com/frasermolyneux/bicep-modules/main/scripts/CreateAppRegistration.ps1'
    arguments: '-applicationName \\"portal-servers-integration-${parEnvironment}-${parInstance}\\" -appRoles \\"${loadJsonContent('./../../app-registration-manifests/portal-servers-integration-approles.json')}\\"'
    retentionInterval: 'P1D'
  }
}
