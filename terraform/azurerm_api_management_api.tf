resource "azurerm_api_management_logger" "app_insights" {
  name                = "${var.workload_name}-application-insights"
  resource_group_name = data.azurerm_api_management.api_management.resource_group_name
  api_management_name = data.azurerm_api_management.api_management.name

  application_insights {
    instrumentation_key = data.azurerm_application_insights.app_insights.instrumentation_key
  }
}

resource "azurerm_api_management_diagnostic" "app_insights" {
  identifier               = "applicationinsights"
  resource_group_name      = data.azurerm_api_management.api_management.resource_group_name
  api_management_name      = data.azurerm_api_management.api_management.name
  api_management_logger_id = azurerm_api_management_logger.app_insights.id
}

# The following removed blocks tell Terraform to drop previously-managed API
# resources from state without destroying them in Azure. These resources are
# now managed by the deploy workflow via `az apim api import`.
# These blocks can be safely removed after the first successful deploy.

removed {
  from = azurerm_api_management_api.versioned_api
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_api_management_product_api.versioned_api
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_api_management_api_policy.versioned_api_policy
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_api_management_api_diagnostic.versioned_api_diagnostic
  lifecycle {
    destroy = false
  }
}

removed {
  from = azurerm_api_management_backend.versioned_api_backend
  lifecycle {
    destroy = false
  }
}
