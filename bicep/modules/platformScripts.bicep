targetScope = 'resourceGroup'

// Parameters
@description('The environment name (e.g. dev, tst, prd).')
param parEnvironment string

@description('The instance of the environment.')
param parInstance string

@description('The location of the resource group.')
param parLocation string

@description('The user assigned identity to use to execute the script')
param parScriptIdentity string

// Module Resources
resource deploymentScript 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'externalScriptCLI'
  location: parLocation
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${parScriptIdentity}': {}
    }
  }
  properties: {
    azCliVersion: '2.52.0'
    primaryScriptUri: 'https://raw.githubusercontent.com/frasermolyneux/bicep-modules/main/scripts/CreateAppRegistration.sh'
    arguments: '"portal-servers-integration-${parEnvironment}-${parInstance}" "${loadJsonContent('./../../app-registration-manifests/portal-servers-integration-approles.json')}"'
    retentionInterval: 'P1D'
  }
}
