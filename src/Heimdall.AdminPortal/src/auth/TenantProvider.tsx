import React, { createContext, useContext, useState, useCallback, useMemo } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import apiClient from '../services/apiClient';
import type { ApiEnvelope, MyTenant } from '../types';
import { useAuth } from './AuthProvider';

export type TenantState =
  | { status: 'loading' }
  | { status: 'no-tenants' }
  | { status: 'select-tenant'; tenants: MyTenant[] }
  | { status: 'active'; tenant: MyTenant; tenants: MyTenant[] };

interface TenantContextValue {
  state: TenantState;
  selectTenant: (tenantId: string) => void;
  clearTenant: () => void;
  refetchTenants: () => void;
}

const TenantContext = createContext<TenantContextValue | null>(null);

const TENANT_STORAGE_KEY = 'heimdall_active_tenant_id';

/**
 * Provides tenant context to the application.
 * After login, fetches the user's tenants and determines the initial state:
 * - loading: waiting for API response
 * - no-tenants: user has no memberships (show onboarding)
 * - select-tenant: user has multiple tenants (show selector)
 * - active: tenant is selected and the portal is ready
 */
export function TenantProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const [activeTenantId, setActiveTenantId] = useState<string | null>(
    () => sessionStorage.getItem(TENANT_STORAGE_KEY)
  );

  const { data: tenants, isLoading, refetch } = useQuery({
    queryKey: ['my-tenants'],
    queryFn: async () => {
      const { data } = await apiClient.get<ApiEnvelope<MyTenant[]>>('/me/tenants');
      return data.data ?? [];
    },
    enabled: isAuthenticated,
    staleTime: 60_000,
  });

  const selectTenant = useCallback((tenantId: string) => {
    setActiveTenantId(tenantId);
    sessionStorage.setItem(TENANT_STORAGE_KEY, tenantId);
    // Invalidate all tenant-scoped queries when switching
    queryClient.invalidateQueries();
  }, [queryClient]);

  const clearTenant = useCallback(() => {
    setActiveTenantId(null);
    sessionStorage.removeItem(TENANT_STORAGE_KEY);
  }, []);

  const refetchTenants = useCallback(() => {
    refetch();
  }, [refetch]);

  const state = useMemo((): TenantState => {
    if (!isAuthenticated || isLoading) return { status: 'loading' };
    if (!tenants || tenants.length === 0) return { status: 'no-tenants' };

    // Auto-select if only one tenant
    const effectiveTenantId = activeTenantId ?? (tenants.length === 1 ? tenants[0].tenantId : null);

    if (effectiveTenantId) {
      const active = tenants.find(t => t.tenantId === effectiveTenantId);
      if (active) return { status: 'active', tenant: active, tenants };
    }

    return { status: 'select-tenant', tenants };
  }, [isAuthenticated, isLoading, tenants, activeTenantId]);

  // Auto-select single tenant
  React.useEffect(() => {
    if (state.status === 'active' && !activeTenantId && tenants?.length === 1) {
      setActiveTenantId(tenants[0].tenantId);
      sessionStorage.setItem(TENANT_STORAGE_KEY, tenants[0].tenantId);
    }
  }, [state.status, activeTenantId, tenants]);

  const value = useMemo(
    () => ({ state, selectTenant, clearTenant, refetchTenants }),
    [state, selectTenant, clearTenant, refetchTenants]
  );

  return <TenantContext.Provider value={value}>{children}</TenantContext.Provider>;
}

export function useTenant(): TenantContextValue {
  const context = useContext(TenantContext);
  if (!context) {
    throw new Error('useTenant must be used within a TenantProvider');
  }
  return context;
}
