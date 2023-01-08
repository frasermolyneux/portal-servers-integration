param (
    $principalId,
    $environment
)

$repositoryApiName = "portal-repository-$environment"
$repositoryApiId = (az ad app list --filter "displayName eq '$repositoryApiName'" --query '[].appId') | ConvertFrom-Json
$repositoryApiSpnId = (az ad sp list --filter "appId eq '$repositoryApiId'" --query '[0].id') | ConvertFrom-Json
$repositoryApiSpn = (az rest -m GET -u https://graph.microsoft.com/v1.0/servicePrincipals/$repositoryApiSpnId) | ConvertFrom-Json
$appRoleId = ($repositoryApiSpn.appRoles | Where-Object { $_.displayName -eq "ServiceAccount" }).id

$permissions = (az rest -m GET -u https://graph.microsoft.com/v1.0/servicePrincipals/$principalId/appRoleAssignments) | ConvertFrom-Json
if ($null -eq ($permissions.value | Where-Object { $_.appRoleId -eq $appRoleId })) {
    az rest -m POST -u https://graph.microsoft.com/v1.0/servicePrincipals/$principalId/appRoleAssignments -b "{'principalId': '$principalId', 'resourceId': '$repositoryApiSpnId','appRoleId': '$appRoleId'}"
}