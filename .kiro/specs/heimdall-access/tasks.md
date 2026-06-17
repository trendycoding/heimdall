# Implementation Plan: Heimdall Access

## Overview

This plan implements the Heimdall Access platform — an Azure-native, multi-tenant security and access management system. The implementation follows Clean Architecture with Domain, Application, Infrastructure, and API layers, plus a React Admin Portal and Azure Bicep infrastructure. Tasks are ordered so each step builds on the previous, with no orphaned code.

## Tasks

- [x] 1. Solution scaffolding and project structure
  - [x] 1.1 Create .NET solution and project structure
    - Create `Heimdall.sln` with projects: `Heimdall.Domain`, `Heimdall.Application`, `Heimdall.Infrastructure`, `Heimdall.Api`
    - Create test projects: `Heimdall.Domain.Tests`, `Heimdall.Application.Tests`, `Heimdall.Infrastructure.Tests`, `Heimdall.Api.IntegrationTests`
    - Add project references following Clean Architecture dependency rules (Domain has no dependencies; Application references Domain; Infrastructure references Application; Api references Application and Infrastructure)
    - Add NuGet packages: MediatR, FluentValidation, EF Core 8+, FsCheck.Xunit, Testcontainers, Microsoft.Identity.Web, StackExchange.Redis, Swashbuckle
    - _Requirements: 25.1, 26.1_

  - [x] 1.2 Create directory structure within each project
    - Domain: `Entities/`, `ValueObjects/`, `Enums/`, `Events/`, `Interfaces/`
    - Application: `Common/Behaviors/`, `Common/Interfaces/`, `Common/Models/`, plus feature folders (Tenants, Applications, IdentityProviders, Users, FunctionalAreas, PermissionTypes, Permissions, Groups, PermissionAssignments, AccessDetails, PermissionTemplates, PermissionResolution, AuditLogs)
    - Infrastructure: `Persistence/Configurations/`, `Persistence/Migrations/`, `Caching/`, `Identity/`, `Logging/`, `Services/`
    - Api: `Controllers/`, `Middleware/`, `Filters/`
    - Tests: `Properties/`, `Generators/`, `Unit/` in domain/application tests; `CrudTests/`, `AccessCheckTests/`, `SecurityTests/`, `Infrastructure/` in integration tests
    - _Requirements: 25.1_

- [x] 2. Domain layer — entities, enums, value objects, and interfaces
  - [x] 2.1 Implement base entity classes and enums
    - Create `BaseEntity` (Id, CreatedAt, CreatedBy, ModifiedAt, ModifiedBy)
    - Create `TenantScopedEntity : BaseEntity` (TenantId)
    - Create `ApplicationScopedEntity : TenantScopedEntity` (ApplicationId)
    - Create enums: `TenantStatus`, `ApplicationStatus`, `UserStatus`, `IdpStatus`, `Effect`, `PrimaryIdentityMode`, `ProviderType`, `AdminScope`
    - Create `ClaimMapping` record
    - _Requirements: 1.1, 29.3_

  - [x] 2.2 Implement core domain entities
    - Create `Tenant`, `Application`, `IdentityProviderConfiguration`, `UserProfile`, `FunctionalArea`, `PermissionType`, `Permission`, `Group`, `GroupMembership`
    - Create `UserPermissionAssignment`, `GroupPermissionAssignment`
    - Create `UserAccessDetail`, `GroupAccessDetail`, `FunctionalAreaAccessRequirement`
    - Create `PermissionTemplate`, `PermissionTemplatePermission`, `PermissionTemplateGroup`, `PermissionTemplateAccessDetail`
    - Create `UserPermissionTemplateApplication`, `ApiCallLog`, `AuditLog`
    - Apply field constraints (max lengths, defaults) as documented in design
    - _Requirements: 1.1, 2.1, 3.1, 4.1, 5.1, 6.1, 7.1, 8.1, 9.1, 9.2, 11.1, 12.1, 13.1, 14.1, 14.2, 14.3, 14.4, 16.1, 17.1_

  - [x] 2.3 Define domain interfaces
    - Create `IRepository<T>` generic interface (GetByIdAsync, GetAllAsync, AddAsync, UpdateAsync)
    - Create `ITenantContext` interface (TenantId, ApplicationId, ActorSubjectId, ActorEmail, ActorUserProfileId, AdminScopes, IsSuperAdmin)
    - Create `IPermissionResolver` interface with all resolution methods (CheckPermissionAsync, CheckPermissionByCodeAsync, CheckPermissionByExternalIdAsync, CheckBatchAsync, GetEffectivePermissionsAsync)
    - Create `IAccessDetailResolver` interface (GetAccessDetailsAsync)
    - Create `ICacheService` interface (GetAsync, SetAsync, RemoveAsync, RemoveByPrefixAsync)
    - Create `IAuditService` interface (RecordAsync)
    - Create `ITokenValidationService` interface (ValidateTokenAsync)
    - Create `IApiCallLogService` interface (BeginLogAsync, CompleteLogAsync)
    - Create `ITemplateApplicationService` interface (ApplyTemplateAsync, PreviewTemplateAsync)
    - Create `IProductConfiguration` interface (GetDisplayName)
    - _Requirements: 10.1, 10.2, 10.5, 10.6, 11.4, 16.1, 17.1, 22.1, 28.1_

