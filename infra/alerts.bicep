// ============================================================================
// Heimdall Access — Azure Monitor Alert Rules
// Requirement 30.7: Alert on error rate > 5%, P95 latency > 3s,
//                   availability < 99.9%
// ============================================================================

@description('Azure region for the alert resources')
param location string

@description('Resource naming suffix (e.g., heimdall-dev)')
param resourceSuffix string

@description('Resource ID of the Application Insights instance')
param appInsightsId string

@description('Email address for alert notifications')
param alertNotificationEmail string = 'ops@heimdall.io'

@description('Resource tags')
param tags object = {}

// ============================================================================
// Action Group — Default notification channel
// ============================================================================

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: 'ag-${resourceSuffix}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'HeimdallOps'
    enabled: true
    emailReceivers: [
      {
        name: 'OpsTeamEmail'
        emailAddress: alertNotificationEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// ============================================================================
// Alert Rule: Error Rate > 5% in 5-minute window
// Uses a scheduled query rule (log-based alert) to calculate the percentage
// of failed requests (HTTP 5xx) over all requests within a 5-min window.
// ============================================================================

resource errorRateAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-error-rate-${resourceSuffix}'
  location: location
  tags: tags
  properties: {
    displayName: 'High Error Rate (>5% in 5min)'
    description: 'Triggers when server error rate exceeds 5% of requests within a 5-minute window'
    severity: 1
    enabled: true
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    scopes: [
      appInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: '''
            let allRequests = AppRequests
            | where TimeGenerated > ago(5m)
            | summarize TotalCount = count(), FailedCount = countif(toint(ResultCode) >= 500);
            allRequests
            | extend ErrorRate = iff(TotalCount == 0, 0.0, (toreal(FailedCount) / toreal(TotalCount)) * 100)
            | project ErrorRate
          '''
          timeAggregation: 'Maximum'
          metricMeasureColumn: 'ErrorRate'
          operator: 'GreaterThan'
          threshold: 5
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

// ============================================================================
// Alert Rule: P95 Latency > 3 seconds
// Uses a scheduled query rule to compute the 95th percentile of request
// duration and alert when it exceeds 3000ms.
// ============================================================================

resource latencyAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-p95-latency-${resourceSuffix}'
  location: location
  tags: tags
  properties: {
    displayName: 'High P95 Latency (>3s)'
    description: 'Triggers when P95 server response time exceeds 3 seconds'
    severity: 2
    enabled: true
    evaluationFrequency: 'PT1M'
    windowSize: 'PT5M'
    scopes: [
      appInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: '''
            AppRequests
            | where TimeGenerated > ago(5m)
            | summarize P95Duration = percentile(DurationMs, 95)
            | project P95Duration
          '''
          timeAggregation: 'Maximum'
          metricMeasureColumn: 'P95Duration'
          operator: 'GreaterThan'
          threshold: 3000
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

// ============================================================================
// Alert Rule: Monthly Availability < 99.9%
// Uses a scheduled query rule with a 1-hour evaluation window. Calculates
// availability as the percentage of requests returning HTTP status < 500.
// A 1-hour window approximates the monthly trend; fires early warning.
// ============================================================================

resource availabilityAlert 'Microsoft.Insights/scheduledQueryRules@2023-03-15-preview' = {
  name: 'alert-availability-${resourceSuffix}'
  location: location
  tags: tags
  properties: {
    displayName: 'Low Availability (<99.9%)'
    description: 'Triggers when API availability drops below 99.9% (successful responses with HTTP status < 500)'
    severity: 1
    enabled: true
    evaluationFrequency: 'PT5M'
    windowSize: 'PT1H'
    scopes: [
      appInsightsId
    ]
    criteria: {
      allOf: [
        {
          query: '''
            AppRequests
            | where TimeGenerated > ago(1h)
            | summarize TotalCount = count(), SuccessCount = countif(toint(ResultCode) < 500)
            | extend Availability = iff(TotalCount == 0, 100.0, (toreal(SuccessCount) / toreal(TotalCount)) * 100)
            | project Availability
          '''
          timeAggregation: 'Minimum'
          metricMeasureColumn: 'Availability'
          operator: 'LessThan'
          threshold: json('99.9')
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    actions: {
      actionGroups: [
        actionGroup.id
      ]
    }
  }
}

// ============================================================================
// Outputs
// ============================================================================

output actionGroupId string = actionGroup.id
output errorRateAlertId string = errorRateAlert.id
output latencyAlertId string = latencyAlert.id
output availabilityAlertId string = availabilityAlert.id
