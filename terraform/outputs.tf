output "web_app_name_v1" {
  value = azurerm_linux_web_app.app_v1.name
}

output "web_app_resource_group_v1" {
  value = azurerm_linux_web_app.app_v1.resource_group_name
}
