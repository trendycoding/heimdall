import { NavLink } from 'react-router-dom';

const quickLinks = [
  { label: 'Tenants', path: '/tenants', description: 'Manage multi-tenant organizations' },
  { label: 'Applications', path: '/applications', description: 'Configure registered applications' },
  { label: 'Users', path: '/users', description: 'View and manage user profiles' },
  { label: 'Permissions', path: '/permissions', description: 'Define and manage permissions' },
  { label: 'Groups', path: '/groups', description: 'Organize users into groups' },
  { label: 'Permission Templates', path: '/permission-templates', description: 'Standardized permission provisioning' },
];

export function Dashboard() {
  return (
    <div className="space-y-6">
      <div className="rounded-lg bg-white p-6 shadow-sm">
        <h2 className="text-xl font-semibold text-heimdall-dark">Dashboard</h2>
        <p className="mt-2 text-gray-600">
          Welcome to the Heimdall Access Admin Portal. Use the sidebar or quick links below to manage your platform.
        </p>
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