- [x] 3. Infrastructure layer — persistence and EF Core
  - [x] 3.1 Implement HeimdallDbContext with global query filters
    - Create `HeimdallDbContext` inheriting `DbContext` with `ITenantContext` injection
    - Define `DbSet<T>` properties for all entities
    - Apply global query filters for TenantId on all tenant-scoped entities
    - Override `SaveChangesAsync` to auto-populate CreatedAt/ModifiedAt/CreatedBy/ModifiedBy from `ITenantContext`
    - _Requirements: 19.1, 19.4, 29.4_

  - [x] 3.2 Create EF Core entity configurations
    - Create `IEntityTypeConfiguration<T>` for each entity defining: primary keys, max lengths, required fields, column types
    - Configure unique indexes: Tenant.Slug (global), Application.ClientIdentifier (per tenant), FunctionalArea.FunctionalAreaCode (per app), PermissionType.Code (per app, case-insensitive), Permission.PermissionCode (per app), Group.Name (per app), PermissionTemplate.TemplateCode (per app), UserProfile ExternalSubjectId+IdentityProvider (per tenant), IdentityProviderConfiguration composite unique, FunctionalAreaAccessRequirement composite unique, UserAccessDetail composite unique, GroupAccessDetail composite unique, GroupMembership composite unique, PermissionTemplatePermission composite unique
    - Configure composite lookup indexes per design's index table (UserPermissionAssignment, GroupPermissionAssignment, ApiCallLog, AuditLog, etc.)
    - Configure list-to-JSON converters for AllowedRedirectUris, AllowedOrigins, AllowedAlgorithms, ClaimMappings
    - _Requirements: 1.4, 2.4, 3.4, 4.3, 5.2, 6.2, 7.2, 8.2, 8.4, 11.3, 12.2, 14.5, 14.8, 29.1, 29.2_

  - [x] 3.3 Create initial EF Core migration
    - Generate initial migration from DbContext configuration
    - Verify migration produces all tables, indexes, and constraints as designed
    - _Requirements: 26.3, 29.1_

  - [x] 3.4 Implement generic repository and tenant-scoped repositories
    - Create `Repository<T> : IRepository<T>` with HeimdallDbContext
    - Create specialized repositories where needed (e.g., permission resolution queries, audit log queries with IgnoreQueryFilters)
    - Implement `ITenantContext` as `TenantContext` (populated from middleware)
    - _Requirements: 19.1, 19.4, 29.4_

- [x] 4. Checkpoint — Domain and Infrastructure compile
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Application layer — CQRS commands, queries, validators, behaviors
  - [x] 5.1 Implement MediatR pipeline behaviors
    - Create `ValidationBehavior<TRequest, TResponse>` running FluentValidation before handler execution
    - Create `AuditBehavior<TRequest, TResponse>` capturing BeforeJson/AfterJson state for commands
    - Create `CacheInvalidationBehavior<TRequest, TResponse>` invalidating relevant cache keys after mutations
    - Create `PerformanceLogBehavior<TRequest, TResponse>` logging slow queries
    - Register behaviors in DI with correct pipeline order
    - _Requirements: 17.1, 22.3, 23.6_

  - [x] 5.2 Implement Tenant commands, queries, and validators
    - Create `CreateTenantCommand` / handler with FluentValidation (Name 1-128, Slug 1-64 pattern, valid PrimaryIdentityMode)
    - Create `UpdateTenantCommand` / handler preserving immutable fields (TenantId, CreatedAt, CreatedBy)
    - Create `DeactivateTenantCommand` / handler (reject if already Inactive)
    - Create `GetTenantQuery`, `GetTenantsQuery` / handlers
    - _Requirements: 1.1, 1.2, 1.3, 1.6, 1.7, 1.8, 1.9_

  - [x] 5.3 Implement Application commands, queries, and validators
    - Create `CreateApplicationCommand` / handler (Name required max 200, ClientIdentifier required max 128, Description max 1000, AllowedRedirectUris max 20 valid URIs, AllowedOrigins max 20)
    - Create `UpdateApplicationCommand` / handler (mutable: Name, Description, AllowedRedirectUris, AllowedOrigins only)
    - Create `DeactivateApplicationCommand` / handler
    - Create `GetApplicationQuery`, `GetApplicationsQuery` / handlers
    - Enforce ClientIdentifier uniqueness within tenant
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7_

  - [x] 5.4 Implement Identity Provider commands, queries, and validators
    - Create `CreateIdentityProviderCommand` / handler with all IdP fields
    - Create `UpdateIdentityProviderCommand` / handler
    - Create `DeactivateIdentityProviderCommand` / handler
    - Create queries for IdP listing/retrieval
    - Enforce composite uniqueness (TenantId, ApplicationId, ProviderType, Name)
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.9, 3.10_

  - [x] 5.5 Implement User Sync commands, queries, and validators
    - Create `SyncUserCommand` / handler (upsert UserProfile by ExternalSubjectId + IdentityProvider)
    - Handle applications array with permissionTemplateCodes during sync
    - Create `UpdateUserCommand` / handler, `DeactivateUserCommand` / handler
    - Create user queries
    - Update LastLoginAt on auth
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8_

  - [x] 5.6 Implement Functional Area, Permission Type, and Permission commands/queries
    - Functional Area: CRUD with FunctionalAreaCode validation (max 50, uppercase alphanum + underscore), uniqueness per app
    - Permission Type: CRUD with Code validation (max 100, alphanum + underscore), case-insensitive uniqueness per app, IsSystemReserved protection
    - Permission: CRUD with PermissionCode validation (max 200, uppercase), uniqueness per app, cross-app reference validation
    - _Requirements: 5.1–5.7, 6.1–6.7, 7.1–7.7_

  - [x] 5.7 Implement Group and Group Membership commands/queries
    - Create `CreateGroupCommand` / handler with Name uniqueness per application
    - Create `AddGroupMembershipCommand` / handler enforcing single membership per user per group
    - Create `DeactivateGroupCommand` / handler
    - Create group and membership queries
    - Cross-tenant validation for membership
    - _Requirements: 8.1–8.8_

  - [x] 5.8 Implement Permission Assignment commands/queries
    - Create `CreateUserPermissionAssignmentCommand` / handler (Effect: Allow/Deny, ValidFrom, ValidTo)
    - Create `CreateGroupPermissionAssignmentCommand` / handler
    - Validate referenced entities exist and are active; ValidFrom <= ValidTo
    - Create assignment queries and deletion commands
    - _Requirements: 9.1–9.7_

  - [x] 5.9 Implement Access Detail commands/queries (User, Group, Functional Area Requirements)
    - User Access Detail CRUD with composite uniqueness (UserProfileId, ApplicationId, AccessDetailType, AccessDetailCode)
    - Group Access Detail CRUD with composite uniqueness (GroupId, ApplicationId, AccessDetailType, AccessDetailCode)
    - Functional Area Access Requirement CRUD (FunctionalAreaId + AccessDetailType unique)
    - _Requirements: 11.1–11.6, 12.1–12.5, 13.1–13.6_

  - [x] 5.10 Implement Permission Template CRUD commands/queries
    - Create `CreatePermissionTemplateCommand` / handler with TemplateCode uniqueness per application
    - Support adding PermissionTemplatePermission, PermissionTemplateGroup, PermissionTemplateAccessDetail entries
    - Cross-application/tenant reference validation
    - _Requirements: 14.1–14.8_

  - [x] 5.11 Implement Audit Log and API Call Log queries
    - Create `GetAuditLogsQuery` / handler with filters: TenantId, ApplicationId, EntityType, EntityId, Action, UserId, date range, CorrelationId; sorted by CreatedAt DESC, max page 100
    - Create `GetApiCallLogsQuery` / handler with filters: TenantId, ApplicationId, CorrelationId, Endpoint, HttpMethod, SourceIp, CallerSubjectId, date range
    - _Requirements: 16.5, 17.6_

