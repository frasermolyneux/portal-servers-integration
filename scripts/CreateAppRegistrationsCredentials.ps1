param (
    $environment,
    $location,
    $keyVaultName
)

. "scripts/functions/CreateAppRegistrationCredential.ps1" `
    -keyVaultName $keyVaultName `
    -applicationName "portal-servers-integration-$environment" `
    -secretPrefix "portal-servers-integration-$environment" `
    -secretDisplayName "portalserversintegration"