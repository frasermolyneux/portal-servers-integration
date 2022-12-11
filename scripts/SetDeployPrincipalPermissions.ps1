param (
    $keyVaultName,
    $spnId
)

$keyVaultId = az keyvault show --name $keyVaultName --output tsv --query id

# https://learn.microsoft.com/en-gb/azure/role-based-access-control/built-in-roles#key-vault-secrets-officer
az role assignment create --assignee $spnId --role 4633458b-17de-408a-b874-0445c86b69e6 --scope $keyVaultId