- [x] 6. Checkpoint — Application layer compile and unit tests
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Permission Resolution Engine
  - [x] 7.1 Implement PermissionResolver service
    - Implement `IPermissionResolver` in Infrastructure layer
    - Single permission check: validate user active → resolve permission → check FA/PT/Permission active → collect direct + group assignments → filter by time window → apply deny-overrides-allow → build response with matchedAssignments
    - Batch permission check (max 50): share user validation + group membership lookup, evaluate each permission individually
    - Effective permissions: return all resolved permissions with Allow/Deny status
    - ExternalSubjectId resolution: lookup UserProfile by (TenantId, ExternalSubjectId, IdentityProvider) then delegate to standard resolution
    - _Requirements: 10.1–10.12_

  - [x] 7.2 Write property test: Deny Overrides Allow
    - **Property 1: Deny Overrides Allow**
    - Generate arbitrary user with both Allow and Deny assignments for the same permission (both temporally valid, all entities active); assert resolution always returns Deny
    - **Validates: Requirements 9.5, 10.3**

  - [x] 7.3 Write property test: Temporal Validity Filtering
    - **Property 2: Temporal Validity Filtering**
    - Generate arbitrary assignments with ValidFrom/ValidTo ranges and random evaluation times; assert only assignments where evaluationTime ∈ [ValidFrom, ValidTo] are considered
    - **Validates: Requirements 9.4**

  - [x] 7.4 Write property test: Inactive Entity Exclusion
    - **Property 3: Inactive Entity Exclusion**
    - Generate arbitrary permission chains where one entity is inactive; assert those assignments are excluded from evaluation
    - **Validates: Requirements 5.6, 8.8, 10.4**

  - [x] 7.5 Write property test: Default Deny
    - **Property 4: Default Deny**
    - Generate scenarios with inactive user or no applicable assignments; assert Deny decision
    - **Validates: Requirements 10.10, 10.11**

  - [x] 7.6 Write property test: Batch Permission Check Consistency
    - **Property 5: Batch Permission Check Consistency**
    - Generate batch of permission checks; assert each result equals the standalone single check result
    - **Validates: Requirements 10.5**

  - [x] 7.7 Write property test: ExternalSubjectId Lookup Equivalence
    - **Property 6: ExternalSubjectId Lookup Equivalence**
    - Generate users with external IDs; assert check by external ID equals check by resolved UserProfileId
    - **Validates: Requirements 10.12**

- [x] 8. Access Detail Resolution
  - [x] 8.1 Implement AccessDetailResolver service
    - Implement `IAccessDetailResolver` in Infrastructure layer
    - Validate user has functional permission to the area first
    - Retrieve active FunctionalAreaAccessRequirement records for the FunctionalArea
    - Collect direct UserAccessDetail records matching declared AccessDetailTypes (active, non-expired)
    - Collect GroupAccessDetail records from active groups user belongs to (active, non-expired)
    - Include source indicator (DirectUser or Group with groupId/groupName) on each record
    - If no FunctionalAreaAccessRequirements exist, return empty details (access based solely on permission)
    - _Requirements: 11.4, 11.5, 11.6, 12.4, 12.5, 13.3, 13.4_

  - [x] 8.2 Write property test: Access Detail Filtering by Functional Area Requirements
    - **Property 13: Access Detail Filtering by Functional Area Requirements**
    - Generate access details with various types; assert only those matching active FA requirements are returned
    - **Validates: Requirements 11.4, 12.4, 13.3**

  - [x] 8.3 Write property test: Access Detail Source Indicator Correctness
    - **Property 14: Access Detail Source Indicator Correctness**
    - Generate mixed direct and group access details; assert correct source indicators on each
    - **Validates: Requirements 11.5, 12.5**

