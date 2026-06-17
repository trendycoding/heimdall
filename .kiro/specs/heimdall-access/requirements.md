# Requirements Document

## Introduction

Heimdall Access is an Azure-native security and access management platform that provides multi-tenant identity federation, permission management, access detail-based data-level filtering context, permission templates, and comprehensive audit logging. The platform delegates all authentication to trusted external identity providers and provides fine-grained authorization services to implementer applications. The product name is configurable to allow rebranding.

## Glossary

- **Platform**: The Heimdall Access security and access management system
- **Implementer**: An organization that deploys and uses the Platform to secure their applications
- **Tenant**: A customer organization registered in the Platform; represents complete data isolation boundary
- **Application**: A client application registered under a Tenant that uses the Platform for authorization
- **Identity_Provider**: A trusted external authentication system configured per Tenant (e.g., Entra External ID, Google, SAML provider)
- **Functional_Area**: A logical section or capability within an Application that can be independently secured
- **Permission_Type**: An implementer-defined category of permission (e.g., Read, Write, Execute, Approve) scoped to an Application
- **Permission**: A specific authorization grant combining a Functional_Area and Permission_Type
- **User_Profile**: A Platform-local record representing an externally authenticated user, linked by ExternalSubjectId
- **Group**: A named collection of User_Profiles scoped to an Application within a Tenant
- **Permission_Template**: A reusable blueprint containing permissions, group memberships, and access details that can be applied to users
- **User_Access_Detail**: An implementer-defined key-value record attached to a User_Profile providing data-level filtering context
- **Group_Access_Detail**: An implementer-defined key-value record attached to a Group providing data-level filtering context inherited by members
- **Functional_Area_Access_Requirement**: A declaration of which access detail types are relevant for a given Functional_Area
- **API_Call_Log**: An immutable record of every API request processed by the Platform
- **Audit_Log**: An immutable record of every meaningful state change with before/after snapshots
- **Effect**: The outcome of a permission assignment; either Allow or Deny
- **Claim_Mapping**: Configuration that maps token claims from an Identity_Provider to Platform-recognized fields
- **Tenant_Resolution**: The process of determining which Tenant a request belongs to via route parameter, token claim, client ID, or API key
- **Platform_Super_Admin**: An administrative role with cross-tenant access for platform-level operations
- **Admin_Portal**: The React-based management interface for administering the Platform
- **Correlation_Id**: A unique identifier that links an API call to all audit records it produces

## Requirements

### Requirement 1: Tenant Registration and Management

**User Story:** As an implementer, I want to register and manage tenant organizations, so that each customer has an isolated security boundary.

#### Acceptance Criteria

1. WHEN a valid tenant registration request is received, THE Platform SHALL create a Tenant record with TenantId, Name (1–128 characters), Slug (1–64 lowercase alphanumeric or hyphen characters, starting and ending with an alphanumeric character), Status set to Active, a valid PrimaryIdentityMode value, and audit fields (CreatedAt, CreatedBy)
2. WHEN a tenant update request is received with a valid TenantId, THE Platform SHALL update only the mutable fields (Name, Slug, PrimaryIdentityMode, Status) and record ModifiedAt and ModifiedBy, while preserving immutable fields (TenantId, CreatedAt, CreatedBy)
3. WHEN a tenant deactivation request is received for an Active or Suspended tenant, THE Platform SHALL set the Tenant Status to Inactive via soft delete and record ModifiedAt and ModifiedBy
4. THE Platform SHALL enforce uniqueness of Tenant Slug across all tenants including Inactive tenants
5. THE Platform SHALL include TenantId on all tenant-scoped database tables to enforce data isolation
6. IF a request targets a TenantId that does not exist, THEN THE Platform SHALL return a standardized error response indicating the tenant was not found
7. IF a tenant registration or update request fails validation (missing required fields, Name exceeding 128 characters, Slug exceeding 64 characters, Slug not matching the allowed format, or invalid PrimaryIdentityMode value), THEN THE Platform SHALL reject the request with a standardized error response indicating which fields failed validation
8. IF a tenant update or deactivation request targets a tenant with Status Inactive, THEN THE Platform SHALL reject the request with a standardized error response indicating the tenant is inactive
9. IF a tenant registration or update request specifies a Slug that is already in use by another tenant, THEN THE Platform SHALL reject the request with a standardized error response indicating the Slug conflict

### Requirement 2: Application Registration and Management

**User Story:** As a tenant administrator, I want to register client applications, so that each application has its own authorization configuration.

#### Acceptance Criteria

1. WHEN a valid application registration request is received, THE Platform SHALL create an Application record with ApplicationId, TenantId, Name (required, maximum 200 characters), Description (optional, maximum 1000 characters), ClientIdentifier (required, maximum 128 characters), AllowedRedirectUris (optional, maximum 20 entries, each a valid absolute URI), AllowedOrigins (optional, maximum 20 entries, each a valid origin URI), Status set to Active, and audit fields (CreatedAt, CreatedBy)
2. IF an application registration request is missing required fields (Name or ClientIdentifier) or any field exceeds its maximum length, THEN THE Platform SHALL reject the request and return a validation error indicating the failing fields
3. WHEN an application update request is received with a valid ApplicationId within the caller's Tenant, THE Platform SHALL update only the mutable fields (Name, Description, AllowedRedirectUris, AllowedOrigins) and record ModifiedAt and ModifiedBy; ApplicationId, TenantId, ClientIdentifier, and Status SHALL NOT be modifiable through the update operation
4. THE Platform SHALL enforce uniqueness of Application ClientIdentifier within a Tenant
5. IF a request references an ApplicationId belonging to a different Tenant, THEN THE Platform SHALL reject the request and return an authorization error
6. WHEN an application deactivation request is received, THE Platform SHALL set the Application Status to Inactive via soft delete and record ModifiedAt and ModifiedBy
7. IF a request targets an ApplicationId that does not exist within the caller's Tenant, THEN THE Platform SHALL return a standardized error response with an appropriate error code

