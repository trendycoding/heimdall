# Heimdall on AWS — Implementation Guide

This document is for engineers porting Heimdall to AWS. The application code is
already cloud-agnostic at the seams (see [ADR 0002](adr/0002-cloud-provider-abstraction.md)).
What remains is implementing the AWS adapters, choosing AWS-native services, and
writing AWS infrastructure-as-code and CI/CD.

> **Status:** Azure is the reference deployment. AWS adapters are stubs. This guide
> is the TODO list to reach a working AWS deployment.

---

## 1. Service mapping (Azure → AWS)

| Concern | Azure (current) | AWS (target) | App-code change? |
|---------|-----------------|--------------|------------------|
| Compute | Container Apps / App Service | ECS Fargate, App Runner, or EKS | No (same image) |
| Database | Azure SQL (SQL Server) | RDS SQL Server **or** RDS/Aurora PostgreSQL | None if SQL Server; small if Postgres |
| Secrets | Key Vault | Secrets Manager (or SSM Parameter Store) | Implement adapter |
| Config | App Configuration | AppConfig or SSM Parameter Store | Config source only |
| Cache | Azure Cache for Redis | ElastiCache for Redis | No (StackExchange.Redis) |
| Telemetry | Application Insights | CloudWatch / X-Ray / OTLP | Exporter wiring only |
| Identity | Entra External ID (OIDC) | Cognito or any OIDC IdP | No (already OIDC-based) |
| Static portal | Static Web Apps | S3 + CloudFront, or Amplify | Hosting only |
| Container registry | GHCR / ACR | ECR (or keep GHCR) | CI/CD only |
| Managed identity | Managed Identity | IAM roles (IRSA / task role) | Credential wiring |

**Key point:** if you stay on SQL Server (RDS for SQL Server), the only mandatory
application code change is the secrets adapter. Everything else is infrastructure
and configuration.

---

## 2. Configuration switches

Set these in the AWS environment (task definition env vars, or SSM/AppConfig):

```jsonc
{
  "Cloud":    { "Provider": "Aws" },       // Azure | Aws | None
  "Database": { "Provider": "SqlServer" }  // SqlServer | Postgres
}
```

- `Cloud:Provider=Aws` selects `AwsSecretsManagerProvider` and skips all Azure
  config sources (App Configuration, Key Vault, App Insights) in `Program.cs`.
- `Cloud:Provider=None` is also viable if you inject all secrets/config as
  environment variables and don't want any managed secret store.

---

## 3. TODO checklist

### 3.1 Secrets — `AwsSecretsManagerProvider` (REQUIRED)

**File:** `src/Heimdall.Infrastructure/Cloud/Aws/AwsSecretsManagerProvider.cs`

Currently a stub that reads from `IConfiguration`. To implement native retrieval:

- [ ] Add NuGet packages to `Heimdall.Infrastructure`:
  - `AWSSDK.SecretsManager`
  - `AWSSDK.Extensions.NETCore.Setup`
- [ ] Register the client in `CloudServiceRegistration.AddCloudServices` under the
      `CloudProvider.Aws` branch:
  ```csharp
  services.AddDefaultAWSOptions(configuration.GetAWSOptions());
  services.AddAWSService<IAmazonSecretsManager>();
  ```
- [ ] Inject `IAmazonSecretsManager` into `AwsSecretsManagerProvider`.
- [ ] Replace `ReadFromStore(string secretKey)` with a `GetSecretValueAsync` call.
      Decide on a secret-naming convention (e.g. `heimdall/<env>/<key>`) and map
      the `secretKey` config path to the AWS secret name.
- [ ] Implement `IsHealthyAsync` with a lightweight call (e.g. `DescribeSecret`
      on a known secret, or `ListSecrets` with `MaxResults=1`).
- [ ] Preserve the existing caching + retry contract (5-minute cache; the base
      stub already caches).

**Secrets to store:** database connection string, Redis connection string, any
external IdP client secrets, and the API-key signing/pepper material if added later.

### 3.2 Database (REQUIRED — choose one path)

**Option A — RDS for SQL Server (least effort):**
- [ ] Provision RDS SQL Server.
- [ ] Set `Database:Provider=SqlServer` (default) and the `HeimdallDb` connection
      string (from Secrets Manager).
- [ ] No code or migration changes. EF Core migrations apply as-is.

