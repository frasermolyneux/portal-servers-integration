resource "azurerm_api_management_api_version_set" "api_version_set" {
  name = local.servers_integration_api.api_management.root_path

  resource_group_name = data.azurerm_api_management.api_management.resource_group_name
  api_management_name = data.azurerm_api_management.api_management.name

  display_name      = "Servers Integration API"
  versioning_scheme = "Segment"
}