### Requirement 3: Identity Provider Configuration

**User Story:** As a tenant administrator, I want to configure trusted identity providers, so that users can authenticate through their organization's identity system.

#### Acceptance Criteria

1. WHEN a valid identity provider configuration request is received, THE Platform SHALL create an IdentityProviderConfiguration record with ProviderId, TenantId, ApplicationId (nullable), ProviderType, Name, Issuer, Audience, ClientId, JWKS endpoint, SAML metadata URL, Allowed algorithms, Claim mappings, Clock skew tolerance, Status, and audit fields
2. THE Platform SHALL support the following ProviderType values: EntraExternalId, AzureAdB2C, EntraWorkforce, Google, Facebook, ExternalOidc, ExternalSaml, CustomJwtIssuer
3. WHEN a Claim_Mapping is configured, THE Platform SHALL store a mapping entry for each Platform-recognized field (Subject, Email, DisplayName, Groups, Roles, Tenant, and Application) specifying the source claim name from the identity provider token that maps to that field
4. THE Platform SHALL enforce uniqueness of the combination of TenantId, ApplicationId, ProviderType, and Name across IdentityProviderConfiguration records
5. IF a CustomJwtIssuer token is unsigned, THEN THE Platform SHALL reject the token and return a standardized error response indicating that unsigned tokens are not accepted
6. IF a CustomJwtIssuer token uses a disallowed signing algorithm (including the "none" algorithm or any algorithm not present in the provider's Allowed algorithms list), THEN THE Platform SHALL reject the token and return a standardized error response indicating the algorithm is not permitted
7. WHEN validating a CustomJwtIssuer token, THE Platform SHALL validate that the token issuer matches the configured Issuer, the audience matches the configured Audience, the token lifetime has not expired, and the signing key can be verified against the configured keys
8. WHEN validating a CustomJwtIssuer token, THE Platform SHALL apply the configured Clock skew tolerance (defaulting to 300 seconds if not specified, configurable between 0 and 600 seconds) when evaluating token lifetime, and SHALL accept signing keys from a configured JWKS URL or an uploaded public key
9. IF a token validation request references an IdentityProviderConfiguration with a Status other than Active, THEN THE Platform SHALL reject the token and return a standardized error response indicating that the identity provider is not active
10. IF an identity provider configuration request would violate the uniqueness constraint on TenantId, ApplicationId, ProviderType, and Name, THEN THE Platform SHALL reject the request and return a standardized error response indicating a duplicate configuration

### Requirement 4: User Sync and Management

**User Story:** As a tenant administrator, I want to register or sync users from trusted identity providers, so that external users are represented in the Platform for authorization purposes.

#### Acceptance Criteria

1. WHEN a user sync request is received with a valid ExternalSubjectId and IdentityProvider reference, THE Platform SHALL create or update a UserProfile record with UserProfileId, TenantId, ExternalSubjectId, IdentityProvider, Email, DisplayName, Status, and audit fields, setting Status to Active on initial creation
2. WHEN a user authenticates successfully, THE Platform SHALL update the LastLoginAt timestamp on the corresponding UserProfile
3. THE Platform SHALL enforce uniqueness of ExternalSubjectId combined with IdentityProvider within a Tenant
4. THE Platform SHALL NOT store passwords or implement custom password authentication
5. IF a user sync request references an identity provider not configured for the Tenant, THEN THE Platform SHALL reject the request with an error response indicating the identity provider is not configured for the Tenant
6. IF a user sync request is missing ExternalSubjectId, IdentityProvider, or Email, THEN THE Platform SHALL reject the request with an error response identifying the missing fields
7. WHEN a user sync request includes an applications array containing applicationId and permissionTemplateCodes, THE Platform SHALL apply the specified Permission Templates to the user for each referenced Application after the UserProfile is created or updated
8. IF a user sync request references a permissionTemplateCode that does not exist or is inactive within the specified Application, THEN THE Platform SHALL reject the request with an error response identifying the invalid template code

### Requirement 5: Functional Area Definition

**User Story:** As an application administrator, I want to define functional areas within an application, so that I can establish independently securable sections of the application.

#### Acceptance Criteria

1. WHEN a functional area creation request is received with a FunctionalAreaCode (max 50 characters, uppercase alphanumeric and underscores only), a Name (max 200 characters), and a reference to an existing Application within the caller's Tenant, THE Platform SHALL create a FunctionalArea record with FunctionalAreaId, TenantId, ApplicationId, FunctionalAreaCode, Name, Description (max 1000 characters, optional), IsActive defaulting to true, and audit fields (CreatedAt, CreatedBy)
2. THE Platform SHALL enforce uniqueness of FunctionalAreaCode within an Application
3. IF a functional area creation request supplies a FunctionalAreaCode that already exists within the target Application, THEN THE Platform SHALL reject the request with a standardized error response indicating a uniqueness constraint violation
4. WHEN a functional area update request is received with a valid FunctionalAreaId within the caller's Tenant, THE Platform SHALL update the specified mutable fields (Name, Description) and record ModifiedAt and ModifiedBy
5. WHEN a functional area deactivation request is received, THE Platform SHALL set IsActive to false and record ModifiedAt and ModifiedBy
6. IF a permission resolution request references an inactive FunctionalArea, THEN THE Platform SHALL exclude permissions associated with that FunctionalArea from the evaluation
7. IF a functional area creation or update request references an ApplicationId that does not exist or belongs to a different Tenant, THEN THE Platform SHALL reject the request with a standardized error response

### Requirement 6: Permission Type Definition

**User Story:** As an application administrator, I want to define custom permission types per application, so that authorization categories match my application's domain language.

#### Acceptance Criteria

1. WHEN a valid permission type creation request is received, THE Platform SHALL create a PermissionType record with PermissionTypeId, TenantId, ApplicationId, Code (maximum 100 characters, alphanumeric and underscores only, case-insensitive for uniqueness), Name (maximum 200 characters), Description (maximum 1000 characters), IsSystemReserved (default false), IsActive (default true), and audit fields (CreatedAt, CreatedBy)
2. THE Platform SHALL enforce uniqueness of PermissionType Code within an Application
3. IF a permission type creation or update request contains a Code that already exists within the same Application, THEN THE Platform SHALL reject the request with a standardized error response indicating a uniqueness constraint violation
4. THE Platform SHALL allow implementers to define permission types without hardcoding any predefined set
5. IF a permission type is marked IsSystemReserved, THEN THE Platform SHALL prevent deletion of that PermissionType and return a standardized error response indicating that system-reserved permission types cannot be deleted
6. WHEN a permission type deactivation request is received, THE Platform SHALL set IsActive to false and record ModifiedAt and ModifiedBy
7. IF a permission creation request references an inactive PermissionType, THEN THE Platform SHALL reject the request with a standardized error response indicating that inactive permission types cannot be assigned to new permissions

### Requirement 7: Permission Definition

**User Story:** As an application administrator, I want to define functional permissions combining a functional area and permission type, so that I can create granular authorization controls.

#### Acceptance Criteria

1. WHEN a valid permission creation request is received, THE Platform SHALL create a Permission record with PermissionId, TenantId, ApplicationId, FunctionalAreaId, PermissionTypeId, PermissionCode (maximum 200 characters, uppercase alphanumeric and underscores only), Name, Description, IsActive (default true), and audit fields
2. THE Platform SHALL enforce uniqueness of PermissionCode within an Application
3. THE Platform SHALL associate each Permission with exactly one FunctionalArea and one PermissionType
4. IF a permission references a FunctionalArea or PermissionType from a different Application, THEN THE Platform SHALL reject the request with a standardized error response
5. IF a permission creation request supplies a PermissionCode that already exists within the target Application, THEN THE Platform SHALL reject the request with a standardized error response indicating a uniqueness constraint violation
6. WHEN a permission deactivation request is received, THE Platform SHALL set IsActive to false and record ModifiedAt and ModifiedBy
7. IF a permission assignment request references an inactive Permission, THEN THE Platform SHALL reject the request with a standardized error response indicating that inactive permissions cannot be newly assigned

### Requirement 8: Group Management

**User Story:** As an application administrator, I want to create groups and manage group memberships, so that I can assign permissions to collections of users efficiently.

#### Acceptance Criteria

1. WHEN a valid group creation request is received, THE Platform SHALL create a Group record with GroupId, TenantId, ApplicationId, Name (unique per ApplicationId), Description, IsActive (default true), and audit fields
2. THE Platform SHALL enforce uniqueness of Group Name within an Application
3. WHEN a valid group membership request is received, THE Platform SHALL create a GroupMembership record linking a UserProfile to a Group with GroupMembershipId, TenantId, ApplicationId, GroupId, UserProfileId, CreatedAt, and CreatedBy
4. THE Platform SHALL enforce that a UserProfile can be a member of a Group only once within the same Application
5. IF a group membership request would create a duplicate membership for the same UserProfile and Group, THEN THE Platform SHALL reject the request with a standardized error response indicating the user is already a member
6. IF a group membership request references a UserProfile or Group from a different Tenant, THEN THE Platform SHALL reject the request with a standardized error response
7. WHEN a group deactivation request is received, THE Platform SHALL set IsActive to false and record ModifiedAt and ModifiedBy
8. WHILE evaluating permissions, THE Platform SHALL exclude permission assignments from inactive Groups

### Requirement 9: Permission Assignment

**User Story:** As a permission manager, I want to assign permissions directly to users and to groups with allow/deny effects and validity periods, so that I can control access with temporal precision.

#### Acceptance Criteria

1. WHEN a user permission assignment request is received with a valid TenantId, ApplicationId, UserProfileId, PermissionId, and Effect (Allow or Deny), THE Platform SHALL create a UserPermissionAssignment record storing the provided Effect, ValidFrom, and ValidTo values
2. WHEN a group permission assignment request is received with a valid TenantId, ApplicationId, GroupId, PermissionId, and Effect (Allow or Deny), THE Platform SHALL create a GroupPermissionAssignment record storing the provided Effect, ValidFrom, and ValidTo values
3. IF a permission assignment request references a nonexistent TenantId, ApplicationId, UserProfileId, GroupId, or PermissionId, THEN THE Platform SHALL reject the request with an error message indicating which referenced entity was not found
4. WHILE evaluating permissions, THE Platform SHALL treat assignments with a null ValidFrom as valid from the beginning of time, treat assignments with a null ValidTo as valid indefinitely, and ignore assignments where the current time is outside the ValidFrom to ValidTo range
5. WHILE evaluating permissions for a user who holds both Allow and Deny assignments for the same permission at the current time, THE Platform SHALL apply the Deny effect, overriding any Allow assignments
6. THE Platform SHALL allow the same user to hold different permission assignments in different Applications within the same Tenant
7. IF a permission assignment request specifies a ValidFrom that is later than ValidTo, THEN THE Platform SHALL reject the request with an error message indicating that ValidFrom must not be later than ValidTo

### Requirement 10: Permission Resolution and Access Checks

**User Story:** As a client application, I want to evaluate runtime access for a user against functional areas and permissions, so that I can enforce authorization decisions in real time.

#### Acceptance Criteria

1. WHEN a single permission check request is received with a UserProfileId and a FunctionalAreaCode combined with a PermissionTypeCode, THE Platform SHALL evaluate all applicable direct user assignments and group-inherited assignments and return a response containing an allowed boolean, a decision of Allow or Deny, a reason explanation, and a list of matchedAssignments that contributed to the decision
2. WHEN a single permission check request is received with a UserProfileId and a PermissionCode, THE Platform SHALL evaluate all applicable direct user assignments and group-inherited assignments and return a response containing an allowed boolean, a decision of Allow or Deny, a reason explanation, and a list of matchedAssignments that contributed to the decision
3. WHILE resolving permissions, THE Platform SHALL apply the rule that Deny overrides Allow when conflicting assignments exist
4. WHILE resolving permissions, THE Platform SHALL exclude assignments linked to inactive Permissions, FunctionalAreas, or PermissionTypes
5. WHEN a batch permission check request is received with a maximum of 50 permission checks, THE Platform SHALL evaluate each permission in a single call and return individual results for each permission check in the batch
6. WHEN an effective permissions request is received for a UserProfileId within an Application, THE Platform SHALL return all resolved permissions with their effective Allow or Deny status
7. THE Platform SHALL enforce Tenant isolation during all permission resolution operations
8. THE Platform SHALL respond to permission check requests within 100ms at the 95th percentile when cached results are available
9. THE Platform SHALL respond to permission check requests within 300ms at the 95th percentile when cached results are not available
10. IF the target UserProfile has a Status of Inactive, THEN THE Platform SHALL return a Deny decision without evaluating permission assignments
11. IF no applicable permission assignments exist for the requested user and permission combination, THEN THE Platform SHALL return a Deny decision by default
12. WHEN a single permission check request is received with an ExternalSubjectId and IdentityProvider instead of a UserProfileId, THE Platform SHALL resolve the corresponding UserProfile and then evaluate permissions using the same resolution rules

### Requirement 11: User Access Details

**User Story:** As an application administrator, I want to define user access detail records providing data-level filtering context, so that client applications can apply row-level or entity-level access restrictions.

#### Acceptance Criteria

1. WHEN a valid user access detail creation request is received, THE Platform SHALL create a UserAccessDetail record with UserAccessDetailId, TenantId, ApplicationId, UserProfileId, AccessDetailType, AccessDetailCode, AccessDetailValue (maximum 500 characters), Description (maximum 1000 characters), IsActive, ValidFrom, ValidTo, and audit fields
2. THE Platform SHALL allow implementers to define AccessDetailType and AccessDetailCode values without hardcoding any predefined set
3. THE Platform SHALL enforce uniqueness of UserProfileId combined with ApplicationId, AccessDetailType, and AccessDetailCode
4. WHEN a user access detail lookup request is received for a specific UserProfileId and FunctionalAreaCode, THE Platform SHALL first validate that the user has functional permission to the area, then return all active and non-expired UserAccessDetail records matching the access detail types declared as relevant by the FunctionalAreaAccessRequirement for that FunctionalArea
5. THE Platform SHALL return structured access detail records to the client application with a source indicator (DirectUser or Group) for each record; the Platform SHALL NOT apply data filtering directly
6. THE Platform SHALL respond to access detail lookup requests within 150ms at the 95th percentile when cached results are available
7. THE Platform SHALL respond to access detail lookup requests within 400ms at the 95th percentile when cached results are not available

### Requirement 12: Group Access Details

**User Story:** As an application administrator, I want to attach access detail records to groups, so that group members inherit data-level filtering context from their group memberships.

#### Acceptance Criteria

1. WHEN a valid group access detail creation request is received with a unique combination of GroupId, ApplicationId, AccessDetailType, and AccessDetailCode, THE Platform SHALL create a GroupAccessDetail record containing GroupAccessDetailId, TenantId, ApplicationId, GroupId, AccessDetailType, AccessDetailCode, AccessDetailValue (maximum 500 characters), Description (maximum 1000 characters), IsActive, ValidFrom, ValidTo, and audit fields
2. IF a group access detail creation request contains a GroupId, ApplicationId, AccessDetailType, and AccessDetailCode combination that already exists, THEN THE Platform SHALL reject the request with an error message indicating the duplicate combination
3. IF a group access detail creation request references a GroupId that does not exist or belongs to an inactive group, THEN THE Platform SHALL reject the request with an error message indicating the invalid group reference
4. WHEN a user access detail lookup is performed, THE Platform SHALL return the user's direct UserAccessDetail records combined with GroupAccessDetail records inherited from all active groups the user belongs to, excluding any GroupAccessDetail records where IsActive is false or the current date falls outside the ValidFrom to ValidTo range
5. WHEN returning combined access detail results, THE Platform SHALL include a source indicator on each record specifying either DirectUser for direct assignments or Group with the associated groupId and groupName for inherited assignments

### Requirement 13: Functional Area Access Requirements

**User Story:** As an application administrator, I want to declare which access detail types are relevant for each functional area, so that access detail lookups return only contextually appropriate records.

#### Acceptance Criteria

1. WHEN a functional area access requirement creation request is received with a valid FunctionalAreaId, a valid AccessDetailType, an IsRequired flag, and an optional Description of 500 characters or fewer, THE Platform SHALL create a FunctionalAreaAccessRequirement record linking the FunctionalArea to the AccessDetailType within the specified Tenant and Application context
2. IF a creation request specifies a FunctionalAreaId and AccessDetailType combination that already exists and is active, THEN THE Platform SHALL reject the request with an error indicating a duplicate access requirement
3. WHEN an access detail lookup is performed for a FunctionalArea, THE Platform SHALL return only access detail records whose AccessDetailType matches an active FunctionalAreaAccessRequirement declared for that FunctionalArea
4. IF a FunctionalArea has no active FunctionalAreaAccessRequirement records, THEN THE Platform SHALL determine access based solely on functional permissions without filtering by access detail type
5. WHEN a FunctionalAreaAccessRequirement is soft-deleted by setting IsActive to false, THE Platform SHALL exclude that AccessDetailType from subsequent access detail lookups for the associated FunctionalArea
6. THE Platform SHALL persist the IsRequired flag on each FunctionalAreaAccessRequirement to indicate whether the associated AccessDetailType is mandatory or optional for access to that FunctionalArea

### Requirement 14: Permission Templates

**User Story:** As a permission manager, I want to create reusable permission templates containing permissions, group memberships, and access details, so that I can standardize and accelerate user provisioning.

#### Acceptance Criteria

1. WHEN a valid permission template creation request is received, THE Platform SHALL create a PermissionTemplate record with PermissionTemplateId, TenantId, ApplicationId, TemplateCode, Name, Description, IsActive, and audit fields
2. THE Platform SHALL support PermissionTemplatePermission entries with Effect (Allow or Deny) and optional ValidFromOffsetDays and ValidToOffsetDays representing the number of days relative to the time the template is applied to a user
3. THE Platform SHALL support PermissionTemplateGroup entries linking templates to Groups
4. THE Platform SHALL support PermissionTemplateAccessDetail entries with AccessDetailType, AccessDetailCode, AccessDetailValue, Description, and optional ValidFromOffsetDays and ValidToOffsetDays representing the number of days relative to the time the template is applied to a user
5. THE Platform SHALL enforce uniqueness of TemplateCode within an Application
6. IF a permission template entry references a Permission, Group, or AccessDetailType belonging to a different Application or Tenant, THEN THE Platform SHALL reject the request with a standardized error response
7. IF a template application request targets a PermissionTemplate where IsActive is false, THEN THE Platform SHALL reject the request with a standardized error response indicating the template is inactive
8. THE Platform SHALL enforce that a PermissionTemplatePermission entry for a given PermissionId is unique within a PermissionTemplate

### Requirement 15: Permission Template Application

**User Story:** As a permission manager, I want to apply a permission template to a user to materialize all template entries into direct assignments, so that user provisioning is efficient and traceable.

#### Acceptance Criteria

1. WHEN a template application request is received with applyDirectPermissions set to true, THE Platform SHALL materialize all PermissionTemplatePermission entries into UserPermissionAssignment records for the target user, calculating ValidFrom and ValidTo by adding the template's ValidFromOffsetDays and ValidToOffsetDays to the current date and time
2. WHEN a template application request is received with applyGroups set to true, THE Platform SHALL materialize all PermissionTemplateGroup entries into GroupMembership records for the target user
3. WHEN a template application request is received with applyAccessDetails set to true, THE Platform SHALL materialize all PermissionTemplateAccessDetail entries into UserAccessDetail records for the target user, calculating ValidFrom and ValidTo by adding the template's offset days to the current date and time
4. WHEN a template application request includes replaceExistingPermissions set to true, THE Platform SHALL remove all existing direct UserPermissionAssignment records for that user and Application before materializing new assignments from the template
5. WHEN a template application request includes replaceExistingGroups set to true, THE Platform SHALL remove all existing GroupMembership records for that user and Application before materializing new group memberships from the template
6. WHEN a template application request includes replaceExistingAccessDetails set to true, THE Platform SHALL remove all existing UserAccessDetail records for that user and Application before materializing new access details from the template
7. WHEN a template is applied, THE Platform SHALL create a UserPermissionTemplateApplication record with AppliedAt, AppliedBy, SourceIp, UserAgent, CorrelationId, and ApiCallLogId
8. WHEN a template preview request is received, THE Platform SHALL return the projected changes (directPermissionsToAdd, groupsToAdd, accessDetailsToAdd, existingPermissionsUnaffected, conflicts) without persisting any changes
9. IF a template application request targets a PermissionTemplate where IsActive is false, THEN THE Platform SHALL reject the request with a standardized error response indicating the template is inactive

### Requirement 16: API Call Logging

**User Story:** As a platform operator, I want every API call tracked with full request context, so that I can troubleshoot issues and correlate actions with audit records.

#### Acceptance Criteria

1. THE Platform SHALL create an ApiCallLog record for every API request with ApiCallLogId, TenantId, ApplicationId, CorrelationId, RequestId, HttpMethod, Endpoint, RequestPath, CallerSubjectId, CallerClientId, SourceIp, UserAgent, StatusCode, DurationMs, RequestTimestamp, and ResponseTimestamp
2. THE Platform SHALL generate a unique CorrelationId (UUID v4 format) for each API call and include the CorrelationId in the API response envelope
3. THE Platform SHALL NOT modify or delete ApiCallLog records after creation
4. THE Platform SHALL NOT store sensitive request bodies or raw JWT tokens in ApiCallLog records
5. THE Platform SHALL support querying API call logs filtered by TenantId, ApplicationId, CorrelationId, Endpoint, HttpMethod, SourceIp, CallerSubjectId, and date range
6. IF API call log creation fails, THE Platform SHALL still process the request but SHALL log a warning to the application diagnostics indicating the logging failure

### Requirement 17: Audit Logging

**User Story:** As a compliance officer, I want every meaningful change audited with before/after state and cross-referenced to the originating API call, so that I have a complete change history for security review.

#### Acceptance Criteria

1. WHEN a state change occurs on any auditable entity, THE Platform SHALL create an AuditLog record containing BeforeJson, AfterJson, ChangedFieldsJson, Action, and the following metadata: AuditLogId, TenantId, ApplicationId, CorrelationId, ApiCallLogId, ActorUserProfileId, ActorSubjectId, ActorEmail, EntityType, EntityId, SourceIp, UserAgent, and CreatedAt
2. WHEN an entity is created, THE Platform SHALL record BeforeJson as null and AfterJson as the full entity state; WHEN an entity is deleted or deactivated, THE Platform SHALL record BeforeJson as the full prior state and AfterJson as null or the deactivated state
3. THE Platform SHALL cross-reference each AuditLog record with the ApiCallLogId of the API call that caused the change, and SHALL assign a shared CorrelationId to all AuditLog records produced by a single API call
4. THE Platform SHALL NOT modify or delete AuditLog records after creation
5. IF audit log creation fails during a state-changing operation, THEN THE Platform SHALL fail the originating operation and return an error indicating that the change could not be audited
6. THE Platform SHALL support querying audit logs filtered by TenantId, ApplicationId, EntityType, EntityId, Action, UserId, date range, and CorrelationId, returning results sorted by CreatedAt descending with a maximum page size of 100 records
7. THE Platform SHALL record audit actions for all entity lifecycle events including creation, update, deactivation, permission assignment, template application, group membership changes, and access detail changes, generating a separate AuditLog record for each individual entity affected

### Requirement 18: Tenant Resolution

**User Story:** As a platform architect, I want tenant context resolved from multiple sources, so that different integration patterns can identify the correct tenant.

#### Acceptance Criteria

1. WHEN an API request contains a TenantId route parameter, THE Platform SHALL resolve the Tenant from the route parameter as the highest-priority source
2. WHEN an API request contains a tenant claim in the bearer token and no route parameter is present, THE Platform SHALL resolve the Tenant from the token claim
3. WHEN an API request contains a recognized ClientId and no higher-priority source is present, THE Platform SHALL resolve the Tenant from the Application registration linked to that ClientId
4. WHEN an API request uses an API key for service-to-service communication and no higher-priority source is present, THE Platform SHALL resolve the Tenant from the API key registration
5. IF Tenant resolution fails because no source is present, THEN THE Platform SHALL reject the request with a 403 status code
6. IF the route parameter resolves to a TenantId different from the token tenant claim, THE Platform SHALL use the route parameter but verify the caller is authorized for that Tenant before processing the request

### Requirement 19: Cross-Tenant Isolation

**User Story:** As a security architect, I want complete tenant isolation enforced at the data layer, so that no tenant can read or modify another tenant's data.

#### Acceptance Criteria

1. THE Platform SHALL include a TenantId filter on all database queries for tenant-scoped entities, applied automatically via the global query filter in Entity Framework Core regardless of whether application code includes an explicit WHERE clause
2. IF a request attempts to read data belonging to a different Tenant, THEN THE Platform SHALL reject the request with an authorization error response and return no data from the target Tenant, unless the caller holds the Platform_Super_Admin role
3. IF a request attempts to write data targeting a different Tenant, THEN THE Platform SHALL reject the request with an authorization error response and apply no changes to the target Tenant's data, unless the caller holds the Platform_Super_Admin role
4. THE Platform SHALL enforce tenant isolation at the database query level using global query filters in Entity Framework Core, including any raw SQL or direct query operations against tenant-scoped tables
5. IF the TenantId cannot be resolved from the incoming request, THEN THE Platform SHALL reject the request with an authorization error response and SHALL NOT execute any database query against tenant-scoped entities
6. WHEN a caller holding the Platform_Super_Admin role accesses data belonging to a different Tenant, THE Platform SHALL record an audit log entry containing the caller identity, the target TenantId, and the operation performed

### Requirement 20: Administrative Authorization

**User Story:** As a platform operator, I want service-level administrative roles, so that access to management operations is controlled.

#### Acceptance Criteria

1. THE Platform SHALL define the following administrative permission scopes: SecurityService.Admin (includes all scopes), TenantAdmin, ApplicationAdmin, IdentityProviderAdmin, PermissionManager, TemplateManager, AccessDetailManager, Auditor, ReadOnly
2. WHEN an administrative API is called, THE Platform SHALL verify the caller holds the required permission scope before processing the request, where SecurityService.Admin satisfies any scope requirement
3. IF a caller lacks the required administrative permission scope, THEN THE Platform SHALL reject the request with a 403 status code and log the access denial in the AuditLog
4. IF an API request arrives without a valid bearer token or API key, THEN THE Platform SHALL reject the request with a 401 status code before evaluating administrative permission scopes
5. THE Platform SHALL support assigning multiple administrative permission scopes to a single caller

### Requirement 21: API Response Standard

**User Story:** As a client application developer, I want consistent API response formatting, so that I can handle success and error cases uniformly.

#### Acceptance Criteria

1. THE Platform SHALL return all API responses in a standard envelope containing: success (boolean), data (response payload or null), errors (array of error objects, may be empty), and correlationId (a unique string matching the ApiCallLog CorrelationId for the request)
2. WHEN an API request succeeds, THE Platform SHALL set success to true, populate the data field with the response payload, set errors to an empty array, and return an HTTP status code of 200 for retrieval operations or 201 for resource creation operations
3. WHEN an API request fails, THE Platform SHALL set success to false, set data to null, and populate the errors array with at least one error object where each object contains a machine-readable code (e.g., "ValidationFailed", "TenantNotFound", "PermissionDenied", "ResourceConflict") and a human-readable message describing the failure
4. IF an API request fails, THEN THE Platform SHALL return the HTTP status code corresponding to the failure category: 400 for validation errors, 401 for unauthenticated requests, 403 for unauthorized requests, 404 for resources not found, 409 for resource conflicts, 429 for rate-limited requests, and 500 for internal errors

### Requirement 22: Caching

**User Story:** As a platform operator, I want permission check results and access detail lookups cached, so that repeated authorization decisions are fast.

#### Acceptance Criteria

1. THE Platform SHALL cache effective permission results and access detail lookup results using Redis in deployed environments and in-memory cache during local development, with a configurable TTL per environment that defaults to 300 seconds and supports a range of 1 to 3600 seconds
2. WHEN a cache entry does not exist for a requested permission check or access detail lookup, THE Platform SHALL compute the result from the authoritative data store, return it to the caller, and store it in the cache with the configured TTL
3. WHEN a permission assignment, group membership, or access detail record is created, updated, or deleted, THE Platform SHALL invalidate the affected cache entries before the mutating operation's response is returned to the caller
4. WHEN a group's permission assignments change, THE Platform SHALL invalidate cached permissions for all members of that group before the mutating operation's response is returned to the caller
5. WHEN a user's group membership changes, THE Platform SHALL invalidate cached permissions and access details for that user before the mutating operation's response is returned to the caller
6. WHEN a functional area, permission type, user status, group status, or application status change occurs, or a template is applied that modifies permissions or access details, THE Platform SHALL invalidate all cache entries whose keys reference the affected TenantId, ApplicationId, UserProfileId, FunctionalAreaId, or PermissionTypeCode before the mutating operation's response is returned to the caller
7. IF a cache entry remains after a permission denial or removal due to invalidation failure, THEN THE Platform SHALL guarantee the stale entry expires no later than the configured TTL and SHALL NOT serve a stale grant beyond that duration

### Requirement 23: Data Security

**User Story:** As a security architect, I want defense-in-depth protections applied at every layer, so that the platform meets enterprise security standards.

#### Acceptance Criteria

1. THE Platform SHALL encrypt all data at rest using Azure SQL Transparent Data Encryption
2. THE Platform SHALL require TLS 1.2 or higher for all network communication and SHALL reject connections using older TLS versions
3. THE Platform SHALL use Azure Managed Identity for service-to-service authentication to Azure resources
4. THE Platform SHALL store all secrets and connection strings in Azure Key Vault
5. THE Platform SHALL use parameterized queries for all database operations to prevent SQL injection
6. WHEN an API request fails input validation against the endpoint's defined schema, THE Platform SHALL reject the request with an error response indicating which fields failed validation, without processing the request
7. THE Platform SHALL enforce rate limiting on all public-facing API endpoints, and IF a client exceeds the configured rate limit for an endpoint, THEN THE Platform SHALL reject subsequent requests with a rate-limit-exceeded response until the rate window resets
8. THE Platform SHALL redact sensitive values (secrets, keys, tokens) from all log output
9. IF Azure Key Vault is unreachable during service startup, THEN THE Platform SHALL fail to start and SHALL log an error indicating the Key Vault connectivity failure

### Requirement 24: Admin Portal

**User Story:** As a tenant administrator, I want a web-based management portal, so that I can manage all platform entities through a visual interface.

#### Acceptance Criteria

1. THE Admin_Portal SHALL provide CRUD management screens for: Tenants, Applications, Identity Providers, Functional Areas, Functional Area Access Requirements, Permission Types, Permissions, Users, Groups, Group Memberships, Group Access Details, User Access Details, User Permission Assignments, Group Permission Assignments, Permission Templates, Template Application History, Audit Logs, and API Call Logs
2. THE Admin_Portal SHALL provide a template builder interface that allows selecting an application, creating a template, adding direct permissions, adding groups, adding access details, previewing template contents, applying the template to a user, duplicating an existing template, and deactivating a template
3. THE Admin_Portal SHALL provide an access detail lookup simulator that accepts a user, application, functional area, and optional permission type as inputs and displays: functional access decision, relevant access detail requirements, direct user access details, group-inherited access details, and the final set of data-level access details
4. THE Admin_Portal SHALL provide a permission simulation screen that accepts a user, application, functional area, permission type, optional permission code, and optional date/time as inputs and displays: allowed or denied decision, reason, direct assignments considered, group assignments considered, deny assignments considered, expired assignments ignored, inactive records ignored, template application history, and the final decision
5. THE Admin_Portal SHALL authenticate administrators using MSAL and the configured Identity Provider for the tenant

### Requirement 25: Infrastructure and Deployment

**User Story:** As a DevOps engineer, I want infrastructure defined as code with environment separation, so that deployments are repeatable and consistent.

#### Acceptance Criteria

1. THE Platform SHALL define all Azure infrastructure using Bicep templates covering the following resources: Resource Group, Azure SQL Server, Azure SQL Database, Key Vault, App Configuration, Application Insights, Log Analytics Workspace, Azure API Management, App Service or Container Apps, Azure Storage Account, Managed Identities, and Private Endpoints where required by environment
2. THE Platform SHALL support three deployment environments (development, staging, and production) where development uses lowest-cost SKUs, staging uses low-cost but production-like configuration, and production uses higher-reliability settings with automated backup retention, monitoring, autoscaling, and stricter network controls
3. THE Platform SHALL use GitHub Actions for CI/CD with stages executed in the following order: build, lint, test, security scan, database migration, and deploy, where each environment is triggered by pushes to its mapped branch (dev branch to development, staging branch to staging, main branch to production)
4. IF any CI/CD pipeline stage fails, THEN THE Platform SHALL halt the pipeline, prevent subsequent stages from executing, and report the failure with the failed stage name and error summary
5. WHEN deploying to the production environment, THE Platform SHALL require manual approval before proceeding with the deploy stage
6. THE Platform SHALL use Azure App Configuration for environment-specific application settings with each environment reading from its own isolated App Configuration instance or label set

### Requirement 26: Local Development Environment

**User Story:** As a developer, I want a local development setup using containers, so that I can develop and test without deploying to Azure.

#### Acceptance Criteria

1. THE Platform SHALL provide a Docker Compose configuration that starts all local dependencies — including SQL Server and Azurite for Azure Storage emulation — with a single command
2. THE Platform SHALL include seed data scripts that populate one Tenant, one Application, one Identity Provider configuration, three Functional Areas, three Functional Area Access Requirements, five Permission Types, eight Permissions, two Groups, three Users, two Permission Templates, user and group permission assignments, user access details, group access details, template access details, sample template application history, sample audit logs, and sample API call logs
3. THE Platform SHALL support Entity Framework Core migrations for database schema management, executable via a documented CLI command or automatically applied on local environment startup
4. THE Platform SHALL provide local development appsettings configured to use the containerized SQL Server and Azurite instances
5. THE Platform SHALL expose Swagger UI for API exploration when running in the local development environment

### Requirement 27: Testing

**User Story:** As a developer, I want comprehensive automated tests, so that I can verify correctness of critical security logic.

#### Acceptance Criteria

1. THE Platform SHALL include unit tests for permission resolution logic covering each of the following scenarios: deny overrides allow when conflicting assignments exist, expired permission (outside ValidFrom/ValidTo) is excluded from evaluation, inactive User_Profile is excluded, inactive Application is excluded, inactive Functional_Area is excluded, inactive Permission_Type is excluded, inactive Permission is excluded, inactive Group is excluded, group permission inheritance resolves to the user, and direct user permission assignment resolves correctly
2. THE Platform SHALL include unit tests for permission template materialization covering each of the following scenarios: template applies direct permission assignments to target user, template applies group memberships to target user, template applies access detail records to target user, template replace option removes existing assignments before materializing new ones, and template preview returns projected assignments without persisting changes
3. THE Platform SHALL include unit tests for access detail resolution covering each of the following scenarios: user access details filtered by Functional_Area_Access_Requirement declarations, group access detail inheritance combines with direct user access details, expired access detail (outside ValidFrom/ValidTo) is excluded, and inactive access detail is excluded from results
4. THE Platform SHALL include unit tests for cache invalidation covering each of the following scenarios: permission assignment change invalidates affected user cache, group permission assignment change invalidates cached permissions for all group members, group membership change invalidates cached permissions and access details for the affected user, and access detail change invalidates affected user cache
5. THE Platform SHALL include unit tests for CustomJwtIssuer token validation covering each of the following scenarios: unsigned token is rejected, token using a weak signing algorithm is rejected, token with invalid issuer is rejected, token with invalid audience is rejected, token with expired lifetime is rejected, and valid token with correct issuer, audience, lifetime, and signing key is accepted
6. THE Platform SHALL include integration tests using test containers for all CRUD API operations across the following entities: Tenant, Application, Identity Provider Configuration, User_Profile, Functional_Area, Permission_Type, Permission, Group, Group Membership, Permission Assignment, User_Access_Detail, Group_Access_Detail, Functional_Area_Access_Requirement, and Permission_Template
7. THE Platform SHALL include integration tests for access check endpoints covering single permission check by FunctionalAreaCode and PermissionTypeCode, single permission check by PermissionCode, batch permission check, and effective permissions retrieval
8. THE Platform SHALL include security tests verifying each of the following scenarios: cross-tenant read access is denied, cross-tenant write access is denied, invalid JWT is rejected, token with wrong audience is rejected, token with wrong issuer is rejected, expired token is rejected, unsigned token is rejected, token using a weak algorithm is rejected, request without required administrative permission scope is rejected, audit log records cannot be modified or deleted via API, and sensitive values (secrets, keys, tokens) are not present in log output

### Requirement 28: Configurable Product Name

**User Story:** As a product owner, I want the product name configurable, so that the platform can be rebranded without code changes.

#### Acceptance Criteria

1. THE Platform SHALL read the product display name from Azure App Configuration or application settings using a configuration key (e.g., "Platform:DisplayName"), defaulting to "Heimdall Access" if the value is not set
2. THE Admin_Portal SHALL display the configured product name in the page title, navigation header, and login screen
3. WHEN the product name configuration value is changed in Azure App Configuration, THE Platform SHALL reflect the new name without requiring redeployment, picking up the change within the configured refresh interval
4. IF the configured product name is empty or exceeds 100 characters, THEN THE Platform SHALL fall back to the default value of "Heimdall Access"

### Requirement 29: Database Schema Design

**User Story:** As a data architect, I want proper indexing and constraints on all tables, so that query performance and data integrity are maintained.

#### Acceptance Criteria

1. THE Platform SHALL create composite indexes on TenantId combined with each of the following columns on their respective tenant-scoped tables: ApplicationId, UserProfileId, ExternalSubjectId, IdentityProvider, FunctionalAreaCode, FunctionalAreaId, PermissionTypeId, PermissionType.Code, PermissionCode, GroupId, PermissionTemplateId, AccessDetailType, AccessDetailCode, CorrelationId, ApiCallLogId, CreatedAt, SourceIp, and EntityType combined with EntityId
2. THE Platform SHALL enforce uniqueness constraints as specified: Tenant.Slug globally; Application.ClientIdentifier within Tenant scope; FunctionalArea.FunctionalAreaCode, PermissionType.Code, Permission.PermissionCode, PermissionTemplate.TemplateCode, and Group.Name each within their Application scope; UserProfile.ExternalSubjectId combined with IdentityProvider within Tenant scope; IdentityProviderConfiguration unique by TenantId combined with ApplicationId combined with ProviderType combined with Name; FunctionalAreaAccessRequirement unique by FunctionalAreaId combined with AccessDetailType; UserAccessDetail unique by UserProfileId combined with ApplicationId combined with AccessDetailType combined with AccessDetailCode; GroupAccessDetail unique by GroupId combined with ApplicationId combined with AccessDetailType combined with AccessDetailCode
3. THE Platform SHALL use soft deletes (Status or IsActive flags) rather than physical deletion for all entities that carry a Status or IsActive column, including Tenant, Application, IdentityProviderConfiguration, FunctionalArea, PermissionType, Permission, UserProfile, Group, PermissionTemplate, UserAccessDetail, and GroupAccessDetail
4. THE Platform SHALL implement EF Core global query filters to automatically exclude inactive records from all read queries except those explicitly executed with filter-ignoring scope for administrative or audit purposes
5. IF a write operation would violate a uniqueness constraint, THEN THE Platform SHALL reject the operation and return a standardized error response indicating the duplicate field and conflicting value

### Requirement 30: Observability

**User Story:** As a platform operator, I want centralized monitoring and logging, so that I can detect and diagnose issues in production.

#### Acceptance Criteria

1. THE Platform SHALL send structured logs to Azure Application Insights where each log entry includes at minimum: timestamp, severity level, CorrelationId, source component name, and message
2. THE Platform SHALL track request duration, dependency call duration, unhandled exceptions, and custom metrics (permission checks per second, cache hit rate) as telemetry in Azure Application Insights
3. THE Platform SHALL send infrastructure and diagnostic logs from all Azure resources to Azure Log Analytics Workspace
4. THE Platform SHALL expose a health check endpoint that verifies database connectivity, Redis connectivity (if enabled), and Key Vault accessibility, and returns a response within 5 seconds
5. IF any health check dependency (database, Redis, or Key Vault) is unreachable, THEN THE Platform SHALL return a degraded or unhealthy status indicating which dependency failed
6. WHILE deployed in the production environment, THE Platform SHALL maintain at least 99.9% API availability measured as successful responses (HTTP status below 500) returned within 10 seconds, calculated over each calendar month
7. IF the error rate exceeds 5% of requests within a 5-minute window, or P95 response latency exceeds 3 seconds, or availability drops below 99.9% for the current month, THEN THE Platform SHALL trigger an alert via Azure Monitor
