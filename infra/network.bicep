// Network module — Private Endpoints (production only), IP restrictions (staging)
// Provides network isolation and access controls per environment

@description('Name prefix for network resources')
param namePrefix string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name')
@allowed(['development', 'staging', 'production'])
param environment string

@description('Virtual Network address prefix')
param vnetAddressPrefix string = '10.0.0.0/16'

@description('Application subnet address prefix')
param appSubnetPrefix string = '10.0.1.0/24'

@description('Private endpoints subnet address prefix')
param privateEndpointSubnetPrefix string = '10.0.2.0/24'

@description('Allowed IP addresses for staging IP restrictions')
param allowedIpAddresses array = []

@description('SQL Server resource ID for private endpoint (production)')
param sqlServerId string = ''

@description('Storage Account resource ID for private endpoint (production)')
param storageAccountId string = ''

@description('Redis resource ID for private endpoint (production)')
param redisId string = ''

@description('Key Vault resource ID for private endpoint (production)')
param keyVaultId string = ''

@description('Tags to apply to resources')
param tags object = {}

// Virtual Network — deployed for staging and production
resource vnet 'Microsoft.Network/virtualNetworks@2023-05-01' = if (environment != 'development') {
  name: '${namePrefix}-vnet'
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressPrefix
      ]
    }
    subnets: [
      {
        name: 'app-subnet'
        properties: {
          addressPrefix: appSubnetPrefix
          networkSecurityGroup: environment == 'staging' ? {
            id: nsg.id
          } : null
          delegations: environment == 'production' ? [
            {
              name: 'containerApps'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ] : [
            {
              name: 'appService'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
          serviceEndpoints: [
            {
              service: 'Microsoft.Sql'
            }
            {
              service: 'Microsoft.Storage'
            }
            {
              service: 'Microsoft.KeyVault'
            }
          ]
        }
      }
      {
        name: 'private-endpoints-subnet'
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

// Network Security Group for staging — IP restrictions
resource nsg 'Microsoft.Network/networkSecurityGroups@2023-05-01' = if (environment == 'staging') {
  name: '${namePrefix}-nsg'
  location: location
  tags: tags
  properties: {
    securityRules: [
      {
        name: 'AllowHTTPS'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefixes: allowedIpAddresses
          destinationAddressPrefix: '*'
        }
      }
      {
        name: 'AllowAzureLoadBalancer'
        properties: {
          priority: 200
          direction: 'Inbound'
          access: 'Allow'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: 'AzureLoadBalancer'
          destinationAddressPrefix: '*'
        }
      }
      {
        name: 'DenyAllInbound'
        properties: {
          priority: 4096
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
        }
      }
    ]
  }
}

// ============================================================================
// Private DNS Zones — production only
// ============================================================================

// Private DNS Zone for SQL
resource sqlPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (environment == 'production') {
  name: 'privatelink${az.environment().suffixes.sqlServerHostname}'
  location: 'global'
  tags: tags
}

// Private DNS Zone VNet Link for SQL
resource sqlDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if (environment == 'production') {
  parent: sqlPrivateDnsZone
  name: '${namePrefix}-sql-dns-link'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

// Private DNS Zone for Storage (Table)
resource storagePrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (environment == 'production') {
  name: 'privatelink.table.${az.environment().suffixes.storage}'
  location: 'global'
  tags: tags
}

// Private DNS Zone VNet Link for Storage
resource storageDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if (environment == 'production') {
  parent: storagePrivateDnsZone
  name: '${namePrefix}-storage-dns-link'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

// Private DNS Zone for Redis
resource redisPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (environment == 'production') {
  name: 'privatelink.redis.cache.windows.net'
  location: 'global'
  tags: tags
}

// Private DNS Zone VNet Link for Redis
resource redisDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if (environment == 'production') {
  parent: redisPrivateDnsZone
  name: '${namePrefix}-redis-dns-link'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

// Private DNS Zone for Key Vault
resource keyVaultPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = if (environment == 'production') {
  name: 'privatelink.vaultcore.azure.net'
  location: 'global'
  tags: tags
}

// Private DNS Zone VNet Link for Key Vault
resource keyVaultDnsZoneLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = if (environment == 'production') {
  parent: keyVaultPrivateDnsZone
  name: '${namePrefix}-kv-dns-link'
  location: 'global'
  properties: {
    virtualNetwork: {
      id: vnet.id
    }
    registrationEnabled: false
  }
}

// ============================================================================
// Private Endpoints — production only
// ============================================================================

// Private Endpoint for SQL Server
resource sqlPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = if (environment == 'production' && sqlServerId != '') {
  name: '${namePrefix}-sql-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-sql-plsc'
        properties: {
          privateLinkServiceId: sqlServerId
          groupIds: [
            'sqlServer'
          ]
        }
      }
    ]
  }
}

// DNS Zone Group for SQL Private Endpoint
resource sqlPrivateEndpointDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-05-01' = if (environment == 'production' && sqlServerId != '') {
  parent: sqlPrivateEndpoint
  name: 'sql-dns-zone-group'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'sql-dns-config'
        properties: {
          privateDnsZoneId: sqlPrivateDnsZone.id
        }
      }
    ]
  }
}

