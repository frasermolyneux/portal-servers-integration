param (
    $environment,
    $location
)

. "./.azure-pipelines/scripts/functions/CreateAppRegistration.ps1" `
    -applicationName "portal-servers-integration-api-$environment" `
    -appRoles "portal-servers-integration-api-approles.json"