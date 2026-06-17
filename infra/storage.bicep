// Azure Storage Account module
// Used for Table Storage (permission snapshots, logs) and Blob storage (static assets)

@description('Name of the storage account (must be globally unique, 3-24 lowercase alphanumeric)')
param storageAccountName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name')
@allowed(['development', 'staging', 'production'])
param environment string

@description('Storage account SKU')
param skuName string = environment == 'production' ? 'Standard_GRS' : 'Standard_LRS'

@description('Storage account kind')
param kind string = 'StorageV2'

@description('Enable blob soft delete')
param enableBlobSoftDelete bool = environment != 'development'

@description('Blob soft delete retention days')
param blobSoftDeleteRetentionDays int = environment == 'production' ? 35 : 7

@description('Enable table storage encryption')
param enableTableEncryption bool = true

@description('Allowed IP ranges for network rules (staging)')
param allowedIpRanges array = []

@description('Virtual network subnet IDs for network rules')
param virtualNetworkSubnetIds array = []

@description('Log Analytics workspace ID for diagnostics')
param logAnalyticsWorkspaceId string = ''

@description('Tags to apply to resources')
param tags object = {}

// Network ACL rules
var ipRules = [for ip in allowedIpRanges: {
  value: ip
  action: 'Allow'
}]

var vnetRules = [for subnetId in virtualNetworkSubnetIds: {
  id: subnetId
  action: 'Allow'
}]

var networkAclsDev = {
  defaultAction: 'Allow'
}

var networkAclsRestricted = {
  defaultAction: environment == 'production' ? 'Deny' : 'Allow'
  ipRules: ipRules
  virtualNetworkRules: vnetRules
  bypass: 'AzureServices'
}

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  tags: tags
  kind: kind
  sku: {
    name: skuName
  }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    encryption: {
      services: {
        blob: {
          enabled: true
        }
        table: {
          enabled: enableTableEncryption
        }
        file: {
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
    networkAcls: environment == 'development' ? networkAclsDev : networkAclsRestricted
  }
}

// Blob Services with soft delete
resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: enableBlobSoftDelete
      days: blobSoftDeleteRetentionDays
    }
    containerDeleteRetentionPolicy: {
      enabled: enableBlobSoftDelete
      days: blobSoftDeleteRetentionDays
    }
  }
}

// Table Services
resource tableServices 'Microsoft.Storage/storageAccounts/tableServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

// Permission Snapshots table
resource permissionSnapshotsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  parent: tableServices
  name: 'PermissionSnapshots'
}

// Access Detail Snapshots table
resource accessDetailSnapshotsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  parent: tableServices
  name: 'AccessDetailSnapshots'
}

// API Call Logs table
resource apiCallLogsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  parent: tableServices
  name: 'ApiCallLogs'
}

// Audit Logs table
resource auditLogsTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  parent: tableServices
  name: 'AuditLogs'
}

// Lifecycle management policy for production — auto-archive old log data
resource lifecyclePolicy 'Microsoft.Storage/storageAccounts/managementPolicies@2023-01-01' = if (environment == 'production') {
  name: 'default'
  parent: storageAccount
  properties: {
    policy: {
      rules: [
        {
          name: 'archiveOldBlobs'
          enabled: true
          type: 'Lifecycle'
          definition: {
            actions: {
              baseBlob: {
                tierToCool: {
                  daysAfterModificationGreaterThan: 30
                }
                tierToArchive: {
                  daysAfterModificationGreaterThan: 90
                }
              }
            }
            filters: {
              blobTypes: ['blockBlob']
              prefixMatch: ['logs/', 'archive/']
            }
          }
        }
      ]
    }
  }
}

// Diagnostic settings — send storage metrics to Log Analytics
resource storageDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (logAnalyticsWorkspaceId != '' && environment != 'development') {
  name: '${storageAccountName}-diagnostics'
  scope: storageAccount
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    metrics: [
      {
        category: 'Transaction'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: environment == 'production' ? 90 : 30
        }
      }
    ]
  }
}

@description('The resource ID of the Storage Account')
output id string = storageAccount.id

@description('The name of the Storage Account')
output name string = storageAccount.name

@description('The primary endpoint for Table Storage')
output tableEndpoint string = storageAccount.properties.primaryEndpoints.table

@description('The primary endpoint for Blob Storage')
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob

@description('The primary connection string')
output connectionString string = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=core.windows.net'
