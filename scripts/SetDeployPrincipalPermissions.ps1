param (
    $environment, 
    $keyVaultName
)

$principalId = az apim show --resource-group rg-platform-apim-$environment-uksouth --name apim-mx-platform-$environment-uksouth --output tsv --query 'identity.principalId'

az keyvault set-policy --name $keyVaultName --spn $principalId --secret-permissions get set | Out-Null