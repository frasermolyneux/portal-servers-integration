locals {
  // List of version files that exist (excluding legacy which is handled separately)
  version_files = fileset("../openapi", "openapi-v*.json")

  // Extract version strings from filenames (e.g., "v1", "v1.1", "v2")
  version_strings = [for file in local.version_files :
    trimsuffix(trimprefix(basename(file), "openapi-"), ".json")
  ]

  // Filter out legacy as it's handled in separate file
  versioned_apis = [for version in local.version_strings :
    version if version != "legacy"
  ]

  // Extract major versions from all discovered APIs (v1, v2, etc.)
  major_versions = toset([for version in local.versioned_apis :
    regex("^(v[0-9]+)", version)[0]
  ])

  // Dynamic API version formatting - automatically add .0 for versions without dots
  api_version_formats = { for version in local.versioned_apis :
    version => can(regex("\\.", version)) ? version : "${version}.0"
  }

  // Static mapping of major versions to function app configurations
  backend_mapping = {
    "v1" = {
      name         = local.web_app_name_v1
      hostname     = azurerm_linux_web_app.app_v1.default_hostname
      protocol     = "http"
      tls_validate = true
      description  = "Backend for v1.x APIs"
      exists       = true
    }
    # Add future versions with explicit entries here as needed
  }

  // Filter to only include function apps that have a major version in our discovered APIs
  filtered_backend_mapping = {
    for k, v in local.backend_mapping :
    k => v if contains(local.major_versions, k) && v.exists
  }

  // Default backend uses the lowest available major version (v1 in most cases)
  default_backend_version = length(local.filtered_backend_mapping) > 0 ? sort(keys(local.filtered_backend_mapping))[0] : "v1"
  default_backend         = length(local.filtered_backend_mapping) > 0 ? local.filtered_backend_mapping[local.default_backend_version] : local.backend_mapping["v1"]

  // Helper function to get the major version from a full version (e.g., "v1" from "v1.2")
  get_major_version = { for version in local.versioned_apis :
    version => regex("^(v[0-9]+)", version)[0]
  }

  // Get the backend configuration for a specific API version
  get_backend_for_version = { for version in local.versioned_apis :
    version => contains(keys(local.filtered_backend_mapping), local.get_major_version[version]) ?
    local.filtered_backend_mapping[local.get_major_version[version]] :
    local.default_backend
  }
}

// Data sources for versioned OpenAPI specification files
data "local_file" "openapi_versioned" {
  for_each = toset(local.versioned_apis)
  filename = "../openapi/openapi-${each.key}.json"
}

// Create backend for versioned APIs
resource "azurerm_api_management_backend" "versioned_api_backend" {
  for_each = local.filtered_backend_mapping

  name = each.value.name

  resource_group_name = data.azurerm_api_management.api_management.resource_group_name
  api_management_name = data.azurerm_api_management.api_management.name

  protocol    = lower(each.value.protocol)
  title       = each.value.name
  description = each.value.description
  url         = format("https://%s/api/%s", each.value.hostname, lower(local.api_version_formats[each.key]))

  tls {
    validate_certificate_chain = each.value.tls_validate
    validate_certificate_name  = each.value.tls_validate
  }
}

resource "azurerm_api_management_logger" "app_insights" {
  name                = "${var.workload_name}-application-insights"
  resource_group_name = data.azurerm_api_management.api_management.resource_group_name
  api_management_name = data.azurerm_api_management.api_management.name

  application_insights {
    instrumentation_key = data.azurerm_application_insights.app_insights.instrumentation_key
  }
}

// Dynamic versioned APIs that are discovered from OpenAPI spec files
resource "azurerm_api_management_api" "versioned_api" {
  for_each = toset(local.versioned_apis)

  name = "${local.servers_integration_api.api_management.root_path}-api-${replace(each.key, ".", "-")}"

  resource_group_name = data.azurerm_api_management.api_management.resource_group_name
  api_management_name = data.azurerm_api_management.api_management.name

  revision     = "1"
  display_name = "Servers Integration API"
  description  = "API for servers integration"
  path         = local.servers_integration_api.api_management.root_path
  protocols    = ["https"]

  subscription_required = false

  version        = each.key
  version_set_id = azurerm_api_management_api_version_set.api_version_set.id

  import {
    content_format = "openapi+json"
    content_value  = data.local_file.openapi_versioned[each.key].content
  }
}

// Add versioned APIs to the product
resource "azurerm_api_management_product_api" "versioned_api" {
  for_each = azurerm_api_management_api.versioned_api

  api_name   = each.value.name
  product_id = azurerm_api_management_product.api_product.product_id

  resource_group_name = data.azurerm_api_management.api_management.resource_group_name
  api_management_name = data.azurerm_api_management.api_management.name
}

// Configure policies for versioned APIs
resource "azurerm_api_management_api_policy" "versioned_api_policy" {
  for_each = azurerm_api_management_api.versioned_api

  api_name = each.value.name

  resource_group_name = data.azurerm_api_management.api_management.resource_group_name
  api_management_name = data.azurerm_api_management.api_management.name

  xml_content = <<XML
<policies>
  <inbound>
      <base/>
      <set-backend-service backend-id="${
  contains(keys(local.filtered_backend_mapping), local.get_major_version[each.key])
  ? azurerm_api_management_backend.versioned_api_backend[local.get_major_version[each.key]].name
  : azurerm_api_management_backend.versioned_api_backend[local.default_backend_version].name
}" />
      <set-variable name="rewriteUriTemplate" value="@((string)context.Request.OriginalUrl.Path.Substring(context.Api.Path.Length))" />
      <rewrite-uri template="@((string)context.Variables["rewriteUriTemplate"])" />
  </inbound>
  <backend>
      <forward-request />
  </backend>
  <outbound>
      <base/>
  </outbound>
  <on-error />
</policies>
XML

depends_on = [
  azurerm_api_management_backend.versioned_api_backend
]
}

resource "azurerm_api_management_api_diagnostic" "versioned_api_diagnostic" {
  for_each = azurerm_api_management_api.versioned_api

  api_name                 = each.value.name
  identifier               = "applicationinsights"
  resource_group_name      = data.azurerm_api_management.api_management.resource_group_name
  api_management_name      = data.azurerm_api_management.api_management.name
  api_management_logger_id = azurerm_api_management_logger.app_insights.id
}
