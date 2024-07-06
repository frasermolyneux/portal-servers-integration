targetScope = 'resourceGroup'

// Parameters
param parEnvironment string
param parInstance string

param parApiManagementName string
param parBackendHostname string

@description('The app insights reference')
param parAppInsightsRef object

// Existing In-Scope Resources
resource apiManagement 'Microsoft.ApiManagement/service@2021-12-01-preview' existing = {
  name: parApiManagementName
}

// Existing Out-Of-Scope Resources
resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: parAppInsightsRef.Name
  scope: resourceGroup(parAppInsightsRef.SubscriptionId, parAppInsightsRef.ResourceGroupName)
}

// Module Resources
resource apiBackend 'Microsoft.ApiManagement/service/backends@2021-08-01' = {
  name: 'servers-integration-api-backend'
  parent: apiManagement

  properties: {
    title: parBackendHostname
    description: parBackendHostname
    url: 'https://${parBackendHostname}/'
    protocol: 'http'
    properties: {}

    tls: {
      validateCertificateChain: true
      validateCertificateName: true
    }
  }
}

resource apiActiveBackendNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-08-01' = {
  name: 'servers-integration-api-active-backend'
  parent: apiManagement

  properties: {
    displayName: 'servers-integration-api-active-backend'
    value: apiBackend.name
    secret: false
  }
}

resource apiAudienceNamedValue 'Microsoft.ApiManagement/service/namedValues@2021-08-01' = {
  name: 'servers-integration-api-audience'
  parent: apiManagement

  properties: {
    displayName: 'servers-integration-api-audience'
    value: 'api://portal-servers-integration-${parEnvironment}-${parInstance}'
    secret: false
  }
}

resource api 'Microsoft.ApiManagement/service/apis@2021-08-01' = {
  name: 'servers-integration-api'
  parent: apiManagement

  properties: {
    apiRevision: '1.0'
    apiType: 'http'
    type: 'http'

    description: 'API for servers integration'
    displayName: 'Servers Integration API'
    path: 'servers-integration'

    protocols: [
      'https'
    ]

    subscriptionRequired: true
    subscriptionKeyParameterNames: {
      header: 'Ocp-Apim-Subscription-Key'
    }

    format: 'openapi+json'
    value: loadTextContent('./../../servers-integration-api.openapi+json.json')
  }
}

resource apiPolicy 'Microsoft.ApiManagement/service/apis/policies@2021-08-01' = {
  name: 'policy'
  parent: api
  properties: {
    format: 'xml'
    value: '''
<policies>
  <inbound>
      <base/>
      <set-backend-service backend-id="{{servers-integration-api-active-backend}}" />
      <cache-lookup vary-by-developer="false" vary-by-developer-groups="false" downstream-caching-type="none" />
      <validate-jwt header-name="Authorization" failed-validation-httpcode="401" failed-validation-error-message="JWT validation was unsuccessful" require-expiration-time="true" require-scheme="Bearer" require-signed-tokens="true">
          <openid-config url="https://login.microsoftonline.com/e56a6947-bb9a-4a6e-846a-1f118d1c3a14/v2.0/.well-known/openid-configuration" />
          <audiences>
              <audience>{{servers-integration-api-audience}}</audience>
          </audiences>
          <issuers>
              <issuer>https://sts.windows.net/e56a6947-bb9a-4a6e-846a-1f118d1c3a14/</issuer>
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

  dependsOn: [
    apiActiveBackendNamedValue
    apiAudienceNamedValue
  ]
}

resource apiDiagnostics 'Microsoft.ApiManagement/service/apis/diagnostics@2021-08-01' = {
  name: 'applicationinsights'
  parent: api

  properties: {
    alwaysLog: 'allErrors'

    httpCorrelationProtocol: 'W3C'
    logClientIp: true
    loggerId: resourceId('Microsoft.ApiManagement/service/loggers', apiManagement.name, appInsights.name)
    operationNameFormat: 'Name'

    sampling: {
      percentage: 100
      samplingType: 'fixed'
    }

    verbosity: 'information'
  }
}
