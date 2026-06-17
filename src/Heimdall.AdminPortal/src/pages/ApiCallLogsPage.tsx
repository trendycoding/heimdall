import { useState } from 'react';
import { DataTable, Column, Modal, Pagination } from '../components';
import { useListQuery } from '../hooks/useApi';
import type { ApiCallLog } from '../types';

export function ApiCallLogsPage() {
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState({
    search: '',
    httpMethod: '',
    dateFrom: '',
    dateTo: '',
  });
  const [selectedLog, setSelectedLog] = useState<ApiCallLog | null>(null);

  const { data, isLoading } = useListQuery<ApiCallLog>(
    ['api-call-logs'],
    '/api-call-logs',
    {
      page,
      pageSize: 50,
      search: filters.search || undefined,
      httpMethod: filters.httpMethod || undefined,
      dateFrom: filters.dateFrom || undefined,
      dateTo: filters.dateTo || undefined,
    }
  );

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  const columns: Column<ApiCallLog>[] = [
    { key: 'requestTimestamp', header: 'Timestamp', render: (row) => new Date(row.requestTimestamp).toLocaleString() },
    {
      key: 'httpMethod',
      header: 'Method',
      render: (row) => (
        <span className={`font-mono text-xs font-medium ${methodColor(row.httpMethod)}`}>
          {row.httpMethod}
        </span>
      ),
    },
    { key: 'endpoint', header: 'Endpoint' },
    {
      key: 'statusCode',
      header: 'Status',
      render: (row) => (
        <span className={`font-mono text-xs font-medium ${row.statusCode < 400 ? 'text-green-700' : 'text-red-700'}`}>
          {row.statusCode}
        </span>
      ),
    },
    { key: 'durationMs', header: 'Duration', render: (row) => `${row.durationMs}ms` },
    { key: 'sourceIp', header: 'Source IP' },
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
        <h2 className="text-xl font-semibold text-heimdall-dark">API Call Logs</h2>
        <p className="text-sm text-gray-500">Read-only view of API request history</p>
      </div>

      {/* Filters */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-3 rounded-lg border bg-white p-4">
        <input
          type="text"
          placeholder="Search endpoint, caller..."
          value={filters.search}
          onChange={(e) => { setFilters((f) => ({ ...f, search: e.target.value })); setPage(1); }}
          className="rounded-md border px-3 py-2 text-sm focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary"
        />
        <select
          value={filters.httpMethod}
          onChange={(e) => { setFilters((f) => ({ ...f, httpMethod: e.target.value })); setPage(1); }}
          aria-label="Filter by HTTP method"
          className="rounded-md border px-3 py-2 text-sm focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary"
        >
          <option value="">All Methods</option>
          <option value="GET">GET</option>
          <option value="POST">POST</option>
          <option value="PUT">PUT</option>
          <option value="DELETE">DELETE</option>
          <option value="PATCH">PATCH</option>
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

      <DataTable columns={columns} data={items} keyField="id" isLoading={isLoading} emptyMessage="No API call logs found." />
      <Pagination currentPage={page} totalPages={totalPages} onPageChange={setPage} />

      {/* Detail Modal */}
      {selectedLog && (
        <ApiCallLogDetailModal log={selectedLog} onClose={() => setSelectedLog(null)} />
      )}
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* API Call Log Detail Modal                                                    */
/* -------------------------------------------------------------------------- */

function ApiCallLogDetailModal({ log, onClose }: { log: ApiCallLog; onClose: () => void }) {
  return (
    <Modal isOpen onClose={onClose} title="API Call Log Details" size="xl">
      <div className="grid grid-cols-2 gap-4 text-sm">
        <div>
          <span className="font-medium text-gray-500">HTTP Method</span>
          <p className={`mt-0.5 font-mono font-medium ${methodColor(log.httpMethod)}`}>{log.httpMethod}</p>
        </div>
        <div>
          <span className="font-medium text-gray-500">Status Code</span>
          <p className={`mt-0.5 font-mono font-medium ${log.statusCode < 400 ? 'text-green-700' : 'text-red-700'}`}>{log.statusCode}</p>
        </div>
        <div className="col-span-2">
          <span className="font-medium text-gray-500">Endpoint</span>
          <p className="mt-0.5 font-mono text-xs">{log.endpoint}</p>
        </div>
        <div className="col-span-2">
          <span className="font-medium text-gray-500">Request Path</span>
          <p className="mt-0.5 font-mono text-xs">{log.requestPath}</p>
        </div>
        <div>
          <span className="font-medium text-gray-500">Duration</span>
          <p className="mt-0.5">{log.durationMs}ms</p>
        </div>
        <div>
          <span className="font-medium text-gray-500">Source IP</span>
          <p className="mt-0.5 font-mono text-xs">{log.sourceIp}</p>
        </div>
        <div>
          <span className="font-medium text-gray-500">Caller Subject ID</span>
          <p className="mt-0.5 font-mono text-xs">{log.callerSubjectId}</p>
        </div>
        <div>
          <span className="font-medium text-gray-500">Caller Client ID</span>
          <p className="mt-0.5 font-mono text-xs">{log.callerClientId ?? 'N/A'}</p>
        </div>
        <div>
          <span className="font-medium text-gray-500">User Agent</span>
          <p className="mt-0.5 text-xs truncate">{log.userAgent ?? 'N/A'}</p>
        </div>
        <div>
          <span className="font-medium text-gray-500">Correlation ID</span>
          <p className="mt-0.5 font-mono text-xs break-all">{log.correlationId}</p>
        </div>
        <div>
          <span className="font-medium text-gray-500">Request Timestamp</span>
          <p className="mt-0.5">{new Date(log.requestTimestamp).toLocaleString()}</p>
        </div>
        <div>
          <span className="font-medium text-gray-500">Response Timestamp</span>
          <p className="mt-0.5">{log.responseTimestamp ? new Date(log.responseTimestamp).toLocaleString() : 'N/A'}</p>
        </div>
      </div>
    </Modal>
  );
}

/* -------------------------------------------------------------------------- */
/* Helpers                                                                     */
/* -------------------------------------------------------------------------- */

function methodColor(method: string): string {
  switch (method) {
    case 'GET': return 'text-green-700';
    case 'POST': return 'text-blue-700';
    case 'PUT': return 'text-yellow-700';
    case 'DELETE': return 'text-red-700';
    case 'PATCH': return 'text-purple-700';
    default: return 'text-gray-700';
  }
}
