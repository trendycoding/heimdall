/** Standard API envelope response */
export interface ApiEnvelope<T> {
  success: boolean;
  data: T;
  errors: string[];
  correlationId: string;
}

/** Paginated response wrapper */
export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

/* -------------------------------------------------------------------------- */
/* Entity Types                                                                */
/* -------------------------------------------------------------------------- */

export interface Tenant {
  id: string;
  name: string;
  slug: string;
  primaryIdentityMode: string;
  status: string;
  createdAt: string;
  modifiedAt: string | null;
}

export interface Application {
  id: string;
  tenantId: string;
  name: string;
  clientIdentifier: string;
  description: string;
  allowedRedirectUris: string[];
  allowedOrigins: string[];
  status: string;
  createdAt: string;
  modifiedAt: string | null;
}

export interface IdentityProvider {
  id: string;
  tenantId: string;
  applicationId: string | null;
  name: string;
  providerType: string;
  issuer: string;
  audience: string;
  jwksUri: string;
  allowedAlgorithms: string[];
  clockSkewSeconds: number;
  claimMappings: ClaimMapping[];
  status: string;
  createdAt: string;
  modifiedAt: string | null;
}

export interface ClaimMapping {
  sourceClaimType: string;
  targetField: string;
}

export interface UserProfile {
  id: string;
  tenantId: string;
  externalSubjectId: string;
  identityProvider: string;
  email: string;
  displayName: string;
  status: string;
  lastLoginAt: string | null;
  createdAt: string;
  modifiedAt: string | null;
}

export interface FunctionalArea {
  id: string;
  applicationId: string;
  functionalAreaCode: string;
  name: string;
  description: string;
  isActive: boolean;
  createdAt: string;
}

export interface FunctionalAreaAccessRequirement {
  id: string;
  functionalAreaId: string;
  functionalAreaCode: string;
  accessDetailType: string;
  isActive: boolean;
  createdAt: string;
}

export interface PermissionType {
  id: string;
  applicationId: string;
  code: string;
  name: string;
  description: string;
  isSystemReserved: boolean;
  isActive: boolean;
  createdAt: string;
}

export interface Permission {
  id: string;
  applicationId: string;
  permissionCode: string;
  name: string;
  description: string;
  functionalAreaId: string;
  functionalAreaCode: string;
  permissionTypeId: string;
  permissionTypeCode: string;
  isActive: boolean;
  createdAt: string;
}

export interface Group {
  id: string;
  applicationId: string;
  name: string;
  description: string;
  isActive: boolean;
  createdAt: string;
}

export interface GroupMembership {
  id: string;
  groupId: string;
  groupName: string;
  userProfileId: string;
  userDisplayName: string;
  userEmail: string;
  isActive: boolean;
  createdAt: string;
}

export interface UserPermissionAssignment {
  id: string;
  userProfileId: string;
  userDisplayName: string;
  permissionId: string;
  permissionCode: string;
  effect: string;
  validFrom: string | null;
  validTo: string | null;
  createdAt: string;
}

export interface GroupPermissionAssignment {
  id: string;
  groupId: string;
  groupName: string;
  permissionId: string;
  permissionCode: string;
  effect: string;
  validFrom: string | null;
  validTo: string | null;
  createdAt: string;
}

export interface UserAccessDetail {
  id: string;
  userProfileId: string;
  userDisplayName: string;
  applicationId: string;
  accessDetailType: string;
  accessDetailCode: string;
  accessDetailValue: string;
  isActive: boolean;
  validFrom: string | null;
  validTo: string | null;
  createdAt: string;
}

export interface GroupAccessDetail {
  id: string;
  groupId: string;
  groupName: string;
  applicationId: string;
  accessDetailType: string;
  accessDetailCode: string;
  accessDetailValue: string;
  isActive: boolean;
  validFrom: string | null;
  validTo: string | null;
  createdAt: string;
}

export interface PermissionTemplate {
  id: string;
  applicationId: string;
  templateCode: string;
  name: string;
  description: string;
  isActive: boolean;
  createdAt: string;
  permissions?: PermissionTemplatePermissionEntry[];
  groups?: PermissionTemplateGroupEntry[];
  accessDetails?: PermissionTemplateAccessDetailEntry[];
}

export interface PermissionTemplatePermissionEntry {
  id: string;
  permissionId: string;
  permissionCode: string;
  permissionName: string;
  effect: 'Allow' | 'Deny';
  validFromOffsetDays: number | null;
  validToOffsetDays: number | null;
}

export interface PermissionTemplateGroupEntry {
  id: string;
  groupId: string;
  groupName: string;
}

export interface PermissionTemplateAccessDetailEntry {
  id: string;
  accessDetailType: string;
  accessDetailCode: string;
  accessDetailValue: string;
  description: string;
  validFromOffsetDays: number | null;
  validToOffsetDays: number | null;
}

export interface TemplateApplicationHistory {
  id: string;
  permissionTemplateId: string;
  templateCode: string;
  templateName: string;
  userProfileId: string;
  userDisplayName: string;
  appliedAt: string;
  appliedBy: string;
}

export interface TemplateApplyOptions {
  userProfileId: string;
  replacePermissions: boolean;
  replaceGroups: boolean;
  replaceAccessDetails: boolean;
}

export interface TemplatePreviewResult {
  directPermissionsToAdd: { permissionCode: string; effect: string; validFrom: string | null; validTo: string | null }[];
  groupsToAdd: { groupName: string }[];
  accessDetailsToAdd: { accessDetailType: string; accessDetailCode: string; accessDetailValue: string; validFrom: string | null; validTo: string | null }[];
  existingPermissionsUnaffected: { permissionCode: string; effect: string }[];
  conflicts: { permissionCode: string; existingEffect: string; templateEffect: string }[];
}

export interface AuditLog {
  id: string;
  tenantId: string;
  applicationId: string | null;
  entityType: string;
  entityId: string;
  action: string;
  actorEmail: string;
  actorSubjectId: string;
  correlationId: string;
  apiCallLogId: string | null;
  beforeJson: string | null;
  afterJson: string | null;
  changedFieldsJson: string | null;
  createdAt: string;
}

export interface ApiCallLog {
  id: string;
  tenantId: string;
  applicationId: string | null;
  httpMethod: string;
  endpoint: string;
  requestPath: string;
  callerSubjectId: string;
  callerClientId: string | null;
  sourceIp: string;
  userAgent: string | null;
  statusCode: number;
  durationMs: number;
  correlationId: string;
  requestTimestamp: string;
  responseTimestamp: string | null;
}

/* -------------------------------------------------------------------------- */
/* Permission Simulation Types                                                 */
/* -------------------------------------------------------------------------- */

export interface PermissionCheckMatchedAssignment {
  assignmentId: string;
  source: string;
  effect: string;
  groupId: string | null;
  groupName: string | null;
}

export interface PermissionCheckResult {
  allowed: boolean;
  decision: string;
  reason: string;
  matchedAssignments: PermissionCheckMatchedAssignment[];
}
