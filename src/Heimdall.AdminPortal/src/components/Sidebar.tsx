import { NavLink } from 'react-router-dom';
import type { TenantRole } from '../types';

interface NavItem {
  label: string;
  path: string;
  icon: string;
  /** Minimum role required to see this item. Defaults to all roles. */
  minRole?: TenantRole[];
}

const navSections: { title: string; items: NavItem[] }[] = [
  {
    title: 'Core',
    items: [
      { label: 'Applications', path: '/applications', icon: '📱' },
      { label: 'Identity Providers', path: '/identity-providers', icon: '🔑' },
      { label: 'Users', path: '/users', icon: '👤' },
    ],
  },
  {
    title: 'Permissions',
    items: [
      { label: 'Functional Areas', path: '/functional-areas', icon: '📂' },
      { label: 'Permission Types', path: '/permission-types', icon: '🏷️' },
      { label: 'Permissions', path: '/permissions', icon: '🔒' },
      { label: 'Groups', path: '/groups', icon: '👥' },
    ],
  },
  {
    title: 'Assignments',
    items: [
      { label: 'Permission Assignments', path: '/permission-assignments', icon: '✅' },
      { label: 'Access Details', path: '/access-details', icon: '📋' },
      { label: 'Permission Templates', path: '/permission-templates', icon: '📄' },
      { label: 'Template Builder', path: '/template-builder', icon: '🧩' },
    ],
  },
  {
    title: 'Simulators',
    items: [
      { label: 'Access Detail Lookup', path: '/access-detail-simulator', icon: '🔍' },
      { label: 'Permission Simulation', path: '/permission-simulation', icon: '🧪' },
    ],
  },
  {
    title: 'Audit & Logs',
    items: [
      { label: 'Audit Logs', path: '/audit-logs', icon: '📜' },
      { label: 'API Call Logs', path: '/api-call-logs', icon: '📡' },
    ],
  },
  {
    title: 'Settings',
    items: [
      { label: 'Organization', path: '/settings', icon: '⚙️', minRole: ['Owner', 'Admin'] },
    ],
  },
];

/** Role hierarchy: Owner > Admin > Member > ReadOnly */
const roleHierarchy: TenantRole[] = ['Owner', 'Admin', 'Member', 'ReadOnly'];

function hasAccess(userRole: TenantRole, minRoles?: TenantRole[]): boolean {
  if (!minRoles || minRoles.length === 0) return true;
  const userLevel = roleHierarchy.indexOf(userRole);
  return minRoles.some(r => userLevel <= roleHierarchy.indexOf(r));
}

interface SidebarProps {
  onNavigate?: () => void;
  tenantRole?: TenantRole;
}

export function Sidebar({ onNavigate, tenantRole = 'Member' }: SidebarProps) {
  return (
    <aside className="flex h-full w-64 flex-col overflow-y-auto border-r bg-white">
      <nav className="flex-1 px-3 py-4">
        {navSections.map((section) => {
          const visibleItems = section.items.filter(item => hasAccess(tenantRole, item.minRole));
          if (visibleItems.length === 0) return null;

          return (
            <div key={section.title} className="mb-4">
              <h3 className="mb-1 px-3 text-xs font-semibold uppercase tracking-wider text-gray-500">
                {section.title}
              </h3>
              <ul className="space-y-0.5">
                {visibleItems.map((item) => (
                  <li key={item.path}>
                    <NavLink
                      to={item.path}
                      onClick={onNavigate}
                      className={({ isActive }) =>
                        `flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors ${
                          isActive
                            ? 'bg-heimdall-primary/10 text-heimdall-primary font-medium'
                            : 'text-gray-700 hover:bg-gray-100'
                        }`
                      }
                    >
                      <span className="text-base">{item.icon}</span>
                      <span>{item.label}</span>
                    </NavLink>
                  </li>
                ))}
              </ul>
            </div>
          );
        })}
      </nav>
    </aside>
  );
}