- [x] 9. Caching strategy implementation
  - [x] 9.1 Implement ICacheService with Redis and In-Memory providers
    - Create `RedisCacheService : ICacheService` using StackExchange.Redis
    - Create `InMemoryCacheService : ICacheService` using `IMemoryCache`
    - Register appropriate provider based on environment (Redis for prod/staging, InMemory for dev)
    - Implement cache key strategy: `perm:{tenantId}:{appId}:{userId}:{permCode}`, `perm-effective:{tenantId}:{appId}:{userId}`, `access:{tenantId}:{appId}:{userId}:{faCode}`, `groups:{tenantId}:{appId}:{userId}`
    - Configure TTL (default 300s, range 1-3600s, per-environment override)
    - _Requirements: 22.1, 22.2_

  - [x] 9.2 Implement cache invalidation logic in CacheInvalidationBehavior
    - User permission assignment CRUD → invalidate `perm:*:{userId}:*` and `perm-effective:*:{userId}`
    - Group permission assignment change → invalidate all group members' permission cache
    - Group membership change → invalidate user's permission, effective, access, and groups cache
    - Access detail change → invalidate `access:*:{userId}:*` (for group: all members)
    - Entity status change (FA, PT, Permission, App, User) → broad prefix invalidation per design rules
    - Template application → combine user permission + group membership + access detail invalidation
    - _Requirements: 22.3, 22.4, 22.5, 22.6, 22.7_

  - [x] 9.3 Write property test: Cache Invalidation on Mutation
    - **Property 18: Cache Invalidation on Mutation**
    - Generate mutations (permission assignments, group changes, access detail changes); assert all affected cache keys are invalidated before response
    - **Validates: Requirements 22.3, 22.4, 22.5, 22.6**

- [x] 10. Identity Provider Validation Pipeline
  - [x] 10.1 Implement ITokenValidationService
    - Create `TokenValidationService : ITokenValidationService` in Infrastructure
    - Resolve IdentityProviderConfiguration by TenantId + optional ApplicationId
    - Reject if IdP is not Active
    - Route to appropriate validator by ProviderType: CustomJwtIssuer, OIDC types, SAML
    - CustomJwtIssuer: reject unsigned → check algorithm allowlist → validate issuer → validate audience → validate lifetime with clock skew (default 300s, 0-600s) → verify signature via JWKS/public key
    - OIDC types: use Microsoft.IdentityModel for standard OIDC validation
    - Extract claims via configured ClaimMappings → map to platform fields (Subject, Email, DisplayName, Groups, Roles, Tenant, Application)
    - Cache JWKS keys for 24h with retry on fetch
    - _Requirements: 3.5, 3.6, 3.7, 3.8, 3.9_

  - [x] 10.2 Write property test: CustomJwtIssuer Token Validation
    - **Property 22: CustomJwtIssuer Token Validation**
    - Generate tokens with varying signatures, algorithms, issuers, audiences, and lifetimes; assert accept iff all conditions met (signed, allowed algorithm, correct issuer, correct audience, valid lifetime, verifiable key)
    - **Validates: Requirements 3.5, 3.6, 3.7, 3.8**

- [x] 11. Permission Template Application
  - [x] 11.1 Implement ITemplateApplicationService
    - Create `TemplateApplicationService : ITemplateApplicationService` in Infrastructure
    - `ApplyTemplateAsync`: Validate template active → process PermissionTemplatePermission (with replace option) → process PermissionTemplateGroup (with replace option) → process PermissionTemplateAccessDetail (with replace option, calculate ValidFrom/ValidTo from offset days) → create UserPermissionTemplateApplication record → invalidate cache
    - `PreviewTemplateAsync`: Compute projected changes without persisting (directPermissionsToAdd, groupsToAdd, accessDetailsToAdd, existingPermissionsUnaffected, conflicts)
    - Apply within a single transaction; roll back on any failure
    - _Requirements: 15.1–15.9_

  - [x] 11.2 Write property test: Template Materialization Completeness
    - **Property 15: Template Materialization Completeness**
    - Generate templates with permission/group/access detail entries; assert every entry produces a corresponding materialized record
    - **Validates: Requirements 15.1, 15.2, 15.3**

  - [x] 11.3 Write property test: Template Replace Semantics
    - **Property 16: Template Replace Semantics**
    - Generate existing assignments + template with replace flags; assert only template-derived records remain after apply
    - **Validates: Requirements 15.4, 15.5, 15.6**

  - [x] 11.4 Write property test: Template Preview Side-Effect Freedom
    - **Property 17: Template Preview Side-Effect Freedom**
    - Generate template preview requests; assert no database state change occurs
    - **Validates: Requirements 15.8**

