// Managed Identity module
// System-assigned identities are created on each resource; this module handles
// user-assigned managed identity and role assignments for cross-resource access

@description('Name of the user-assigned managed identity')
param identityName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name')
@allowed(['development', 'staging', 'production'])
param environment string

@description('Key Vault resource ID for role assignment')
param keyVaultId string = ''

@description('Storage Account resource ID for role assignment')
param storageAccountId string = ''

@description('SQL Server resource ID for role assignment')
param sqlServerId string = ''

@description('App Configuration resource ID for role assignment')
param appConfigId string = ''

@description('Redis Cache resource ID for role assignment')
param redisCacheId string = ''

@description('Tags to apply to resources')
param tags object = {}

// User-Assigned Managed Identity
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: tags
}

// Built-in role definition IDs
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var appConfigDataReaderRoleId = '516239f1-63e1-4d78-a4de-a74fb236a071'
var redisCacheContributorRoleId = 'e0f68234-74aa-48ed-b826-c38b57376e17'

// Key Vault Secrets User role assignment
resource keyVaultRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (keyVaultId != '') {
  name: guid(keyVaultId, managedIdentity.id, keyVaultSecretsUserRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Storage Blob Data Contributor role assignment
resource storageBlobRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (storageAccountId != '') {
  name: guid(storageAccountId, managedIdentity.id, storageBlobDataContributorRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Storage Table Data Contributor role assignment
resource storageTableRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (storageAccountId != '') {
  name: guid(storageAccountId, managedIdentity.id, storageTableDataContributorRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataContributorRoleId)
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// App Configuration Data Reader role assignment
resource appConfigRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (appConfigId != '') {
  name: guid(appConfigId, managedIdentity.id, appConfigDataReaderRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', appConfigDataReaderRoleId)
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Redis Cache Contributor role assignment (staging and production)
resource redisCacheRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (redisCacheId != '') {
  name: guid(redisCacheId, managedIdentity.id, redisCacheContributorRoleId)
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', redisCacheContributorRoleId)
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

@description('The resource ID of the managed identity')
output id string = managedIdentity.id

@description('The principal ID of the managed identity')
output principalId string = managedIdentity.properties.principalId

@description('The client ID of the managed identity')
output clientId string = managedIdentity.properties.clientId

@description('The name of the managed identity')
output name string = managedIdentity.name