**Option B — RDS/Aurora PostgreSQL (cheaper, but more work):**
- [ ] Add NuGet package `Npgsql.EntityFrameworkCore.PostgreSQL` to
      `Heimdall.Infrastructure`.
- [ ] In `DependencyInjection.cs`, replace the `Postgres` branch's `throw` with:
  ```csharp
  options.UseNpgsql(connectionString, npgsql =>
      npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null));
  ```
- [ ] Regenerate migrations for the Postgres provider. The current migration uses
      SQL Server column types (`nvarchar(max)`, `datetime2`). Options:
  - Generate a separate Postgres migration set, **or**
  - Replace provider-specific `HasColumnType(...)` calls in the entity
    configurations with provider-neutral mappings (e.g. drop `nvarchar(max)` and
    let EF map `string` to `text`/`jsonb`).
  - Affected configs: `ApplicationConfiguration`, `IdentityProviderConfigurationConfiguration`,
    `TenantMembershipConfiguration`, `ApiKeyRegistrationConfiguration`, and any
    `datetime2`/`nvarchar(max)` usages in the model snapshot.
- [ ] Verify JSON-serialized `List<string>` columns (AllowedOrigins, AllowedRedirectUris,
      AllowedAlgorithms, ApiKey Scopes) map correctly (consider `jsonb`).
- [ ] Re-run the integration test suite against Postgres (Testcontainers supports it).

### 3.3 Configuration source (RECOMMENDED)

`IConfiguration` already abstracts config. Pick how config reaches the app:
- [ ] **Simplest:** inject config as environment variables in the ECS task
      definition. No code change.
- [ ] **Managed:** add AWS AppConfig or SSM Parameter Store as a configuration
      provider in `Program.cs`, guarded by `cloudProvider == CloudProvider.Aws`.
      Mirror the existing Azure App Configuration block's placement.

### 3.4 Telemetry (RECOMMENDED)

The custom metrics service (`HeimdallMetricsService`) already uses
`System.Diagnostics.Metrics`, which is OpenTelemetry-compatible. Only the exporter
changes.

- [ ] Add OpenTelemetry packages:
  - `OpenTelemetry.Extensions.Hosting`
  - `OpenTelemetry.Instrumentation.AspNetCore`
  - An exporter: `OpenTelemetry.Exporter.OpenTelemetryProtocol` (OTLP → collector →
    CloudWatch/X-Ray) or the AWS Distro for OpenTelemetry (ADOT).
- [ ] Register OTel in `Program.cs` (guarded by `CloudProvider.Aws`), subscribing
      to the `Heimdall.Access` meter (`HeimdallMetricsService.MeterName`).
- [ ] Note: `CorrelationIdTelemetryInitializer` and `SensitiveDataTelemetryProcessor`
      are Application Insights-specific (wired via `AddAzureApplicationInsightsEnrichment`,
      only called on Azure). For AWS, implement equivalent enrichment/redaction as an
      OTel processor if required — the sensitive-data redaction is a security control
      and should not be dropped.

### 3.5 Compute (REQUIRED)

The container image is provider-neutral. Choose a runtime:
- [ ] **ECS Fargate** (recommended default): task definition + service + ALB.
- [ ] **App Runner** (simplest): point at ECR image; auto-scales, managed TLS.
- [ ] **EKS**: if you already run Kubernetes.
- [ ] Grant the task role IAM permissions for Secrets Manager, (optional) AppConfig/SSM,
      CloudWatch, and ElastiCache network access.
- [ ] Configure health probe against the existing `/health` endpoint.
- [ ] For low-cost/pre-launch: App Runner or Fargate with min 1 task (there is no
      true scale-to-zero on Fargate; App Runner can pause).

### 3.6 Cache (REQUIRED if using Redis)

- [ ] Provision ElastiCache for Redis.
- [ ] Put the connection string in Secrets Manager; the app reads `ConnectionStrings:Redis`.
- [ ] `StackExchange.Redis` works unchanged. Ensure VPC/security-group connectivity
      from the compute layer.

### 3.7 Identity (LIKELY NO CHANGE)

- Token validation is OIDC/JWT-based and provider-driven (per `IdentityProviderConfiguration`).
- [ ] If using Cognito, register it as an `ExternalOidc` or appropriate provider type
      per tenant. No code change expected.
- [ ] If keeping Entra External ID even on AWS, nothing to do.

