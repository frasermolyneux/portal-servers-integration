param (
    $environment,
    $location,
    $keyVaultName
)

. "scripts/functions/CreateAppRegistrationCredential.ps1" `
    -keyVaultName $keyVaultName `
    -applicationName "portal-servers-integration-api-$environment" `
    -secretPrefix "portal-servers-integration-api-$environment" `
    -secretDisplayName "portalserversintegrationwebapi"