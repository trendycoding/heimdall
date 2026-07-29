import { NavLink } from 'react-router-dom';
import { useTenant } from '../auth/TenantProvider';

const quickLinks = [
  { label: 'Applications', path: '/applications', description: 'Configure registered applications' },
  { label: 'Users', path: '/users', description: 'View and manage user profiles' },
  { label: 'Permissions', path: '/permissions', description: 'Define and manage permissions' },
  { label: 'Groups', path: '/groups', description: 'Organize users into groups' },
  { label: 'Permission Templates', path: '/permission-templates', description: 'Standardized permission provisioning' },
  { label: 'Identity Providers', path: '/identity-providers', description: 'Configure authentication sources' },
];

export function Dashboard() {
  const { state } = useTenant();

  const tenantName = state.status === 'active' ? state.tenant.tenantName : 'Your Organization';
  const role = state.status === 'active' ? state.tenant.role : '';

  return (
    <div className="space-y-6">
      <div className="rounded-lg bg-white p-6 shadow-sm">
        <h2 className="text-xl font-semibold text-heimdall-dark">{tenantName}</h2>
        <p className="mt-2 text-gray-600">
          Welcome to your admin portal. Use the sidebar or quick links below to manage your platform.
        </p>
        {role && (
          <p className="mt-1 text-sm text-gray-500">
            Your role: <span className="font-medium">{role}</span>
          </p>
        )}
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {quickLinks.map((link) => (
          <NavLink
            key={link.path}
            to={link.path}
            className="rounded-lg border bg-white p-4 shadow-sm hover:border-heimdall-primary hover:shadow-md transition-all"
          >
            <h3 className="font-medium text-heimdall-dark">{link.label}</h3>
            <p className="mt-1 text-sm text-gray-500">{link.description}</p>
          </NavLink>
        ))}
      </div>
    </div>
  );
}
