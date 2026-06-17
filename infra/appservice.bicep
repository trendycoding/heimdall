// App Service module — Dev/Staging compute with lower SKUs
// Deployed for development and staging environments

@description('Name of the App Service')
param appName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (development, staging)')
@allowed(['development', 'staging'])
param environment string

@description('Container image to deploy')
param containerImage string = ''

@description('App Service Plan SKU name')
param skuName string = environment == 'development' ? 'B1' : 'S1'

@description('App Service Plan SKU tier')
param skuTier string = environment == 'development' ? 'Basic' : 'Standard'

@description('Application settings (environment variables)')
param appSettings array = []

@description('Log Analytics workspace ID for diagnostic settings (staging)')
param logAnalyticsWorkspaceId string = ''

@description('Virtual Network subnet ID for VNet integration (staging)')
param vnetSubnetId string = ''

@description('IP restriction rules (staging)')
param ipRestrictionRules array = []

@description('Tags to apply to resources')
param tags object = {}

// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: '${appName}-plan'
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    reserved: true // Required for Linux
  }
}

// App Service
resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: appName
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    virtualNetworkSubnetId: vnetSubnetId != '' ? vnetSubnetId : null
    siteConfig: {
      linuxFxVersion: containerImage != '' ? 'DOCKER|${containerImage}' : 'DOTNETCORE|8.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      alwaysOn: environment == 'staging'
      http20Enabled: true
      healthCheckPath: '/health'
      appSettings: appSettings
      ipSecurityRestrictions: environment == 'staging' && length(ipRestrictionRules) > 0 ? ipRestrictionRules : []
    }
  }
}

// Staging deployment slot for staging environment
resource stagingSlot 'Microsoft.Web/sites/slots@2022-09-01' = if (environment == 'staging') {
  parent: appService
  name: 'staging'
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: containerImage != '' ? 'DOCKER|${containerImage}' : 'DOTNETCORE|8.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      healthCheckPath: '/health'
      appSettings: appSettings
    }
  }
}

// Diagnostic settings for staging
resource appServiceDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (environment == 'staging' && logAnalyticsWorkspaceId != '') {
  name: '${appName}-diagnostics'
  scope: appService
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 30
        }
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 30
        }
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 30
        }
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 30
        }
      }
    ]
  }
}

@description('The default hostname of the App Service')
output hostname string = appService.properties.defaultHostName

@description('The resource ID of the App Service')
output id string = appService.id

@description('The principal ID of the system-assigned managed identity')
output principalId string = appService.identity.principalId

@description('The App Service Plan ID')
output planId string = appServicePlan.id
