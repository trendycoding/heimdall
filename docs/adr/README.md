# Architecture Decision Records

This directory captures significant architectural and technical decisions for
Heimdall, using lightweight ADRs. Each record explains the context, the decision,
the rationale, and the trade-offs, so the reasoning is preserved for future
contributors.

## Format

Each ADR is a numbered Markdown file: `NNNN-short-title.md`. Suggested sections:
Status, Date, Context, Decision, Rationale, Consequences, Alternatives considered,
References.

Statuses: Proposed, Accepted, Superseded (link to the superseding ADR), Deprecated.

## Related guides

- [AWS Deployment Guide](../AWS_DEPLOYMENT_GUIDE.md) — TODO checklist for porting
  Heimdall to AWS (implements the abstractions from ADR 0002).

## Index

| ID | Title | Status |
|----|-------|--------|
| [0001](0001-container-apps-over-azure-functions.md) | Azure Container Apps over Azure Functions for compute | Accepted |
| [0002](0002-cloud-provider-abstraction.md) | Application-level cloud abstraction (Azure default, AWS-ready) | Accepted |
