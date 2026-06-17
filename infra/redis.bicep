// Azure Cache for Redis module
// Basic for staging, Standard/Premium for production, not deployed for development

@description('Name of the Redis cache')
param redisName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name')
@allowed(['staging', 'production'])
param environment string

@description('Redis SKU name')
param skuName string = environment == 'staging' ? 'Basic' : 'Premium'

@description('Redis SKU family')
param skuFamily string = environment == 'staging' ? 'C' : 'P'

@description('Redis cache capacity (size)')
param skuCapacity int = environment == 'staging' ? 0 : 1

@description('Enable non-SSL port (should be false for production)')
param enableNonSslPort bool = false

@description('Minimum TLS version')
param minimumTlsVersion string = '1.2'

@description('Subnet ID for private networking (production only)')
param subnetId string = ''

@description('Log Analytics workspace ID for diagnostics')
param logAnalyticsWorkspaceId string = ''

@description('Tags to apply to resources')
param tags object = {}

// Redis configuration per environment
var redisConfigStaging = {
  'maxmemory-policy': 'allkeys-lru'
}

var redisConfigProduction = {
  'maxmemory-policy': 'allkeys-lru'
  'maxmemory-reserved': '125'
  'maxfragmentationmemory-reserved': '125'
}

// Azure Cache for Redis
resource redisCache 'Microsoft.Cache/redis@2023-08-01' = {
  name: redisName
  location: location
  tags: tags
  properties: {
    sku: {
      name: skuName
      family: skuFamily
      capacity: skuCapacity
    }
    enableNonSslPort: enableNonSslPort
    minimumTlsVersion: minimumTlsVersion
    redisConfiguration: environment == 'production' ? redisConfigProduction : redisConfigStaging
    publicNetworkAccess: environment == 'production' && subnetId != '' ? 'Disabled' : 'Enabled'
    subnetId: environment == 'production' && subnetId != '' ? subnetId : null
    replicasPerMaster: environment == 'production' ? 1 : null
    replicasPerPrimary: environment == 'production' ? 1 : null
  }
}

// Diagnostic settings — send metrics and logs to Log Analytics
resource redisDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (logAnalyticsWorkspaceId != '') {
  name: '${redisName}-diagnostics'
  scope: redisCache
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'production' ? 90 : 30
        }
      }
    ]
    logs: [
      {
        category: 'ConnectedClientList'
        enabled: environment == 'production'
        retentionPolicy: {
          enabled: true
          days: 30
        }
      }
    ]
  }
}

@description('The hostname of the Redis cache')
output hostname string = redisCache.properties.hostName

@description('The SSL port of the Redis cache')
output sslPort int = redisCache.properties.sslPort

@description('The resource ID of the Redis cache')
output id string = redisCache.id

@description('The primary connection string')
output primaryConnectionString string = '${redisCache.properties.hostName}:${redisCache.properties.sslPort},password=${redisCache.listKeys().primaryKey},ssl=True,abortConnect=False'