### 3.8 Static Admin Portal (REQUIRED)

- [ ] Build the React app (`npm run build` in `src/Heimdall.AdminPortal`).
- [ ] Host on S3 + CloudFront (or AWS Amplify).
- [ ] Configure SPA routing fallback to `index.html`.
- [ ] Set the portal's `VITE_API_BASE_URL` and MSAL/OIDC settings for the AWS API endpoint.

### 3.9 Infrastructure as Code (REQUIRED — largest effort)

The `infra/` directory is Azure Bicep and is **not** reusable on AWS.
- [ ] Author AWS IaC (Terraform or AWS CDK) for: VPC + subnets + security groups,
      RDS, ElastiCache, ECS/Fargate (or App Runner), ALB, ECR, Secrets Manager,
      IAM roles, CloudWatch, S3 + CloudFront for the portal.
- [ ] Parameterize per environment (dev/staging/prod) mirroring the Bicep
      `envConfig` cost tiers.
- [ ] Keep the Bicep for Azure; do not attempt a single tool for both unless you
      standardize on Terraform for both clouds.

### 3.10 CI/CD (REQUIRED)

The GitHub Actions workflows have Azure-specific deploy/migrate steps.
- [ ] Add AWS credentials via OIDC federation (`aws-actions/configure-aws-credentials`).
- [ ] Add a build-and-push step to ECR (reuse the existing container build).
- [ ] Add a deploy step for the chosen compute (ECS deploy / App Runner update).
- [ ] Add a migration step that runs EF Core migrations against RDS
      (mirror `.github/actions/migrate`).
- [ ] Add a portal deploy step (S3 sync + CloudFront invalidation).
- [ ] Gate production with a manual approval environment (as the Azure prod workflow does).

---

## 4. Verification checklist

Before calling an AWS deployment done:

- [ ] `dotnet build` succeeds and the full test suite passes.
- [ ] Integration tests run green against the target database engine (Testcontainers).
- [ ] `/health` reports healthy for database, cache, and secret-store checks
      (`SecretStoreHealthCheck` probes the active `ISecretProvider`).
- [ ] A headless flow works end-to-end: authenticate → `POST /api/me/register-tenant`
      → `POST /api/me/tenants/{id}/api-keys` → use `X-Api-Key` to create an
      application, users, and permissions.
- [ ] Secrets are never logged (verify redaction still applies on AWS).
- [ ] The Admin Portal loads, authenticates, and calls the API.
- [ ] Cost tiers match the intended environment (staging/prod kept minimal).

---

## 5. Effort estimate (reference)

| Task | Effort |
|------|--------|
| `AwsSecretsManagerProvider` implementation | ~1 day |
| Database (SQL Server on RDS) | ~0.5 day |
| Database (PostgreSQL port + migrations) | ~1–2 days |
| Config source (env vars) | ~0.5 day |
| OpenTelemetry exporter + redaction processor | ~1–2 days |
| Compute (ECS/Fargate or App Runner) | ~1–2 days |
| IaC (Terraform/CDK, all envs) | ~3–5 days |
| CI/CD (dual-path) | ~1–2 days |
| Portal hosting (S3 + CloudFront) | ~0.5 day |
| End-to-end testing both clouds | ~2–3 days |

Rough total: **2–3 weeks**, dominated by IaC and CI/CD. The application code changes
are a small fraction (secrets adapter + optional Postgres wiring).

---

## 6. Reference files

- Abstractions: `src/Heimdall.Domain/Interfaces/ISecretProvider.cs`,
  `src/Heimdall.Domain/Enums/CloudProvider.cs`
- Adapters + registration: `src/Heimdall.Infrastructure/Cloud/`
  - `Azure/AzureKeyVaultSecretProvider.cs`
  - `Aws/AwsSecretsManagerProvider.cs` (the stub to implement)
  - `ConfigurationSecretProvider.cs`
  - `CloudServiceRegistration.cs`
- Conditional Azure wiring: `src/Heimdall.Api/Program.cs`
- DB provider switch: `src/Heimdall.Infrastructure/DependencyInjection.cs`
- Telemetry split: `src/Heimdall.Infrastructure/Logging/StructuredLoggingExtensions.cs`
- Health check: `src/Heimdall.Api/HealthChecks/SecretStoreHealthCheck.cs`
- Decision record: `docs/adr/0002-cloud-provider-abstraction.md`
