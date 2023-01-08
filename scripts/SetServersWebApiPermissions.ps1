param (
    $environment,
    $webAppName
)

$identity = az webapp identity show --name $webAppName --resource-group "rg-platform-webapps-$environment-uksouth" | ConvertFrom-Json
$principalId = $identity.principalId

Write-Host "Web App '$webAppName' in resource group 'rg-platform-webapps-$environment-uksouth' has principal id '$principalId'"

. "scripts/functions/GrantRepositoryApiPermissionsToApp.ps1" -principalId $principalId -environment $environment


if ($environment -eq 'prd') {
    $identityStaging = az webapp identity show --name $webAppName --resource-group "rg-platform-webapps-$environment-uksouth" --slot 'staging' | ConvertFrom-Json
    $principalIdStaging = $identityStaging.principalId
    
    Write-Host "Web App Slot '$webAppName/staging' in resource group 'rg-platform-webapps-$environment-uksouth' has principal id '$principalIdStaging'"
    
    . "scripts/functions/GrantRepositoryApiPermissionsToApp.ps1" -principalId $principalIdStaging -environment $environment
}