param (
    $keyVaultName,
    $spnId
)

$keyVaultId = az keyvault show --name $keyVaultName --output tsv --query id

# https://learn.microsoft.com/en-gb/azure/role-based-access-control/built-in-roles#key-vault-secrets-officer
az role assignment create --assignee $spnId --role b86a8fe4-44ce-4948-aee5-eccb2c155cd7 --scope $keyVaultId