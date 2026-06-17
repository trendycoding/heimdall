using 'main.bicep'

// =============================================================================
// Heimdall Access — Development Environment Parameters
// =============================================================================
// Environment characteristics:
//   - Absolute lowest cost SKUs (Basic SQL 5 DTU, Free App Service F1, Consumption APIM)
//   - No Redis cache (in-memory caching only)
//   - No VNet, no private endpoints, no IP restrictions
//   - Auto deploy on push to dev branch (no manual approval)
//   - Minimal backup retention (7 days SQL, no blob soft-delete)
//   - 30-day monitoring retention
//   - Estimated cost: ~$5-10/month (SQL Basic is the main cost)
// =============================================================================

param environment = 'dev'
param location = 'eastus'
param baseName = 'heimdall'
param sqlAdminLogin = 'heimdalladmin'
param sqlAdminPassword = '' // Set via deployment pipeline secret (AZ_SQL_ADMIN_PASSWORD)
param platformDisplayName = 'Heimdall Access (Dev)'
param containerImage = 'mcr.microsoft.com/dotnet/aspnet:8.0'
param allowedIpAddresses = [] // Public access — no IP restrictions in dev
