resource "azurerm_linux_web_app" "app_v1" {
  name = local.web_app_name_v1

  tags = var.tags

  resource_group_name = data.azurerm_resource_group.rg.name
  location            = data.azurerm_resource_group.rg.location

  service_plan_id = data.azurerm_service_plan.sp.id

  https_only = true

  identity {
    type         = "UserAssigned"
    identity_ids = [local.servers_integration_identity.id]
  }

  key_vault_reference_identity_id = local.servers_integration_identity.id

  site_config {
    application_stack {
      dotnet_version = "9.0"
    }

    ftps_state = "Disabled"
    always_on  = true

    minimum_tls_version = "1.2"

    health_check_path                 = "/v1.0/health"
    health_check_eviction_time_in_min = 5
  }

  app_settings = {
    "AzureAppConfiguration__Endpoint"                = local.app_configuration_endpoint
    "AzureAppConfiguration__ManagedIdentityClientId" = local.servers_integration_identity.client_id
    "AzureAppConfiguration__Environment"             = var.environment

    "AZURE_CLIENT_ID" = local.servers_integration_identity.client_id

    "minTlsVersion"= "1.2"
    "APPLICATIONINSIGHTS_CONNECTION_STRING"      = data.azurerm_application_insights.app_insights.connection_string
    "ApplicationInsightsAgent_EXTENSION_VERSION" = "~3"
    "ASPNETCORE_ENVIRONMENT"                     = var.environment == "prd" ? "Production" : "Development"
    "WEBSITE_RUN_FROM_PACKAGE"                   = "1"

    // https://learn.microsoft.com/en-us/azure/azure-monitor/profiler/profiler-azure-functions#app-settings-for-enabling-profiler
    "APPINSIGHTS_PROFILERFEATURE_VERSION"  = "1.0.0"
    "DiagnosticServices_EXTENSION_VERSION" = "~3"
  }
}
