param (
    $environment,
    $location
)

. "scripts/functions/CreateAppRegistration.ps1" `
    -applicationName "portal-servers-integration-$environment" `
    -appRoles "portal-servers-integration-approles.json"