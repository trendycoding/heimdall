targetScope = 'resourceGroup'

// ============================================================================
// Heimdall Access — Main Infrastructure Orchestration
// ============================================================================

@description('Deployment environment')
@allowed([
  'dev'
  'staging'
  'prod'
])
param environment string

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Base name prefix for resources')
param baseName string = 'heimdall'

@description('SQL administrator login')
@secure()
param sqlAdminLogin string

@description('SQL administrator password')
@secure()
param sqlAdminPassword string

@description('Platform display name for App Configuration')
param platformDisplayName string = 'Heimdall Access'

@description('Email address for Azure Monitor alert notifications')
param alertNotificationEmail string = 'ops@heimdall.io'

// ============================================================================
// Environment-specific configuration
// ============================================================================

var envConfig = {
  dev: {
    sqlSkuName: 'Basic'
    sqlSkuTier: 'Basic'
    sqlSkuCapacity: 5
    sqlMaxSizeBytes: 2147483648 // 2 GB
    sqlBackupRetentionDays: 7
    keyVaultSkuName: 'standard'
    apimSkuName: 'Consumption'
    apimSkuCapacity: 0
    appInsightsRetentionDays: 30
    logAnalyticsRetentionDays: 30
  }
  staging: {
    sqlSkuName: 'Basic'
    sqlSkuTier: 'Basic'
    sqlSkuCapacity: 5
    sqlMaxSizeBytes: 2147483648 // 2 GB
    sqlBackupRetentionDays: 7
    keyVaultSkuName: 'standard'
    apimSkuName: 'Consumption'
    apimSkuCapacity: 0
    appInsightsRetentionDays: 30
    logAnalyticsRetentionDays: 30
  }
  prod: {
    sqlSkuName: 'S1'
    sqlSkuTier: 'Standard'
    sqlSkuCapacity: 20
    sqlMaxSizeBytes: 268435456000 // 250 GB
    sqlBackupRetentionDays: 35
    keyVaultSkuName: 'standard'
    apimSkuName: 'Standard'
    apimSkuCapacity: 1
    appInsightsRetentionDays: 90
    logAnalyticsRetentionDays: 90
  }
}

var config = envConfig[environment]
var resourceSuffix = '${baseName}-${environment}'

var commonTags = {
  Environment: environment
  Project: 'Heimdall Access'
  ManagedBy: 'Bicep'
}

// ============================================================================
// Module Deployments
// ============================================================================

module monitoring 'monitoring.bicep' = {
  name: 'monitoring-${environment}'
  params: {
    location: location
    resourceSuffix: resourceSuffix
    appInsightsRetentionDays: config.appInsightsRetentionDays
    logAnalyticsRetentionDays: config.logAnalyticsRetentionDays
    tags: commonTags
  }
}

module alerts 'alerts.bicep' = {
  name: 'alerts-${environment}'
  params: {
    location: location
    resourceSuffix: resourceSuffix
    appInsightsId: monitoring.outputs.appInsightsId
    alertNotificationEmail: alertNotificationEmail
    tags: commonTags
  }
}

module sql 'sql.bicep' = {
  name: 'sql-${environment}'
  params: {
    location: location
    resourceSuffix: resourceSuffix
    administratorLogin: sqlAdminLogin
    administratorPassword: sqlAdminPassword
    skuName: config.sqlSkuName
    skuTier: config.sqlSkuTier
    skuCapacity: config.sqlSkuCapacity
    maxSizeBytes: config.sqlMaxSizeBytes
    backupRetentionDays: config.sqlBackupRetentionDays
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    tags: commonTags
  }
}

module keyVault 'keyvault.bicep' = {
  name: 'keyvault-${environment}'
  params: {
    location: location
    resourceSuffix: resourceSuffix
    enablePurgeProtection: environment == 'prod'
    enableRbacAuthorization: true
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    tags: commonTags
  }
}

module appConfig 'appconfig.bicep' = {
  name: 'appconfig-${environment}'
  params: {
    location: location
    resourceSuffix: resourceSuffix
    platformDisplayName: platformDisplayName
    environment: environment
    tags: commonTags
  }
}

module apim 'apim.bicep' = {
  name: 'apim-${environment}'
  params: {
    location: location
    resourceSuffix: resourceSuffix
    skuName: config.apimSkuName
    skuCapacity: config.apimSkuCapacity
    publisherEmail: 'admin@${baseName}.io'
    publisherName: platformDisplayName
    appInsightsInstrumentationKey: monitoring.outputs.appInsightsInstrumentationKey
    appInsightsId: monitoring.outputs.appInsightsId
    tags: commonTags
  }
}

// ============================================================================
// Compute, Networking, Storage, Redis, and Identity Modules
// ============================================================================

@description('Container image for the API (used in staging and production)')
param containerImage string = 'mcr.microsoft.com/dotnet/aspnet:8.0'

