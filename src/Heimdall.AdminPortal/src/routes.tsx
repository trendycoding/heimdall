import { RouteObject } from 'react-router-dom';
import { Layout } from './components';
import {
  Dashboard,
  ApplicationsPage,
  IdentityProvidersPage,
  UsersPage,
  FunctionalAreasPage,
  PermissionTypesPage,
  PermissionsPage,
  GroupsPage,
  PermissionAssignmentsPage,
  AccessDetailsPage,
  PermissionTemplatesPage,
  TemplateBuilderPage,
  AuditLogsPage,
  ApiCallLogsPage,
} from './pages';

/**
 * Application route definitions.
 * The Layout component handles auth gating, tenant selection, and the app shell.
 * All child routes are tenant-scoped (require an active tenant).
 */
export const routes: RouteObject[] = [
  {
    path: '/',
    element: <Layout />,
    children: [
      { index: true, element: <Dashboard /> },
      { path: 'applications', element: <ApplicationsPage /> },
      { path: 'identity-providers', element: <IdentityProvidersPage /> },
      { path: 'users', element: <UsersPage /> },
      { path: 'functional-areas', element: <FunctionalAreasPage /> },
      { path: 'permission-types', element: <PermissionTypesPage /> },
      { path: 'permissions', element: <PermissionsPage /> },
      { path: 'groups', element: <GroupsPage /> },
      { path: 'permission-assignments', element: <PermissionAssignmentsPage /> },
      { path: 'access-details', element: <AccessDetailsPage /> },
      { path: 'permission-templates', element: <PermissionTemplatesPage /> },
      { path: 'template-builder', element: <TemplateBuilderPage /> },
      { path: 'audit-logs', element: <AuditLogsPage /> },
      { path: 'api-call-logs', element: <ApiCallLogsPage /> },
    ],
  },
];
