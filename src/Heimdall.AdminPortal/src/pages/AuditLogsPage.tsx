import { useState } from 'react';
import { DataTable, Column, Modal, Pagination } from '../components';
import { useListQuery } from '../hooks/useApi';
import type { AuditLog } from '../types';

export function AuditLogsPage() {
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState({
    search: '',
    entityType: '',
    action: '',
    dateFrom: '',
    dateTo: '',
  });
  const [selectedLog, setSelectedLog] = useState<AuditLog | null>(null);

  const { data, isLoading } = useListQuery<AuditLog>(
    ['audit-logs'],
    '/audit-logs',
    {
      page,
      pageSize: 50,
      search: filters.search || undefined,
      entityType: filters.entityType || undefined,
      action: filters.action || undefined,
      dateFrom: filters.dateFrom || undefined,
      dateTo: filters.dateTo || undefined,
    }
  );

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  const columns: Column<AuditLog>[] = [
    { key: 'createdAt', header: 'Timestamp', render: (row) => new Date(row.createdAt).toLocaleString() },
    { key: 'entityType', header: 'Entity Type' },
    { key: 'entityId', header: 'Entity ID', render: (row) => <span className="font-mono text-xs">{row.entityId.slice(0, 8)}...</span> },
    { key: 'action', header: 'Action' },
    { key: 'actorEmail', header: 'Actor' },
    { key: 'correlationId', header: 'Correlation ID', render: (row) => <span className="font-mono text-xs">{row.correlationId.slice(0, 8)}...</span> },
    {
      key: 'actions',
      header: '',
      sortable: false,
      render: (row) => (
        <button type="button" className="text-sm text-heimdall-primary hover:underline" onClick={() => setSelectedLog(row)}>Details</button>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Audit Logs</h2>
        <p className="text-sm text-gray-500">Read-only view of system audit trail</p>
      </div>

      {/* Filters */}
      <div className="grid grid-cols-1 md:grid-cols-5 gap-3 rounded-lg border bg-white p-4">
        <input
          type="text"
          placeholder="Search..."
          value={filters.search}
          onChange={(e) => { setFilters((f) => ({ ...f, search: e.target.value })); setPage(1); }}
          className="rounded-md border px-3 py-2 text-sm focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary"
        />
        <select
          value={filters.entityType}
          onChange={(e) => { setFilters((f) => ({ ...f, entityType: e.target.value })); setPage(1); }}
          aria-label="Filter by entity type"
          className="rounded-md border px-3 py-2 text-sm focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary"
        >
          <option value="">All Entity Types</option>
          <option value="Tenant">Tenant</option>
          <option value="Application">Application</option>
          <option value="IdentityProvider">Identity Provider</option>
          <option value="UserProfile">User Profile</option>
          <option value="FunctionalArea">Functional Area</option>
          <option value="PermissionType">Permission Type</option>
          <option value="Permission">Permission</option>
          <option value="Group">Group</option>
          <option value="GroupMembership">Group Membership</option>
          <option value="PermissionAssignment">Permission Assignment</option>
          <option value="AccessDetail">Access Detail</option>
          <option value="PermissionTemplate">Permission Template</option>
        </select>
        <select
          value={filters.action}
          onChange={(e) => { setFilters((f) => ({ ...f, action: e.target.value })); setPage(1); }}
          aria-label="Filter by action"
          className="rounded-md border px-3 py-2 text-sm focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary"
        >
          <option value="">All Actions</option>
          <option value="Create">Create</option>
          <option value="Update">Update</option>
          <option value="Delete">Delete</option>
          <option value="Deactivate">Deactivate</option>
        </select>
        <input
          type="date"
          value={filters.dateFrom}
          onChange={(e) => { setFilters((f) => ({ ...f, dateFrom: e.target.value })); setPage(1); }}
          aria-label="Filter from date"
          className="rounded-md border px-3 py-2 text-sm focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary"
        />
        <input
          type="date"
          value={filters.dateTo}
          onChange={(e) => { setFilters((f) => ({ ...f, dateTo: e.target.value })); setPage(1); }}
          aria-label="Filter to date"
          className="rounded-md border px-3 py-2 text-sm focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary"
        />
      </div>

      <DataTable columns={columns} data={items} keyField="id" isLoading={isLoading} emptyMessage="No audit logs found." />
      <Pagination currentPage={page} totalPages={totalPages} onPageChange={setPage} />

      {/* Detail Modal with JSON expansion */}
      {selectedLog && (
        <AuditLogDetailModal log={selectedLog} onClose={() => setSelectedLog(null)} />
      )}
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Audit Log Detail Modal                                                      */
/* -------------------------------------------------------------------------- */

function AuditLogDetailModal({ log, onClose }: { log: AuditLog; onClose: () => void }) {
  const [expandedSection, setExpandedSection] = useState<'before' | 'after' | 'changed' | null>(null);

  return (
    <Modal isOpen onClose={onClose} title="Audit Log Details" size="xl">
      <div className="space-y-4">
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <span className="font-medium text-gray-500">Entity Type</span>
            <p className="mt-0.5">{log.entityType}</p>
          </div>
          <div>
            <span className="font-medium text-gray-500">Entity ID</span>
            <p className="mt-0.5 font-mono text-xs break-all">{log.entityId}</p>
          </div>
          <div>
            <span className="font-medium text-gray-500">Action</span>
            <p className="mt-0.5">{log.action}</p>
          </div>
          <div>
            <span className="font-medium text-gray-500">Actor</span>
            <p className="mt-0.5">{log.actorEmail}</p>
          </div>
          <div>
            <span className="font-medium text-gray-500">Correlation ID</span>
            <p className="mt-0.5 font-mono text-xs break-all">{log.correlationId}</p>
          </div>
          <div>
            <span className="font-medium text-gray-500">Timestamp</span>
            <p className="mt-0.5">{new Date(log.createdAt).toLocaleString()}</p>
          </div>
        </div>

        {/* Expandable JSON sections */}
        <div className="space-y-2">
          <JsonSection title="Before State" json={log.beforeJson} isExpanded={expandedSection === 'before'} onToggle={() => setExpandedSection(expandedSection === 'before' ? null : 'before')} />
          <JsonSection title="After State" json={log.afterJson} isExpanded={expandedSection === 'after'} onToggle={() => setExpandedSection(expandedSection === 'after' ? null : 'after')} />
          <JsonSection title="Changed Fields" json={log.changedFieldsJson} isExpanded={expandedSection === 'changed'} onToggle={() => setExpandedSection(expandedSection === 'changed' ? null : 'changed')} />
        </div>
      </div>
    </Modal>
  );
}

function JsonSection({ title, json, isExpanded, onToggle }: { title: string; json: string | null; isExpanded: boolean; onToggle: () => void }) {
  if (!json) {
    return (
      <div className="rounded border p-3">
        <span className="text-sm font-medium text-gray-500">{title}</span>
        <span className="ml-2 text-xs text-gray-400">N/A</span>
      </div>
    );
  }

  let formatted: string;
  try {
    formatted = JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    formatted = json;
  }

  return (
    <div className="rounded border">
      <button type="button" onClick={onToggle} className="flex w-full items-center justify-between p-3 text-left hover:bg-gray-50 transition-colors">
        <span className="text-sm font-medium text-gray-700">{title}</span>
        <svg className={`h-4 w-4 text-gray-400 transition-transform ${isExpanded ? 'rotate-180' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>
      {isExpanded && (
        <div className="border-t p-3">
          <pre className="max-h-64 overflow-auto rounded bg-gray-50 p-3 text-xs font-mono text-gray-800">{formatted}</pre>
        </div>
      )}
    </div>
  );
}
