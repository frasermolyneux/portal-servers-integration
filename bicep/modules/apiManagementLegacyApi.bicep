targetScope = 'resourceGroup'

// Parameters
@description('The api management resource name')
param apiManagementName string

@description('The product ID to associate APIs with')
param productId string

@description('The version set ID to associate APIs with')
param versionSetId string

@description('A reference to the app insights resource')
param appInsightsRef object

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

// Reference the existing backend (created by versioned APIs module)
resource apiBackend 'Microsoft.ApiManagement/service/backends@2021-08-01' existing = {
  name: 'servers-integration-api-backend'
  parent: apiManagement
}

// Module Resources
resource legacyApi 'Microsoft.ApiManagement/service/apis@2021-08-01' = {
  name: 'servers-integration-api-legacy'
  parent: apiManagement

  properties: {
    apiRevision: '1.0'
    apiType: 'http'
    type: 'http'

    description: 'API for servers integration (legacy)'
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

    apiVersion: ''
    apiVersionSetId: versionSetId

    format: 'openapi+json'
    value: loadTextContent('../../openapi/openapi-legacy.json')
  }
}

// Associate legacy API with the product
resource legacyApiProduct 'Microsoft.ApiManagement/service/products/apis@2021-08-01' = {
  name: legacyApi.name
  parent: existingProduct
}

// Policy for legacy API
resource legacyApiPolicy 'Microsoft.ApiManagement/service/apis/policies@2021-08-01' = {
  name: 'policy'
  parent: legacyApi

  properties: {
    format: 'xml'
    value: '''
<policies>
  <inbound>
      <base/>
      <set-backend-service backend-id="${apiBackend.name}" />
      <set-variable name="rewriteUriTemplate" value="@("/api/v1" + context.Request.OriginalUrl.Path.Substring(context.Api.Path.Length))" />
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
    '''
  }

  dependsOn: [
    apiBackend
  ]
}

// Diagnostics for legacy API
resource legacyApiDiagnostics 'Microsoft.ApiManagement/service/apis/diagnostics@2021-08-01' = {
  name: 'applicationinsights'
  parent: legacyApi

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