- [x] 12. Checkpoint — Core services compile and property tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 13. API layer — middleware pipeline, controllers, and filters
  - [x] 13.1 Implement middleware pipeline
    - Create `ExceptionHandlingMiddleware` — catch unhandled exceptions, convert to standard envelope with sanitized message, log full details
    - Create `CorrelationIdMiddleware` — generate UUID v4 per request, add to HttpContext.Items and response headers
    - Create `ApiCallLogMiddleware` — call IApiCallLogService.BeginLogAsync on entry, CompleteLogAsync on exit (status, duration); if logging fails, continue processing with diagnostic warning
    - Create `TokenValidationMiddleware` — validate bearer token or API key; set claims principal; return 401 if invalid
    - Create `TenantResolutionMiddleware` — resolve tenant by priority: route > token > ClientId > API key; return 403 if unresolvable; verify cross-tenant authorization
    - Create `AdminAuthorizationMiddleware` — verify required admin scopes from token; return 403 if insufficient
    - Register middleware in correct order in `Program.cs`
    - _Requirements: 16.1, 16.2, 16.4, 16.6, 18.1–18.6, 19.5, 20.1–20.5, 21.1–21.4_

  - [x] 13.2 Implement API response envelope and filters
    - Create `ApiEnvelope<T>` model with success, data, errors, correlationId
    - Create `EnvelopeResultFilter` to wrap all responses in standard envelope
    - Create `ValidationExceptionFilter` to convert FluentValidation failures to 400 envelope
    - Create rate limiting configuration using ASP.NET Core rate limiting middleware
    - _Requirements: 21.1–21.4, 23.7_

  - [x] 13.3 Implement entity CRUD controllers
    - `TenantsController` — POST, GET, GET/{id}, PUT/{id}, DELETE/{id}
    - `ApplicationsController` — scoped under tenants; CRUD operations
    - `IdentityProvidersController` — scoped under tenants; CRUD operations
    - `UsersController` — POST (sync), GET, GET/{id}, PUT/{id}
    - `FunctionalAreasController` — scoped under tenant/application; CRUD
    - `PermissionTypesController` — scoped under tenant/application; CRUD
    - `PermissionsController` — scoped under tenant/application; CRUD
    - `GroupsController` — CRUD + membership endpoints (POST membership, DELETE membership)
    - `PermissionAssignmentsController` — user + group assignment CRUD
    - `AccessDetailsController` — user + group access details + FA requirements CRUD
    - `PermissionTemplatesController` — CRUD + POST apply + POST preview
    - Each controller uses MediatR to dispatch commands/queries
    - _Requirements: 1.1–1.9, 2.1–2.7, 3.1–3.10, 4.1–4.8, 5.1–5.7, 6.1–6.7, 7.1–7.7, 8.1–8.8, 9.1–9.7, 11.1–11.6, 12.1–12.5, 13.1–13.6, 14.1–14.8, 15.1–15.9_

  - [x] 13.4 Implement access check and log controllers
    - `AccessChecksController` — POST single check (by FA+PT code or PermissionCode or ExternalSubjectId), POST batch (max 50), GET effective permissions
    - `AuditLogsController` — GET with query filters, pagination (max 100, sorted by CreatedAt DESC)
    - `ApiCallLogsController` — GET with query filters
    - `HealthController` — GET /health verifying DB, Redis (if enabled), Key Vault accessibility (respond within 5s)
    - _Requirements: 10.1, 10.2, 10.5, 10.6, 10.12, 16.5, 17.6, 30.4, 30.5_

  - [x] 13.5 Implement Program.cs and DI registration
    - Register all services, repositories, middleware, MediatR, FluentValidation, caching, Application Insights
    - Configure Swagger/OpenAPI for local development
    - Configure App Configuration integration with sentinel-based refresh
    - Configure Key Vault integration (fail startup if unreachable)
    - Configure TLS 1.2+ enforcement
    - Create `IProductConfiguration` implementation reading from `Platform:DisplayName` config key
    - _Requirements: 23.2, 23.3, 23.4, 23.9, 26.5, 28.1, 28.3, 28.4, 30.1, 30.2_

  - [x] 13.6 Write property test: API Response Envelope Consistency
    - **Property 19: API Response Envelope Consistency**
    - Generate various API responses (success/failure); assert envelope always contains success, data, errors, correlationId with correct structure
    - **Validates: Requirements 21.1, 21.2, 21.3, 21.4**

  - [x] 13.7 Write property test: Tenant Resolution Priority
    - **Property 8: Tenant Resolution Priority**
    - Generate requests with various combinations of route param, token claim, ClientId, API key; assert resolution follows strict priority order
    - **Validates: Requirements 18.1, 18.2, 18.3, 18.4**

  - [x] 13.8 Write property test: Administrative Scope Enforcement
    - **Property 23: Administrative Scope Enforcement**
    - Generate callers with various scope combinations and target endpoints; assert access granted iff caller holds required scope OR SecurityService.Admin
    - **Validates: Requirements 20.2, 20.3**

- [x] 14. Checkpoint — API compiles and Swagger accessible
  - Ensure all tests pass, ask the user if questions arise.

- [x] 15. Cross-cutting concerns — audit, security, observability
  - [x] 15.1 Implement IAuditService with transactional guarantees
    - Create `AuditService : IAuditService` that creates AuditLog records within the same transaction as entity changes
    - Capture BeforeJson (null for creates), AfterJson (full state), ChangedFieldsJson, Action, CorrelationId, ApiCallLogId, actor metadata
    - If audit record creation fails, entire transaction rolls back (Requirement 17.5)
    - Create separate AuditLog records per entity affected (e.g., template apply → multiple records)
    - Prevent UPDATE/DELETE on AuditLog and ApiCallLog tables (no API endpoints for mutation)
    - _Requirements: 17.1–17.7_

  - [x] 15.2 Implement IApiCallLogService
    - Create `ApiCallLogService : IApiCallLogService`
    - BeginLogAsync: create ApiCallLog with request metadata (HttpMethod, Endpoint, RequestPath, CallerSubjectId, CallerClientId, SourceIp, UserAgent, RequestTimestamp, CorrelationId)
    - CompleteLogAsync: update with StatusCode, DurationMs, ResponseTimestamp
    - Ensure sensitive data (request bodies, raw tokens) not stored
    - If logging fails, emit diagnostic warning but do not block request
    - _Requirements: 16.1–16.6_

  - [x] 15.3 Configure Application Insights and structured logging
    - Add Application Insights SDK with structured logging (timestamp, severity, CorrelationId, source component, message)
    - Configure custom metrics: permission checks/sec, cache hit rate
    - Track request duration, dependency call duration, unhandled exceptions
    - Redact sensitive values (secrets, keys, tokens) from all log output
    - _Requirements: 23.8, 30.1, 30.2, 30.3_

  - [x] 15.4 Write property test: Audit Log Completeness
    - **Property 20: Audit Log Completeness**
    - Generate state-changing operations; assert AuditLog records are created with correct BeforeJson, AfterJson, ApiCallLogId, CorrelationId, and actor metadata
    - **Validates: Requirements 17.1, 17.2, 17.3**

  - [x] 15.5 Write property test: Log Immutability
    - **Property 21: Log Immutability**
    - Attempt to modify/delete existing ApiCallLog and AuditLog records; assert all attempts are rejected
    - **Validates: Requirements 16.3, 17.4**

