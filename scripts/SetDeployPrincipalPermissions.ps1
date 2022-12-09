param (
    $keyVaultName
)

az keyvault set-policy --name $keyVaultName --spn (az ad signed-in-user show --query id --output tsv) --secret-permissions get set | Out-Null