@description('Allowed IP addresses for staging network restrictions')
param allowedIpAddresses array = []

// --- Managed Identity ---

module identity 'identity.bicep' = {
  name: 'identity-${environment}'
  params: {
    identityName: '${resourceSuffix}-id'
    location: location
    environment: environment == 'dev' ? 'development' : environment == 'staging' ? 'staging' : 'production'
    keyVaultId: keyVault.outputs.vaultId
    storageAccountId: storage.outputs.id
    sqlServerId: sql.outputs.serverId
    appConfigId: appConfig.outputs.configStoreId
    redisCacheId: environment != 'dev' ? redis.outputs.id : ''
    tags: commonTags
  }
}

// --- Storage Account ---

module storage 'storage.bicep' = {
  name: 'storage-${environment}'
  params: {
    storageAccountName: replace('${baseName}${environment}stg', '-', '')
    location: location
    environment: environment == 'dev' ? 'development' : environment == 'staging' ? 'staging' : 'production'
    skuName: environment == 'prod' ? 'Standard_GRS' : 'Standard_LRS'
    blobSoftDeleteRetentionDays: environment == 'prod' ? 35 : (environment == 'staging' ? 7 : 7)
    enableBlobSoftDelete: environment != 'dev'
    tags: commonTags
  }
}

// --- Azure Cache for Redis (staging and production only) ---

module redis 'redis.bicep' = if (environment != 'dev') {
  name: 'redis-${environment}'
  params: {
    redisName: '${resourceSuffix}-redis'
    location: location
    environment: environment == 'staging' ? 'staging' : 'production'
    skuName: environment == 'staging' ? 'Basic' : 'Premium'
    skuFamily: environment == 'staging' ? 'C' : 'P'
    skuCapacity: environment == 'staging' ? 0 : 1
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    tags: commonTags
  }
}

// --- Networking (production only — staging uses IP restrictions at App Service level) ---

module network 'network.bicep' = if (environment == 'prod') {
  name: 'network-${environment}'
  params: {
    namePrefix: resourceSuffix
    location: location
    environment: 'production'
    allowedIpAddresses: allowedIpAddresses
    sqlServerId: sql.outputs.serverId
    storageAccountId: storage.outputs.id
    redisId: environment != 'dev' ? redis.outputs.id : ''
    keyVaultId: keyVault.outputs.vaultId
    tags: commonTags
  }
}

// --- Compute: App Service (dev / staging) ---

module appService 'appservice.bicep' = if (environment != 'prod') {
  name: 'appservice-${environment}'
  params: {
    appName: '${resourceSuffix}-app'
    location: location
    environment: environment == 'dev' ? 'development' : 'staging'
    containerImage: containerImage
    skuName: environment == 'dev' ? 'F1' : 'B1'
    skuTier: environment == 'dev' ? 'Free' : 'Basic'
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    vnetSubnetId: ''
    appSettings: [
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: monitoring.outputs.appInsightsConnectionString }
      { name: 'AZURE_APPCONFIG_ENDPOINT', value: appConfig.outputs.endpoint }
      { name: 'AZURE_CLIENT_ID', value: identity.outputs.clientId }
    ]
    tags: commonTags
  }
}

// --- Compute: Container Apps (production only) ---

module containerApp 'containerapp.bicep' = if (environment == 'prod') {
  name: 'containerapp-${environment}'
  params: {
    environmentName: '${resourceSuffix}-cae'
    appName: '${resourceSuffix}-app'
    location: location
    containerImage: containerImage
    minReplicas: 2
    maxReplicas: 10
    cpuCores: '0.5'
    memory: '1Gi'
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    managedIdentityId: identity.outputs.id
    envVars: [
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: monitoring.outputs.appInsightsConnectionString }
      { name: 'AZURE_APPCONFIG_ENDPOINT', value: appConfig.outputs.endpoint }
      { name: 'AZURE_CLIENT_ID', value: identity.outputs.clientId }
    ]
    tags: commonTags
  }
}

// ============================================================================
// Outputs
// ============================================================================

output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output keyVaultUri string = keyVault.outputs.vaultUri
output keyVaultName string = keyVault.outputs.vaultName
output appConfigEndpoint string = appConfig.outputs.endpoint
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString
output appInsightsInstrumentationKey string = monitoring.outputs.appInsightsInstrumentationKey
output logAnalyticsWorkspaceId string = monitoring.outputs.logAnalyticsWorkspaceId
output apimGatewayUrl string = apim.outputs.gatewayUrl
output apimName string = apim.outputs.apimName
output identityClientId string = identity.outputs.clientId
output identityPrincipalId string = identity.outputs.principalId
output storageAccountName string = storage.outputs.name
output storageTableEndpoint string = storage.outputs.tableEndpoint
