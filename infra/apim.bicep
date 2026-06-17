// ============================================================================
// Heimdall Access — Azure API Management
// ============================================================================

@description('Azure region for the API Management instance')
param location string

@description('Resource naming suffix (e.g., heimdall-dev)')
param resourceSuffix string

@description('API Management SKU name')
@allowed([
  'Developer'
  'Basic'
  'Standard'
  'Premium'
  'Consumption'
])
param skuName string

@description('API Management SKU capacity (number of units)')
param skuCapacity int = 1

@description('Publisher email address')
param publisherEmail string

@description('Publisher organization name')
param publisherName string

@description('Application Insights instrumentation key for APIM logging')
param appInsightsInstrumentationKey string = ''

@description('Application Insights resource ID')
param appInsightsId string = ''

@description('Resource tags')
param tags object = {}

// ============================================================================
// Resources
// ============================================================================

resource apim 'Microsoft.ApiManagement/service@2023-03-01-preview' = {
  name: 'apim-${resourceSuffix}'
  location: location
  tags: tags
  sku: {
    name: skuName
    capacity: skuCapacity
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
    customProperties: {
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls10': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls11': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Ssl30': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Tls10': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Tls11': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Ssl30': 'False'
    }
  }
}

resource apimLogger 'Microsoft.ApiManagement/service/loggers@2023-03-01-preview' = if (!empty(appInsightsInstrumentationKey)) {
  parent: apim
  name: 'appinsights-logger'
  properties: {
    loggerType: 'applicationInsights'
    resourceId: appInsightsId
    credentials: {
      instrumentationKey: appInsightsInstrumentationKey
    }
  }
}

resource apimDiagnostics 'Microsoft.ApiManagement/service/diagnostics@2023-03-01-preview' = if (!empty(appInsightsInstrumentationKey)) {
  parent: apim
  name: 'applicationinsights'
  properties: {
    loggerId: apimLogger.id
    alwaysLog: 'allErrors'
    sampling: {
      percentage: 100
      samplingType: 'fixed'
    }
    frontend: {
      request: {
        body: {
          bytes: 0
        }
      }
      response: {
        body: {
          bytes: 0
        }
      }
    }
    backend: {
      request: {
        body: {
          bytes: 0
        }
      }
      response: {
        body: {
          bytes: 0
        }
      }
    }
  }
}

resource heimdallApi 'Microsoft.ApiManagement/service/apis@2023-03-01-preview' = {
  parent: apim
  name: 'heimdall-access-api'
  properties: {
    displayName: 'Heimdall Access API'
    path: 'api'
    protocols: [
      'https'
    ]
    subscriptionRequired: false
    apiType: 'http'
  }
}

// ============================================================================
// Outputs
// ============================================================================

output apimId string = apim.id
output apimName string = apim.name
output gatewayUrl string = apim.properties.gatewayUrl
