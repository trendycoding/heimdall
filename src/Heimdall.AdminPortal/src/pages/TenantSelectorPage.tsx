import { useAuth } from '../auth/AuthProvider';
import { useTenant } from '../auth/TenantProvider';
import type { MyTenant } from '../types';

function roleBadgeColor(role: string): string {
  switch (role) {
    case 'Owner': return 'bg-purple-100 text-purple-700';
    case 'Admin': return 'bg-blue-100 text-blue-700';
    case 'Member': return 'bg-green-100 text-green-700';
    case 'ReadOnly': return 'bg-gray-100 text-gray-700';
    default: return 'bg-gray-100 text-gray-700';
  }
}

export function TenantSelectorPage() {
  const { account, logout } = useAuth();
  const { state, selectTenant } = useTenant();

  if (state.status !== 'select-tenant') return null;

  const { tenants } = state;

  return (
    <div className="flex min-h-screen items-center justify-center bg-heimdall-light px-4">
      <div className="w-full max-w-md">
        <div className="rounded-lg bg-white p-8 shadow-lg">
          <div className="mb-6 text-center">
            <h1 className="text-2xl font-bold text-heimdall-dark">Select organization</h1>
            <p className="mt-2 text-gray-600">
              Choose the organization you want to manage.
            </p>
          </div>

          <div className="space-y-2">
            {tenants.map((tenant: MyTenant) => (
              <button
                key={tenant.tenantId}
                type="button"
                onClick={() => selectTenant(tenant.tenantId)}
                className="flex w-full items-center justify-between rounded-md border border-gray-200 px-4 py-3 text-left transition-colors hover:border-heimdall-primary hover:bg-heimdall-primary/5"
              >
                <div>
                  <span className="font-medium text-heimdall-dark">{tenant.tenantName}</span>
                  <p className="text-xs text-gray-500">{tenant.slug}</p>
                </div>
                <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${roleBadgeColor(tenant.role)}`}>
                  {tenant.role}
                </span>
              </button>
            ))}
          </div>

          <div className="mt-6 border-t pt-4">
            <a
              href="/onboarding"
              className="block w-full rounded-md border border-heimdall-primary px-4 py-2 text-center text-sm text-heimdall-primary hover:bg-heimdall-primary/5 transition-colors"
            >
              Create a new organization
            </a>
          </div>

          <div className="mt-4 text-center">
            <p className="text-sm text-gray-500">
              Signed in as <span className="font-medium">{account?.name ?? account?.username}</span>
            </p>
            <button
              type="button"
              onClick={logout}
              className="mt-1 text-sm text-heimdall-primary hover:underline"
            >
              Sign out
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