- [x] 16. Additional property tests for data integrity
  - [x] 16.1 Write property test: Cross-Tenant Data Isolation
    - **Property 7: Cross-Tenant Data Isolation**
    - Generate non-Super-Admin requests targeting a different tenant's data; assert read returns no data and write applies no changes (authorization error returned)
    - **Validates: Requirements 2.5, 8.6, 19.2, 19.3**

  - [x] 16.2 Write property test: Immutable Fields Preserved on Update
    - **Property 9: Immutable Fields Preserved on Update**
    - Generate entity update operations with payloads including immutable field values; assert those fields remain unchanged after update
    - **Validates: Requirements 1.2, 2.3**

  - [x] 16.3 Write property test: Uniqueness Constraint Enforcement
    - **Property 10: Uniqueness Constraint Enforcement**
    - Generate creation/update requests that would produce duplicate values on uniqueness-constrained fields; assert conflict error is returned
    - **Validates: Requirements 1.4, 2.4, 3.4, 4.3, 5.2, 6.2, 7.2, 8.2, 8.4, 11.3, 12.2, 14.5, 14.8**

  - [x] 16.4 Write property test: Input Validation Rejection
    - **Property 11: Input Validation Rejection**
    - Generate requests with invalid input (missing required fields, exceeding lengths, invalid formats); assert validation error with failing fields identified, no entity modified
    - **Validates: Requirements 1.7, 2.2, 4.6, 9.7, 23.6**

  - [x] 16.5 Write property test: ValidFrom Must Not Exceed ValidTo
    - **Property 12: ValidFrom Must Not Exceed ValidTo**
    - Generate permission assignments with ValidFrom > ValidTo; assert rejection
    - **Validates: Requirements 9.7**

  - [x] 16.6 Write property test: Product Name Configuration Fallback
    - **Property 24: Product Name Configuration Fallback**
    - Generate various configuration values (null, empty, >100 chars, valid); assert correct fallback behavior
    - **Validates: Requirements 28.1, 28.4**

- [x] 17. Local development environment
  - [x] 17.1 Create Docker Compose configuration
    - Create `docker-compose.yml` with SQL Server 2022 (port 1433) and Azurite (ports 10000-10002)
    - Create `docker-compose.override.yml` for development settings
    - Configure local appsettings.Development.json pointing to containerized SQL Server and Azurite
    - Configure in-memory caching for local environment
    - Enable Swagger UI
    - _Requirements: 26.1, 26.4, 26.5_

  - [x] 17.2 Create seed data scripts
    - Create EF Core seed data (or startup seeder service) populating: 1 Tenant, 1 Application, 1 Identity Provider configuration, 3 Functional Areas, 3 Functional Area Access Requirements, 5 Permission Types, 8 Permissions, 2 Groups, 3 Users, 2 Permission Templates with entries, user and group permission assignments, user access details, group access details, template access details, sample template application history, sample audit logs, sample API call logs
    - Apply seed only in development environment
    - _Requirements: 26.2_

  - [x] 17.3 Create migration application tooling
    - Document CLI command for manual migration: `dotnet ef database update`
    - Implement automatic migration on local startup (conditional on environment)
    - _Requirements: 26.3_

- [x] 18. Checkpoint — Local environment starts and seed data loads
  - Ensure all tests pass, ask the user if questions arise.

- [x] 19. Admin Portal (React frontend)
  - [x] 19.1 Scaffold React Admin Portal project
    - Initialize Vite + React 18 + TypeScript project in `src/Heimdall.AdminPortal/`
    - Install dependencies: Tailwind CSS, @azure/msal-react, Axios, TanStack Query, React Router v6, React Hook Form, Zod
    - Configure MSAL authentication with login/logout flows
    - Set up Axios interceptor for bearer token attachment and API base URL
    - Configure TanStack Query client
    - _Requirements: 24.5_

  - [x] 19.2 Implement shared layout and navigation
    - Create app shell with navigation sidebar listing all entity types
    - Display configured product name (from API) in page title, navigation header, and login screen
    - Implement responsive layout with Tailwind CSS
    - Create reusable components: DataTable, Form, Modal, ConfirmDialog, Pagination, StatusBadge
    - _Requirements: 24.1, 28.2_

  - [x] 19.3 Implement entity CRUD screens
    - Create list/create/edit/deactivate screens for all entity types: Tenants, Applications, Identity Providers, Functional Areas, FA Access Requirements, Permission Types, Permissions, Users, Groups, Group Memberships, Group Access Details, User Access Details, User Permission Assignments, Group Permission Assignments, Permission Templates, Template Application History, Audit Logs, API Call Logs
    - Each screen: list with search/filter, create form with Zod validation, edit form, deactivate confirmation
    - Audit Log and API Call Log screens: filterable, paginated read-only views with JSON detail expansion
    - _Requirements: 24.1_

  - [x] 19.4 Implement Template Builder screen
    - Multi-step wizard: select application → create template (code, name, description) → add permissions (with effect) → add groups → add access details (with offset days) → preview → confirm
    - Implement template duplication
    - Implement template deactivation
    - Implement apply-to-user flow with replace options and preview
    - _Requirements: 24.2_

  - [x] 19.5 Implement Access Detail Lookup Simulator
    - Input form: user selector, application selector, functional area selector, optional permission type
    - Display: functional access decision, relevant access detail requirements, direct user access details, group-inherited access details, final combined set
    - _Requirements: 24.3_

  - [x] 19.6 Implement Permission Simulation screen
    - Input form: user, application, functional area, permission type, optional permission code, optional date/time
    - Display: allowed/denied decision, reason, direct assignments considered, group assignments considered, deny assignments, expired assignments ignored, inactive records ignored, template application history, final decision
    - _Requirements: 24.4_

