// ============================================================================
// Heimdall Access — Azure Key Vault
// ============================================================================

@description('Azure region for the Key Vault')
param location string

@description('Resource naming suffix (e.g., heimdall-dev)')
param resourceSuffix string

@description('Enable purge protection (recommended for production)')
param enablePurgeProtection bool = false

@description('Log Analytics Workspace ID for diagnostics')
param logAnalyticsWorkspaceId string

@description('Enable RBAC authorization model (true) or access policies (false)')
param enableRbacAuthorization bool = true

@description('Object IDs of Managed Identities that need access to the Key Vault (used when RBAC is disabled)')
param managedIdentityObjectIds array = []

@description('Resource tags')
param tags object = {}

// ============================================================================
// Resources
// ============================================================================

var accessPoliciesConfig = [for objectId in managedIdentityObjectIds: {
  tenantId: subscription().tenantId
  objectId: objectId
  permissions: {
    secrets: [
      'get'
      'list'
    ]
    keys: [
      'get'
      'list'
    ]
    certificates: [
      'get'
      'list'
    ]
  }
}]

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-${resourceSuffix}'
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: enableRbacAuthorization
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: enablePurgeProtection ? true : null
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    accessPolicies: enableRbacAuthorization ? [] : accessPoliciesConfig
  }
}

resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: keyVault
  name: 'kv-diagnostics'
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AuditEvent'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// ============================================================================
// Outputs
// ============================================================================

output vaultId string = keyVault.id
output vaultUri string = keyVault.properties.vaultUri
output vaultName string = keyVault.name
