# ADR 0002: Application-level cloud abstraction (Azure default, AWS-ready)

- Status: Accepted
- Date: 2026-09-03
- Deciders: Engineering

## Context

Heimdall targets Azure today, but there was a question about whether it could be
deployed to AWS. A full multi-cloud IaC + CI/CD investment is not justified while
there is no concrete AWS deployment need. However, we did not want cloud-specific
SDK calls scattered through the application, which would make any future port
expensive and error-prone.

## Decision

Introduce application-level abstractions for the cloud-coupled concerns so the
codebase is AWS-ready without committing to a second IaC/CI stack. Anyone who needs
an AWS deployment can implement the stubbed adapters (and their own IaC) or engage
us to do it.

Abstractions introduced:

- `ISecretProvider` (Domain) — cloud-agnostic secret retrieval. Adapters:
  - `AzureKeyVaultSecretProvider` (default)
  - `AwsSecretsManagerProvider` (stub — falls back to configuration)
  - `ConfigurationSecretProvider` (env/appsettings, used for `None`)
- `CloudProvider` enum (Domain) — `Azure | Aws | None`, selected by the
  `Cloud:Provider` configuration setting (defaults to `Azure`).
- `Database:Provider` setting (`SqlServer` default, `Postgres` reserved) — the EF
  Core provider is selected at startup; Postgres is wired to throw a clear
  "not yet enabled" error until the Npgsql package is added.
- Telemetry split: `AddHeimdallTelemetry` now registers only the vendor-neutral
  metrics service (built on `System.Diagnostics.Metrics`).
  `AddAzureApplicationInsightsEnrichment` holds the Azure-only initializer/processor
  and is called only when running on Azure with App Insights configured.
- `Program.cs` wires Azure App Configuration, Key Vault config source, and App
  Insights only when `Cloud:Provider = Azure`.
- Health check `KeyVaultHealthCheck` replaced by `SecretStoreHealthCheck`, which
  probes the active `ISecretProvider`.

## What was intentionally NOT done

- No Terraform/Pulumi multi-cloud IaC (Azure Bicep remains the only IaC).
- No AWS-specific CI/CD paths.
- No native AWS Secrets Manager retrieval (the adapter is a documented stub).
- No OpenTelemetry exporter migration (metrics are already OTel-compatible via
  `System.Diagnostics.Metrics`; only the exporter wiring would change).

These are deferred until there is a real AWS deployment requirement.

## Consequences

### Positive
- Business logic (Domain/Application) has zero cloud-SDK coupling.
- Switching secret backends is a one-line config change plus an adapter.
- Local/self-hosted runs work with `Cloud:Provider=None` (no Azure services needed).
- The remaining work to run on AWS is well-scoped and documented (adapters + IaC).

### Negative / Trade-offs
- The AWS secret adapter is a stub; it reads from configuration until implemented.
- A Postgres deployment requires adding the Npgsql package and one line of wiring.
- Provider-specific EF column types (`nvarchar(max)`, `datetime2`) still exist in
  the SQL Server migration; a Postgres port would need a fresh migration.

## Configuration

```jsonc
{
  "Cloud":    { "Provider": "Azure" },   // Azure | Aws | None
  "Database": { "Provider": "SqlServer" } // SqlServer | Postgres (reserved)
}
```

Local development uses `Cloud:Provider = None`.

## References
- `Heimdall.Domain/Interfaces/ISecretProvider.cs`
- `Heimdall.Domain/Enums/CloudProvider.cs`
- `Heimdall.Infrastructure/Cloud/` (adapters + registration)
- `Heimdall.Api/Program.cs` (conditional Azure wiring)
- ADR 0001 (compute choice — container portability)
