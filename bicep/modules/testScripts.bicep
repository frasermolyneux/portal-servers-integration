targetScope = 'resourceGroup'

// Parameters
@description('The environment name (e.g. dev, tst, prd).')
param environment string

@description('The instance of the environment.')
param instance string

@description('The location to deploy the resources')
param location string

@description('The user assigned identity to use to execute the script')
param scriptIdentity string

@description('The name of the API application registration')
param apiAppRegistrationName string

// -- References
@description('A reference to the key vault resource')
param keyVaultRef object

// Variables
@description('Script is idempotent; execute each deployment to prevent drift')
param updateTag string = newGuid()

// Existing Out-Of-Scope Resources
resource keyVault 'Microsoft.KeyVault/vaults@2021-11-01-preview' existing = {
  name: keyVaultRef.Name
  scope: resourceGroup(keyVaultRef.SubscriptionId, keyVaultRef.ResourceGroupName)
}

// Module Resources
resource appRegistrationTests 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'script-app-registration-tests-${environment}-${instance}'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${scriptIdentity}': {}
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
    arguments: '"portal-servers-integration-${environment}-${instance}-tests"'
    retentionInterval: 'P1D'
    forceUpdateTag: updateTag
  }
}

resource appRegistrationTestsCredentials 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'script-app-registration-tests-credentials-${environment}-${instance}'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${scriptIdentity}': {}
    }
  }
  properties: {
    azCliVersion: '2.52.0'
    primaryScriptUri: 'https://raw.githubusercontent.com/frasermolyneux/bicep-modules/main/scripts/CreateAppRegistrationCredential.sh'
    arguments: '"${keyVault.name}" "portal-servers-integration-${environment}-${instance}-tests" "portal-servers-integration-${environment}-${instance}-tests" "portalserversintegrationtests"'
    retentionInterval: 'P1D'
    forceUpdateTag: updateTag
  }
}

resource appRegistrationTestsAppRole 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'script-app-registration-tests-approle-${environment}-${instance}'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${scriptIdentity}': {}
    }
  }
  properties: {
    azCliVersion: '2.52.0'
    primaryScriptUri: 'https://raw.githubusercontent.com/frasermolyneux/bicep-modules/main/scripts/GrantApplicationAppRole.sh'
    arguments: '"portal-servers-integration-${environment}-${instance}-tests" "${apiAppRegistrationName}" "ServiceAccount'
    retentionInterval: 'P1D'
    forceUpdateTag: updateTag
  }
}

// Outputs
output outServersIntegrationApiAppId string = appRegistrationTests.properties.outputs.applicationId
