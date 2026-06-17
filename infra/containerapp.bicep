// Container Apps module — Production compute with autoscaling
// Deployed only in production environment

@description('Name of the Container Apps environment')
param environmentName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Name of the container app')
param appName string

@description('Container image to deploy')
param containerImage string

@description('Target port for the container')
param targetPort int = 8080

@description('Minimum number of replicas')
param minReplicas int = 2

@description('Maximum number of replicas')
param maxReplicas int = 10

@description('CPU cores allocated to each replica')
param cpuCores string = '0.5'

@description('Memory allocated to each replica (e.g., 1Gi)')
param memory string = '1Gi'

@description('Environment variables for the container')
param envVars array = []

@description('Log Analytics workspace ID for diagnostics')
param logAnalyticsWorkspaceId string

@description('Log Analytics workspace customer ID')
param logAnalyticsCustomerId string = ''

@description('Log Analytics workspace shared key')
@secure()
param logAnalyticsSharedKey string = ''

@description('User-assigned managed identity resource ID (optional)')
param managedIdentityId string = ''

@description('HTTP concurrent requests threshold for autoscaling')
param httpScaleConcurrentRequests string = '50'

@description('Application subnet ID for VNet integration (optional)')
param appSubnetId string = ''

@description('Tags to apply to resources')
param tags object = {}

// Container Apps Environment
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsCustomerId != '' ? logAnalyticsCustomerId : reference(logAnalyticsWorkspaceId, '2022-10-01').customerId
        sharedKey: logAnalyticsSharedKey != '' ? logAnalyticsSharedKey : listKeys(logAnalyticsWorkspaceId, '2022-10-01').primarySharedKey
      }
    }
    zoneRedundant: true
    vnetConfiguration: appSubnetId != '' ? {
      infrastructureSubnetId: appSubnetId
      internal: false
    } : null
  }
}

// Container App
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: appName
  location: location
  tags: tags
  identity: {
    type: managedIdentityId != '' ? 'SystemAssigned,UserAssigned' : 'SystemAssigned'
    userAssignedIdentities: managedIdentityId != '' ? {
      '${managedIdentityId}': {}
    } : null
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Multiple'
      ingress: {
        external: true
        targetPort: targetPort
        transport: 'http'
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
        corsPolicy: {
          allowCredentials: true
          allowedHeaders: ['*']
          allowedMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS']
          allowedOrigins: ['*']
          maxAge: 3600
        }
      }
      maxInactiveRevisions: 5
      secrets: []
    }
    template: {
      containers: [
        {
          name: appName
          image: containerImage
          resources: {
            cpu: json(cpuCores)
            memory: memory
          }
          env: envVars
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: targetPort
                scheme: 'HTTP'
              }
              initialDelaySeconds: 15
              periodSeconds: 30
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: targetPort
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Startup'
              httpGet: {
                path: '/health'
                port: targetPort
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 5
              failureThreshold: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: httpScaleConcurrentRequests
              }
            }
          }
        ]
      }
    }
  }
}

@description('The FQDN of the Container App')
output fqdn string = containerApp.properties.configuration.ingress.fqdn

@description('The resource ID of the Container App')
output id string = containerApp.id

@description('The principal ID of the system-assigned managed identity')
output principalId string = containerApp.identity.principalId

@description('The Container Apps Environment ID')
output environmentId string = containerAppsEnvironment.id
