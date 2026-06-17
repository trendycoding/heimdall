// ============================================================================
// Heimdall Access — Azure App Configuration
// ============================================================================

@description('Azure region for the App Configuration')
param location string

@description('Resource naming suffix (e.g., heimdall-dev)')
param resourceSuffix string

@description('Platform display name to store in App Configuration')
param platformDisplayName string = 'Heimdall Access'

@description('Deployment environment label')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string

@description('Resource tags')
param tags object = {}

// ============================================================================
// Resources
// ============================================================================

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' = {
  name: 'appcs-${resourceSuffix}'
  location: location
  tags: tags
  sku: {
    name: environment == 'prod' ? 'standard' : 'free'
  }
  properties: {
    disableLocalAuth: false
    enablePurgeProtection: environment == 'prod'
    softDeleteRetentionInDays: environment == 'prod' ? 7 : 1
  }
}

resource platformDisplayNameKey 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = {
  parent: appConfig
  name: 'Platform:DisplayName$${environment}'
  properties: {
    value: platformDisplayName
    contentType: 'text/plain'
  }
}

resource sentinelKey 'Microsoft.AppConfiguration/configurationStores/keyValues@2023-03-01' = {
  parent: appConfig
  name: 'Sentinel$${environment}'
  properties: {
    value: '1'
    contentType: 'text/plain'
  }
}

// ============================================================================
// Outputs
// ============================================================================

output configStoreId string = appConfig.id
output configStoreName string = appConfig.name
output endpoint string = appConfig.properties.endpoint
