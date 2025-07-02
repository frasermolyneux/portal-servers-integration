targetScope = 'resourceGroup'

// Parameters
@description('The environment for the resources')
param environment string

@description('The instance for the resources')
param instance string

@description('The api management resource name')
param apiManagementName string

@description('The tenant ID for JWT validation')
param tenantId string = tenant().tenantId

// Existing In-Scope Resources
resource apiManagement 'Microsoft.ApiManagement/service@2021-12-01-preview' existing = {
  name: apiManagementName
}

// Module Resources
resource apiVersionSet 'Microsoft.ApiManagement/service/apiVersionSets@2021-08-01' = {
  name: 'servers-integration-api'
  parent: apiManagement

  properties: {
    displayName: 'Servers Integration API'
    versioningScheme: 'Segment'
  }
}

// Variables for policy template
var audienceValue = 'api://portal-servers-integration-${environment}-${instance}'
var issuerValue = 'https://sts.windows.net/${tenantId}/'
var loginEndpoint = replace(az.environment().authentication.loginEndpoint, '/', '')
var openIdConfigUrl = 'https://${loginEndpoint}/${tenantId}/v2.0/.well-known/openid-configuration'

resource apiProduct 'Microsoft.ApiManagement/service/products@2021-08-01' = {
  name: 'servers-integration-api'
  parent: apiManagement

  properties: {
    displayName: 'Servers Integration API'
    subscriptionRequired: true
    approvalRequired: false
    state: 'published'
  }
}

resource apiProductPolicy 'Microsoft.ApiManagement/service/products/policies@2021-08-01' = {
  name: 'policy'
  parent: apiProduct

  properties: {
    format: 'xml'
    value: '''
<policies>
  <inbound>
      <base/>
      <cache-lookup vary-by-developer="false" vary-by-developer-groups="false" downstream-caching-type="none" />
      <validate-jwt header-name="Authorization" failed-validation-httpcode="401" failed-validation-error-message="JWT validation was unsuccessful" require-expiration-time="true" require-scheme="Bearer" require-signed-tokens="true">
          <openid-config url="${loginEndpoint}/${tenantId}/v2.0/.well-known/openid-configuration" />
          <audiences>
              <audience>${audienceValue}</audience>
          </audiences>
          <issuers>
              <issuer>${issuerValue}</issuer>
          </issuers>
          <required-claims>
              <claim name="roles" match="any">
                <value>ServiceAccount</value>
              </claim>
          </required-claims>
      </validate-jwt>
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
    '''
  }
}

// Outputs
output productId string = apiProduct.name
output versionSetId string = apiVersionSet.id