- [x] 20. Checkpoint — Admin Portal builds and connects to API
  - Ensure all tests pass, ask the user if questions arise.

- [x] 21. Infrastructure as Code (Bicep)
  - [x] 21.1 Create Bicep modules for core Azure resources
    - Create `infra/` directory with modular Bicep files
    - `main.bicep` — orchestrates all modules with environment parameters
    - `sql.bicep` — Azure SQL Server + Database with TDE, configurable SKU per environment
    - `keyvault.bicep` — Key Vault with Managed Identity access policies
    - `appconfig.bicep` — App Configuration per environment with Platform:DisplayName key
    - `monitoring.bicep` — Application Insights + Log Analytics Workspace
    - `apim.bicep` — Azure API Management
    - _Requirements: 25.1, 25.2_

  - [x] 21.2 Create Bicep modules for compute and networking
    - `containerapp.bicep` — Container Apps (production) with autoscaling
    - `appservice.bicep` — App Service (dev/staging) with lower SKUs
    - `redis.bicep` — Azure Cache for Redis (Basic for staging, Standard/Premium for prod; none for dev)
    - `storage.bicep` — Azure Storage Account
    - `identity.bicep` — System-assigned Managed Identities
    - `network.bicep` — Private Endpoints (production only), IP restrictions (staging)
    - Parameterize per environment (development=lowest cost, staging=low cost production-like, production=high reliability with backup retention, monitoring, autoscaling, strict network)
    - _Requirements: 25.1, 25.2, 25.6_

  - [x] 21.3 Create Bicep parameter files for each environment
    - `parameters.dev.bicepparam` — lowest cost SKUs, no Redis, public access, auto deploy
    - `parameters.staging.bicepparam` — low cost, Basic Redis, IP restrictions, auto deploy
    - `parameters.prod.bicepparam` — high reliability, Premium Redis, private endpoints, 35-day backup, autoscaling, manual approval
    - _Requirements: 25.2_

- [x] 22. CI/CD pipelines (GitHub Actions)
  - [x] 22.1 Create GitHub Actions workflow files
    - `.github/workflows/ci.yml` — build → lint → test → security scan stages (runs on all PRs)
    - `.github/workflows/deploy-dev.yml` — triggered on push to dev branch; runs CI + database migration + deploy to development
    - `.github/workflows/deploy-staging.yml` — triggered on push to staging branch; runs CI + database migration + deploy to staging
    - `.github/workflows/deploy-prod.yml` — triggered on push to main branch; runs CI + database migration + manual approval gate + deploy to production
    - Each pipeline halts on any stage failure with failed stage name and error summary
    - _Requirements: 25.3, 25.4, 25.5_

  - [x] 22.2 Create reusable workflow components
    - Reusable build step: restore, build, publish .NET projects
    - Reusable test step: run all test projects with coverage reporting
    - Reusable security scan step: dependency vulnerability check
    - Reusable migration step: apply EF Core migrations to target environment
    - Reusable deploy step: build and push container image, deploy to target compute (Container Apps or App Service)
    - Admin Portal build: install deps, lint, build React app, deploy to Static Web App
    - _Requirements: 25.3_

