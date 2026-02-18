output "web_app_name_v1" {
  value = azurerm_linux_web_app.app_v1.name
}

output "web_app_resource_group_v1" {
  value = azurerm_linux_web_app.app_v1.resource_group_name
}

output "api_management_name" {
  value = data.azurerm_api_management.api_management.name
}

output "api_management_resource_group_name" {
  value = data.azurerm_api_management.api_management.resource_group_name
}

output "api_management_product_id" {
  value = azurerm_api_management_product.api_product.product_id
}

output "api_version_set_id" {
  value = azurerm_api_management_api_version_set.api_version_set.name
}
