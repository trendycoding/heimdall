import { useState, useEffect } from 'react';
import { Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthProvider';
import { useTenant } from '../auth/TenantProvider';
import { useProductName } from '../hooks/useProductName';
import { Sidebar } from './Sidebar';
import { OnboardingPage } from '../pages/OnboardingPage';
import { TenantSelectorPage } from '../pages/TenantSelectorPage';

/**
 * App shell with header, sidebar navigation, and main content area.
 * Gates access based on authentication and tenant state:
 * - Not authenticated → login screen
 * - No tenants → onboarding wizard
 * - Multiple tenants, none selected → tenant selector
 * - Tenant active → full dashboard with sidebar
 */
export function Layout() {
  const { isAuthenticated, login, logout, account } = useAuth();
  const { state: tenantState, clearTenant } = useTenant();
  const { productName } = useProductName();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  // Update document title when product name changes
  useEffect(() => {
    document.title = `${productName} - Admin Portal`;
  }, [productName]);

  // Not authenticated → login screen
  if (!isAuthenticated) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-heimdall-light px-4">
        <div className="w-full max-w-md rounded-lg bg-white p-8 shadow-lg">
          <h1 className="mb-2 text-center text-2xl font-bold text-heimdall-dark">
            {productName}
          </h1>
          <p className="mb-6 text-center text-gray-600">Admin Portal</p>
          <button
            type="button"
            onClick={login}
            className="w-full rounded-md bg-heimdall-primary px-4 py-2 text-white hover:bg-heimdall-secondary transition-colors"
          >
            Sign in with Azure AD
          </button>
        </div>
      </div>
    );
  }

  // Loading tenant state
  if (tenantState.status === 'loading') {
    return (
      <div className="flex min-h-screen items-center justify-center bg-heimdall-light">
        <div className="text-center">
          <div className="mx-auto h-8 w-8 animate-spin rounded-full border-4 border-heimdall-primary border-t-transparent" />
          <p className="mt-3 text-sm text-gray-600">Loading your organizations...</p>
        </div>
      </div>
    );
  }

  // No tenants → onboarding
  if (tenantState.status === 'no-tenants') {
    return <OnboardingPage />;
  }

  // Multiple tenants, none selected → selector
  if (tenantState.status === 'select-tenant') {
    return <TenantSelectorPage />;
  }

  // Active tenant → full portal
  const { tenant, tenants } = tenantState;

  return (
    <div className="flex h-screen flex-col overflow-hidden">
      {/* Header */}
      <header className="flex-shrink-0 border-b bg-heimdall-dark px-4 py-3 text-white sm:px-6">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            {/* Mobile hamburger */}
            <button
              type="button"
              onClick={() => setSidebarOpen(!sidebarOpen)}
              className="rounded-md p-1 text-gray-300 hover:bg-gray-700 transition-colors lg:hidden"
              aria-label="Toggle navigation"
            >
              <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                {sidebarOpen ? (
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                ) : (
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
                )}
              </svg>
            </button>
            <h1 className="text-lg font-semibold">
              {productName}
              <span className="ml-2 text-xs font-normal text-gray-400">({__APP_VERSION__})</span>
            </h1>
          </div>

          <div className="flex items-center gap-4">
            {/* Tenant switcher */}
            {tenants.length > 1 ? (
              <button
                type="button"
                onClick={clearTenant}
                className="hidden items-center gap-1.5 rounded-md border border-gray-600 px-2.5 py-1 text-sm text-gray-200 hover:bg-gray-700 transition-colors sm:flex"
                title="Switch organization"
              >
                <span className="text-xs">🏢</span>
                <span className="max-w-[140px] truncate">{tenant.tenantName}</span>
                <svg className="h-3.5 w-3.5 text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 9l4-4 4 4m0 6l-4 4-4-4" />
                </svg>
              </button>
            ) : (
              <span className="hidden text-sm text-gray-300 sm:inline">{tenant.tenantName}</span>
            )}

            {/* User info */}
            <div className="hidden flex-col items-end sm:flex">
              <span className="text-sm text-gray-200">
                {account?.name ?? account?.username}
              </span>
              <span className="text-xs text-gray-400">{tenant.role}</span>
            </div>

            <button
              type="button"
              onClick={logout}
              className="rounded-md border border-gray-500 px-3 py-1 text-sm text-gray-300 hover:bg-gray-700 transition-colors"
            >
              Sign out
            </button>
          </div>
        </div>
      </header>

      {/* Body: sidebar + content */}
      <div className="flex flex-1 overflow-hidden">
        {/* Mobile sidebar overlay */}
        {sidebarOpen && (
          <div
            className="fixed inset-0 z-30 bg-black/50 lg:hidden"
            onClick={() => setSidebarOpen(false)}
          />
        )}

        {/* Sidebar */}
        <div
          className={`fixed inset-y-0 left-0 z-40 w-64 transform transition-transform duration-200 ease-in-out lg:relative lg:translate-x-0 lg:z-auto ${
            sidebarOpen ? 'translate-x-0' : '-translate-x-full'
          }`}
        >
          <div className="h-full pt-14 lg:pt-0">
            <Sidebar onNavigate={() => setSidebarOpen(false)} tenantRole={tenant.role} />
          </div>
        </div>

        <main className="flex-1 overflow-y-auto bg-heimdall-light p-4 sm:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
