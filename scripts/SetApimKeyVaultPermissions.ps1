param (
    $keyVaultName,
    $spnId
)

az keyvault set-policy --name $keyVaultName --spn $spnId --secret-permissions get | Out-Null