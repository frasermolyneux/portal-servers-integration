resource "azurerm_api_management_logger" "app_insights" {
  name                = "${var.workload_name}-application-insights"
  resource_group_name = data.azurerm_api_management.api_management.resource_group_name
  api_management_name = data.azurerm_api_management.api_management.name

  application_insights {
    instrumentation_key = data.azurerm_application_insights.app_insights.instrumentation_key
  }
}

