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

@description('The name of the API application registration')
param parApiAppRegistrationName string

// -- References
@description('The key vault reference')
param parKeyVaultRef object

// Variables
@description('Script is idempotent; execute each deployment to prevent drift')
param updateTag string = newGuid()

// Existing Out-Of-Scope Resources
resource keyVault 'Microsoft.KeyVault/vaults@2021-11-01-preview' existing = {
  name: parKeyVaultRef.Name
  scope: resourceGroup(parKeyVaultRef.SubscriptionId, parKeyVaultRef.ResourceGroupName)
}

// Module Resources
resource appRegistrationTests 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'script-app-registration-tests-${parEnvironment}-${parInstance}'
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
    environmentVariables: [
      {
        name: 'appRoles'
        value: loadTextContent('./../../app-registration-manifests/portal-servers-integration-approles.json')
      }
    ]
    primaryScriptUri: 'https://raw.githubusercontent.com/frasermolyneux/bicep-modules/main/scripts/CreateAppRegistration.sh'
    arguments: '"portal-servers-integration-${parEnvironment}-${parInstance}-tests"'
    retentionInterval: 'P1D'
    forceUpdateTag: updateTag
  }
}

resource appRegistrationTestsCredentials 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'script-app-registration-tests-credentials-${parEnvironment}-${parInstance}'
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
    primaryScriptUri: 'https://raw.githubusercontent.com/frasermolyneux/bicep-modules/main/scripts/CreateAppRegistrationCredential.sh'
    arguments: '"${keyVault.name}" "portal-servers-integration-${parEnvironment}-${parInstance}-tests" "portal-servers-integration-${parEnvironment}-${parInstance}-tests" "portalserversintegrationtests"'
    retentionInterval: 'P1D'
    forceUpdateTag: updateTag
  }
}

resource appRegistrationTestsAppRole 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'script-app-registration-tests-approle-${parEnvironment}-${parInstance}'
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
    primaryScriptUri: 'https://raw.githubusercontent.com/frasermolyneux/bicep-modules/main/scripts/GrantApplicationAppRole.sh'
    arguments: '"portal-servers-integration-${parEnvironment}-${parInstance}-tests" "${parApiAppRegistrationName}" "ServiceAccount'
    retentionInterval: 'P1D'
    forceUpdateTag: updateTag
  }
}

// Outputs
output outServersIntegrationApiAppId string = appRegistrationTests.properties.outputs.applicationId
