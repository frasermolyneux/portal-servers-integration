param (
    $environment,
    $location
)

. "scripts/functions/CreateAppRegistrationCredential.ps1" `
    -keyVaultName "kv-portal-$environment-$location" `
    -applicationName "portal-servers-api-$environment" `
    -secretPrefix "portal-servers-api-$environment" `
    -secretDisplayName "webportalsvrs"