// Private Endpoint for Storage Account
resource storagePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = if (environment == 'production' && storageAccountId != '') {
  name: '${namePrefix}-storage-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-storage-plsc'
        properties: {
          privateLinkServiceId: storageAccountId
          groupIds: [
            'table'
          ]
        }
      }
    ]
  }
}

// DNS Zone Group for Storage Private Endpoint
resource storagePrivateEndpointDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-05-01' = if (environment == 'production' && storageAccountId != '') {
  parent: storagePrivateEndpoint
  name: 'storage-dns-zone-group'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'storage-dns-config'
        properties: {
          privateDnsZoneId: storagePrivateDnsZone.id
        }
      }
    ]
  }
}

// Private Endpoint for Redis
resource redisPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = if (environment == 'production' && redisId != '') {
  name: '${namePrefix}-redis-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-redis-plsc'
        properties: {
          privateLinkServiceId: redisId
          groupIds: [
            'redisCache'
          ]
        }
      }
    ]
  }
}

// DNS Zone Group for Redis Private Endpoint
resource redisPrivateEndpointDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-05-01' = if (environment == 'production' && redisId != '') {
  parent: redisPrivateEndpoint
  name: 'redis-dns-zone-group'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'redis-dns-config'
        properties: {
          privateDnsZoneId: redisPrivateDnsZone.id
        }
      }
    ]
  }
}

// Private Endpoint for Key Vault
resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = if (environment == 'production' && keyVaultId != '') {
  name: '${namePrefix}-kv-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id
    }
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-kv-plsc'
        properties: {
          privateLinkServiceId: keyVaultId
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
}

// DNS Zone Group for Key Vault Private Endpoint
resource keyVaultPrivateEndpointDnsGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-05-01' = if (environment == 'production' && keyVaultId != '') {
  parent: keyVaultPrivateEndpoint
  name: 'kv-dns-zone-group'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'kv-dns-config'
        properties: {
          privateDnsZoneId: keyVaultPrivateDnsZone.id
        }
      }
    ]
  }
}

// ============================================================================
// Outputs
// ============================================================================

@description('The VNet resource ID')
output vnetId string = environment != 'development' ? vnet.id : ''

@description('The application subnet resource ID')
output appSubnetId string = environment != 'development' ? vnet.properties.subnets[0].id : ''

@description('The private endpoints subnet resource ID')
output privateEndpointSubnetId string = environment != 'development' ? vnet.properties.subnets[1].id : ''

@description('The NSG resource ID (staging only)')
output nsgId string = environment == 'staging' ? nsg.id : ''
