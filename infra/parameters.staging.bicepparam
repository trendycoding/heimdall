using 'main.bicep'

// =============================================================================
// Heimdall Access — Staging Environment Parameters
// =============================================================================
// Environment characteristics:
//   - Lowest viable cost (Basic SQL 5 DTU, Basic App Service B1, Consumption APIM)
//   - Basic Redis cache C0 (shared, ~$16/mo — tests Redis code path)
//   - No VNet integration or private endpoints (cost savings)
//   - IP restrictions via App Service access rules only
//   - Auto deploy on push to staging branch (no manual approval)
//   - 7-day SQL backup, 7-day blob soft-delete
//   - 30-day monitoring retention
//   - Estimated cost: ~$25-35/month
// =============================================================================

param environment = 'staging'
param location = 'eastus'
param baseName = 'heimdall'
param sqlAdminLogin = 'heimdalladmin'
param sqlAdminPassword = '' // Set via deployment pipeline secret (AZ_SQL_ADMIN_PASSWORD)
param platformDisplayName = 'Heimdall Access (Staging)'
param containerImage = 'mcr.microsoft.com/dotnet/aspnet:8.0'

// IP restrictions — restrict access to known CI/CD runners and office IPs
param allowedIpAddresses = [
  '20.37.194.0/24'   // GitHub Actions (East US range)
  '10.0.0.0/16'      // Internal VNet address space
]
