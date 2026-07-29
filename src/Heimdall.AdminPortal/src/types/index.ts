/** Standard API envelope wrapping all responses */
export interface ApiEnvelope<T> {
  success: boolean;
  data: T | null;
  errors: ApiError[] | null;
  correlationId: string;
}

export interface ApiError {
  code: string;
  message: string;
  field?: string;
}

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/** Tenant as returned by the admin API */
export interface Tenant {
  id: string;
  name: string;
  slug: string;
  primaryIdentityMode: string;
  status: string;
  createdAt: string;
  createdBy: string;
  modifiedAt?: string;
  modifiedBy?: string;
}

/** Result from GET /api/me/tenants */
export interface MyTenant {
  tenantId: string;
  tenantName: string;
  slug: string;
  role: TenantRole;
  membershipStatus: string;
  tenantStatus: string;
  memberSince: string;
}

export type TenantRole = 'Owner' | 'Admin' | 'Member' | 'ReadOnly';

/** Request body for POST /api/me/register-tenant */
export interface RegisterTenantRequest {
  name: string;
  slug: string;
  primaryIdentityMode: string;
  displayName?: string;
}

/** Result from POST /api/me/register-tenant */
export interface RegisterTenantResult {
  tenantId: string;
  tenantName: string;
  slug: string;
  membershipId: string;
  role: TenantRole;
}
