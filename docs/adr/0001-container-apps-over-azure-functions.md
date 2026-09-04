# ADR 0001: Azure Container Apps over Azure Functions for compute

- Status: Accepted
- Date: 2026-09-03
- Deciders: Engineering

## Context

Heimdall is a multi-tenant authorization and access management platform. The core
runtime is an ASP.NET Core (.NET 8) REST API with a specific ordered middleware
pipeline (correlation ID, API call logging, token validation, tenant resolution,
admin authorization) plus many CRUD endpoints and access-check endpoints.

We needed to choose a production compute model on Azure. The two main candidates
were:

1. Azure Functions (serverless, event/trigger oriented)
2. Azure Container Apps (serverless containers, HTTP oriented)

Note: "Docker" appears in this project in two separate roles. Locally, Docker
Compose is used purely for developer experience (spins up SQL Server and Azurite
with one command — see Requirement 26). Separately, the production compute target
is Azure Container Apps, which runs the same container image. This ADR is about the
production compute choice, not the local dev tooling.

## Decision

Use Azure Container Apps for production compute and Azure App Service (for
containers) for dev/staging. Package the API as a single container image used
identically across local, dev/staging, and production.

## Rationale

- **Workload shape fits containers, not triggers.** Heimdall is an always-available
  request/response REST API, not event-driven background work. Functions is
  optimized for triggers (queues, blobs, timers). A long-lived container running
  Kestrel is the natural fit.
- **Existing middleware pipeline.** The app relies on the standard ASP.NET Core
  middleware chain in a specific order. This runs as-is in a container. Reproducing
  it under the Functions host model would add friction with little benefit.
- **Latency on the critical path.** As an authorization service, Heimdall sits in
  front of every customer request. Functions Consumption cold starts (notably for
  .NET) introduce latency spikes. Container Apps allows `minReplicas > 0` to keep
  instances warm once live.
- **One artifact everywhere.** The same image runs in local Docker Compose,
  dev/staging (App Service for containers), and production (Container Apps),
  giving consistent behavior across environments.
- **Portability.** A container is not locked to a specific serverless runtime; it
  can run on other clouds or on-prem if needed. Functions couples us to the Azure
  Functions runtime.

## Consequences

### Positive
- Consistent build/run artifact across all environments.
- Full control over the ASP.NET Core pipeline and startup.
- Ability to eliminate cold starts by keeping replicas warm.
- Cloud/runtime portability.

### Negative / Trade-offs
- Keeping Container Apps warm (`minReplicas > 0`) costs money, whereas Functions
  Consumption can scale fully to zero at no idle cost.
- Slightly more infrastructure to manage (container image build/push, registry)
  than a pure Functions deployment.

### Current status
- While the platform is pre-launch and not serving real traffic, production
  Container Apps is configured with `minReplicas: 0` (scale-to-zero) to avoid idle
  compute cost. When going live, raise `minReplicas` to remove cold starts.

## Alternatives considered

- **Azure Functions (Consumption):** Rejected as primary compute for the reasons
  above (workload shape, middleware, cold-start latency on the critical path).
- **Azure App Service (Windows/Linux, non-container):** Used for dev/staging via
  the container variant. Not chosen for production because Container Apps offers
  better autoscaling ergonomics and scale-to-zero for a pre-launch cost profile.

## References
- Requirement 26 (Local Development Environment) — Docker Compose for local deps.
- Design doc "Technology Stack" — Compute: Azure Container Apps (production) /
  App Service (dev/staging).
- `infra/main.bicep` — environment-specific compute wiring.
