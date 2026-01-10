
resource "azurerm_api_management_product_policy" "api_product_policy" {
  product_id = azurerm_api_management_product.api_product.product_id

  resource_group_name = data.azurerm_api_management.api_management.resource_group_name
  api_management_name = data.azurerm_api_management.api_management.name

  xml_content = <<XML
<policies>
  <inbound>
      <base/>
      <cache-lookup vary-by-developer="false" vary-by-developer-groups="false" downstream-caching-type="none" />
      <validate-jwt header-name="Authorization" failed-validation-httpcode="401" failed-validation-error-message="JWT validation was unsuccessful" require-expiration-time="true" require-scheme="Bearer" require-signed-tokens="true">
          <openid-config url="https://login.microsoftonline.com/${data.azuread_client_config.current.tenant_id}/v2.0/.well-known/openid-configuration" />
          <audiences>
          <audience>${local.servers_integration_api.application.primary_identifier_uri}</audience>
          </audiences>
          <issuers>
              <issuer>https://sts.windows.net/${data.azuread_client_config.current.tenant_id}/</issuer>
          </issuers>
          <required-claims>
              <claim name="roles" match="any">
                <value>EventGenerator</value>
              </claim>
          </required-claims>
      </validate-jwt>
      <authentication-managed-identity resource="${local.servers_integration_api.application.primary_identifier_uri}" output-token-variable-name="msi-access-token" ${format(" client-id=\"%s\"", local.api_management_identity.client_id)} />
      <set-header name="Authorization" exists-action="override">
          <value>@("Bearer " + (string)context.Variables["msi-access-token"])</value>
      </set-header>
  </inbound>
  <backend>
      <forward-request />
  </backend>
  <outbound>
      <base/>
      <cache-store duration="3600" />
  </outbound>
  <on-error />
</policies>
XML
}
