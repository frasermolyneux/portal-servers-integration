targetScope = 'resourceGroup'

// Parameters
@description('The api management resource name')
param apiManagementName string

@description('The backend hostname')
param backendHostname string

@description('The product ID to associate APIs with')
param productId string

@description('The version set ID to associate APIs with')
param versionSetId string

@description('A reference to the app insights resource')
param appInsightsRef object

// Variables
var openApiV1Content = loadTextContent('../../openapi/openapi-v1.json')

// Existing In-Scope Resources
resource apiManagement 'Microsoft.ApiManagement/service@2021-12-01-preview' existing = {
  name: apiManagementName
}

// Existing Out-Of-Scope Resources
resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsRef.Name
  scope: resourceGroup(appInsightsRef.SubscriptionId, appInsightsRef.ResourceGroupName)
}

// Reference the existing product
resource existingProduct 'Microsoft.ApiManagement/service/products@2021-08-01' existing = {
  name: productId
  parent: apiManagement
}

// Module Resources
resource apiBackend 'Microsoft.ApiManagement/service/backends@2021-08-01' = {
  name: 'servers-integration-api-backend'
  parent: apiManagement

  properties: {
    title: backendHostname
    description: backendHostname
    url: 'https://${backendHostname}/'
    protocol: 'http'
    properties: {}

    tls: {
      validateCertificateChain: true
      validateCertificateName: true
    }
  }
}

// Create V1 API
resource apiV1 'Microsoft.ApiManagement/service/apis@2021-08-01' = {
  name: 'servers-integration-api-v1'
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
      query: 'subscription-key'
    }

    apiVersion: 'v1'
    apiVersionSetId: versionSetId

    format: 'openapi+json'
    value: openApiV1Content
  }
}

// Associate V1 API with the product
resource apiV1Product 'Microsoft.ApiManagement/service/products/apis@2021-08-01' = {
  name: apiV1.name
  parent: existingProduct
}

// Policy for V1 API
resource apiV1Policy 'Microsoft.ApiManagement/service/apis/policies@2021-08-01' = {
  name: 'policy'
  parent: apiV1

  properties: {
    format: 'xml'
    value: format(
      '''
<policies>
  <inbound>
      <base/>
      <set-backend-service backend-id="{0}" />
      <set-variable name="rewriteUriTemplate" value="@(&quot;/api&quot; + context.Request.OriginalUrl.Path.Substring(context.Api.Path.Length))" />
      <rewrite-uri template="@((string)context.Variables[&quot;rewriteUriTemplate&quot;])" />
  </inbound>
  <backend>
      <forward-request />
  </backend>
  <outbound>
      <base/>
  </outbound>
  <on-error />
</policies>
    ''',
      apiBackend.name
    )
  }
}

// Diagnostics for V1 API
resource apiV1Diagnostics 'Microsoft.ApiManagement/service/apis/diagnostics@2021-08-01' = {
  name: 'applicationinsights'
  parent: apiV1

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
