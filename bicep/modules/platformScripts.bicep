targetScope = 'resourceGroup'

// Parameters
@description('The environment for the resources')
param environment string

@description('The instance of the environment.')
param instance string

@description('The location to deploy the resources')
param location string

@description('The user assigned identity to use to execute the script')
param scriptIdentity string

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
resource appRegistration 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'script-app-registration-${environment}-${instance}'
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
    arguments: '"portal-servers-integration-${environment}-${instance}"'
    retentionInterval: 'P1D'
    forceUpdateTag: updateTag
  }
}

resource appRegistrationCredentials 'Microsoft.Resources/deploymentScripts@2023-08-01' = {
  name: 'script-app-registration-credentials-${environment}-${instance}'
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
    arguments: '"${keyVault.name}" "portal-servers-integration-${environment}-${instance}" "portal-servers-integration-${environment}-${instance}" "portalserversintegration"'
    retentionInterval: 'P1D'
    forceUpdateTag: updateTag
  }
}

// Outputs
output outAppRegistrationName string = appRegistration.properties.outputs.applicationName
output outServersIntegrationApiAppId string = appRegistration.properties.outputs.applicationId
