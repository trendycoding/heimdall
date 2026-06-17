using 'main.bicep'

// =============================================================================
// Heimdall Access — Production Environment Parameters
// =============================================================================
// Environment characteristics:
//   - High reliability SKUs (Standard SQL S1, Premium Redis P1, Standard APIM)
//   - Premium Redis cache (P1 SKU, dedicated infrastructure, zone redundancy)
//   - Private endpoints for all data services (SQL, Storage, Redis, Key Vault)
//   - 35-day SQL backup retention, 35-day blob soft-delete
//   - Autoscaling via Container Apps (2–10 replicas)
//   - Manual approval required before deploy (via GitHub Environment protection)
//   - 90-day monitoring retention
//   - Geo-redundant storage (Standard_GRS)
//   - Key Vault purge protection enabled
// =============================================================================

param environment = 'prod'
param location = 'eastus'
param baseName = 'heimdall'
param sqlAdminLogin = 'heimdalladmin'
param sqlAdminPassword = '' // Set via deployment pipeline secret (AZ_SQL_ADMIN_PASSWORD)
param platformDisplayName = 'Heimdall Access'
param containerImage = 'mcr.microsoft.com/dotnet/aspnet:8.0'

// Production uses private endpoints — no public IP access allowed
param allowedIpAddresses = []