- [x] 23. Integration and unit tests
  - [x] 23.1 Create test infrastructure (TestContainers, test auth)
    - Create `TestContainerFixture` using Testcontainers for SQL Server
    - Create `TestAuthHandler` for simulating authenticated requests with configurable tenants, scopes, and claims
    - Create `WebApplicationFactory<Program>` setup with test database and in-memory cache
    - Create FsCheck custom generators for domain entities (valid Tenants, Applications, Users, Permissions, Assignments, etc.)
    - _Requirements: 27.6_

  - [x] 23.2 Write unit tests for permission resolution
    - Deny overrides allow with conflicting assignments
    - Expired permission (outside ValidFrom/ValidTo) excluded
    - Inactive UserProfile → Deny
    - Inactive Application → excluded
    - Inactive FunctionalArea → excluded
    - Inactive PermissionType → excluded
    - Inactive Permission → excluded
    - Inactive Group → excluded from group inheritance
    - Group permission inheritance resolves to user
    - Direct user permission assignment resolves correctly
    - _Requirements: 27.1_

  - [x] 23.3 Write unit tests for template materialization
    - Template applies direct permission assignments to target user
    - Template applies group memberships to target user
    - Template applies access detail records with offset day calculation
    - Template replace option removes existing before materializing
    - Template preview returns projected assignments without persisting
    - _Requirements: 27.2_

  - [x] 23.4 Write unit tests for access detail resolution
    - User access details filtered by FA Access Requirement declarations
    - Group access detail inheritance combines with direct user details
    - Expired access detail (outside ValidFrom/ValidTo) excluded
    - Inactive access detail excluded from results
    - _Requirements: 27.3_

  - [x] 23.5 Write unit tests for cache invalidation
    - Permission assignment change invalidates affected user cache
    - Group permission assignment change invalidates all group members' cache
    - Group membership change invalidates affected user's permissions and access details cache
    - Access detail change invalidates affected user cache
    - _Requirements: 27.4_

  - [x] 23.6 Write unit tests for CustomJwtIssuer token validation
    - Unsigned token rejected
    - Weak/disallowed algorithm rejected
    - Invalid issuer rejected
    - Invalid audience rejected
    - Expired lifetime rejected
    - Valid token (correct issuer, audience, lifetime, allowed algorithm, verifiable key) accepted
    - _Requirements: 27.5_

  - [x] 23.7 Write integration tests for entity CRUD operations
    - Full CRUD lifecycle tests for: Tenant, Application, IdentityProviderConfiguration, UserProfile, FunctionalArea, PermissionType, Permission, Group, GroupMembership, PermissionAssignment (user + group), UserAccessDetail, GroupAccessDetail, FunctionalAreaAccessRequirement, PermissionTemplate
    - Each test: create → read → update → deactivate → verify state
    - _Requirements: 27.6_

  - [x] 23.8 Write integration tests for access check endpoints
    - Single permission check by FunctionalAreaCode + PermissionTypeCode
    - Single permission check by PermissionCode
    - Single permission check by ExternalSubjectId + IdentityProvider
    - Batch permission check (multiple permissions in single call)
    - Effective permissions retrieval
    - _Requirements: 27.7_

  - [x] 23.9 Write security tests
    - Cross-tenant read access denied (non-Super-Admin)
    - Cross-tenant write access denied (non-Super-Admin)
    - Invalid JWT rejected (401)
    - Token with wrong audience rejected
    - Token with wrong issuer rejected
    - Expired token rejected
    - Unsigned token rejected
    - Token using weak/disallowed algorithm rejected
    - Request without required admin scope rejected (403)
    - Audit log records cannot be modified/deleted via API
    - Sensitive values (secrets, keys, tokens) not present in log output
    - _Requirements: 27.8_

- [x] 24. Checkpoint — All tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 25. Final wiring and observability
  - [x] 25.1 Implement health check endpoint
    - Create `/health` endpoint verifying: database connectivity, Redis connectivity (if enabled), Key Vault accessibility
    - Return within 5 seconds; report degraded/unhealthy status indicating which dependency failed
    - _Requirements: 30.4, 30.5_

  - [x] 25.2 Configure Azure Monitor alerting rules (in Bicep)
    - Add alert rules to monitoring Bicep module: error rate > 5% in 5-min window, P95 latency > 3s, monthly availability < 99.9%
    - Configure action group for alert notifications
    - _Requirements: 30.7_

  - [x] 25.3 Implement resilience patterns
    - Azure SQL: retry with exponential backoff (3 retries) via EF Core connection resiliency
    - Redis: circuit breaker + fallback to direct DB computation
    - Key Vault: retry + local memory cache of secrets
    - External JWKS: retry + 24h key cache
    - App Configuration: sentinel-based refresh (30s interval)
    - _Requirements: 23.3, 28.3_

- [x] 26. Final checkpoint — Full system integration
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document using FsCheck with xUnit (100+ iterations per property)
- Unit tests validate specific examples and edge cases from Requirement 27
- Integration tests use Testcontainers for realistic SQL Server interaction
- The implementation uses C# / .NET 8+ throughout, following the Clean Architecture patterns defined in the design
- All 24 correctness properties from the design document are covered by property-based test tasks
- Admin Portal uses React 18 + TypeScript + Vite + Tailwind CSS + MSAL.js as specified

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["1.2"] },
    { "id": 2, "tasks": ["2.1"] },
    { "id": 3, "tasks": ["2.2", "2.3"] },
    { "id": 4, "tasks": ["3.1"] },
    { "id": 5, "tasks": ["3.2"] },
    { "id": 6, "tasks": ["3.3", "3.4"] },
    { "id": 7, "tasks": ["5.1"] },
    { "id": 8, "tasks": ["5.2", "5.3", "5.4", "5.5", "5.6", "5.7", "5.8", "5.9", "5.10", "5.11"] },
    { "id": 9, "tasks": ["7.1", "8.1", "9.1", "10.1", "11.1"] },
    { "id": 10, "tasks": ["7.2", "7.3", "7.4", "7.5", "7.6", "7.7", "8.2", "8.3", "9.2", "9.3", "10.2", "11.2", "11.3", "11.4"] },
    { "id": 11, "tasks": ["13.1", "13.2"] },
    { "id": 12, "tasks": ["13.3", "13.4", "13.5"] },
    { "id": 13, "tasks": ["13.6", "13.7", "13.8", "15.1", "15.2", "15.3"] },
    { "id": 14, "tasks": ["15.4", "15.5", "16.1", "16.2", "16.3", "16.4", "16.5", "16.6"] },
    { "id": 15, "tasks": ["17.1", "17.2", "17.3"] },
    { "id": 16, "tasks": ["19.1", "21.1"] },
    { "id": 17, "tasks": ["19.2", "21.2"] },
    { "id": 18, "tasks": ["19.3", "21.3"] },
    { "id": 19, "tasks": ["19.4", "19.5", "19.6", "22.1"] },
    { "id": 20, "tasks": ["22.2"] },
    { "id": 21, "tasks": ["23.1"] },
    { "id": 22, "tasks": ["23.2", "23.3", "23.4", "23.5", "23.6"] },
    { "id": 23, "tasks": ["23.7", "23.8", "23.9"] },
    { "id": 24, "tasks": ["25.1", "25.2", "25.3"] }
  ]
}
```
