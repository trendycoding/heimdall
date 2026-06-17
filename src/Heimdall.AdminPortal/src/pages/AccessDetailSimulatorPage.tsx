import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { Form, FormField, FormInput, FormActions, StatusBadge } from '../components';
import apiClient from '../services/apiClient';
import type { ApiEnvelope } from '../types';

/* -------------------------------------------------------------------------- */
/* Types                                                                        */
/* -------------------------------------------------------------------------- */

interface AccessDetailLookupResponse {
  functionalAccessDecision: {
    allowed: boolean;
    decision: string;
    reason: string;
  };
  accessDetailRequirements: AccessRequirementEntry[];
  directUserAccessDetails: AccessDetailResultEntry[];
  groupInheritedAccessDetails: AccessDetailResultEntry[];
  combinedAccessDetails: AccessDetailResultEntry[];
}

interface AccessRequirementEntry {
  functionalAreaId: string;
  accessDetailType: string;
  isRequired: boolean;
  description: string | null;
}

interface AccessDetailResultEntry {
  accessDetailId: string;
  accessDetailType: string;
  accessDetailCode: string;
  accessDetailValue: string;
  description: string | null;
  source: string;
  groupId: string | null;
  groupName: string | null;
}

/* -------------------------------------------------------------------------- */
/* Schema                                                                       */
/* -------------------------------------------------------------------------- */

const lookupSchema = z.object({
  userProfileId: z.string().min(1, 'User is required'),
  applicationId: z.string().min(1, 'Application is required'),
  functionalAreaCode: z.string().min(1, 'Functional area is required'),
  permissionTypeCode: z.string().optional().default(''),
});

type LookupFormData = z.infer<typeof lookupSchema>;

/* -------------------------------------------------------------------------- */
/* Page Component                                                               */
/* -------------------------------------------------------------------------- */

export function AccessDetailSimulatorPage() {
  const [lookupParams, setLookupParams] = useState<LookupFormData | null>(null);

  const { register, handleSubmit, formState: { errors } } = useForm<LookupFormData>({
    resolver: zodResolver(lookupSchema),
  });

  const { data: result, isLoading, error, isFetching } = useQuery({
    queryKey: ['access-detail-simulator', lookupParams],
    queryFn: async () => {
      if (!lookupParams) return null;

      const params: Record<string, string> = {
        userProfileId: lookupParams.userProfileId,
        functionalAreaCode: lookupParams.functionalAreaCode,
      };
      if (lookupParams.permissionTypeCode) {
        params.permissionTypeCode = lookupParams.permissionTypeCode;
      }

      const { data } = await apiClient.get<ApiEnvelope<AccessDetailLookupResponse>>(
        `/access-checks/access-details`,
        { params }
      );
      return data.data;
    },
    enabled: !!lookupParams,
  });

  const onSubmit = (data: LookupFormData) => {
    setLookupParams({ ...data });
  };

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold text-heimdall-dark">Access Detail Lookup Simulator</h1>
        <p className="mt-1 text-sm text-gray-600">
          Simulate an access detail lookup to see what data-level access a user has for a given functional area.
        </p>
      </div>

      {/* Input Form */}
      <div className="rounded-lg border bg-white p-6 shadow-sm">
        <h2 className="mb-4 text-lg font-medium text-gray-900">Lookup Parameters</h2>
        <Form onSubmit={handleSubmit(onSubmit)}>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <FormField label="User Profile ID" htmlFor="sim-user" error={errors.userProfileId?.message} required>
              <FormInput
                id="sim-user"
                {...register('userProfileId')}
                error={!!errors.userProfileId}
                placeholder="Enter user profile ID"
              />
            </FormField>
            <FormField label="Application ID" htmlFor="sim-app" error={errors.applicationId?.message} required>
              <FormInput
                id="sim-app"
                {...register('applicationId')}
                error={!!errors.applicationId}
                placeholder="Enter application ID"
              />
            </FormField>
            <FormField label="Functional Area Code" htmlFor="sim-fa" error={errors.functionalAreaCode?.message} required>
              <FormInput
                id="sim-fa"
                {...register('functionalAreaCode')}
                error={!!errors.functionalAreaCode}
                placeholder="e.g. ORDERS_MANAGEMENT"
              />
            </FormField>
            <FormField label="Permission Type Code" htmlFor="sim-pt" error={errors.permissionTypeCode?.message}>
              <FormInput
                id="sim-pt"
                {...register('permissionTypeCode')}
                error={!!errors.permissionTypeCode}
                placeholder="Optional (e.g. READ)"
              />
            </FormField>
          </div>
          <FormActions>
            <button
              type="submit"
              disabled={isLoading || isFetching}
              className="rounded-md bg-heimdall-primary px-6 py-2 text-sm font-medium text-white hover:bg-heimdall-secondary transition-colors disabled:opacity-50"
            >
              {isFetching ? 'Looking up...' : 'Run Lookup'}
            </button>
          </FormActions>
        </Form>
      </div>

      {/* Error Display */}
      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4">
          <p className="text-sm text-red-700">
            {error instanceof Error ? error.message : 'An error occurred during the lookup.'}
          </p>
        </div>
      )}

      {/* Results */}
      {result && (
        <div className="space-y-6">
          {/* Functional Access Decision */}
          <div className="rounded-lg border bg-white p-6 shadow-sm">
            <h2 className="mb-3 text-lg font-medium text-gray-900">Functional Access Decision</h2>
            <div className="flex items-center gap-4">
              <StatusBadge status={result.functionalAccessDecision.allowed ? 'Active' : 'Inactive'} />
              <span className="text-sm font-medium">
                {result.functionalAccessDecision.decision}
              </span>
              <span className="text-sm text-gray-600">
                — {result.functionalAccessDecision.reason}
              </span>
            </div>
          </div>

          {/* Access Detail Requirements */}
          <div className="rounded-lg border bg-white p-6 shadow-sm">
            <h2 className="mb-3 text-lg font-medium text-gray-900">
              Relevant Access Detail Requirements
            </h2>
            {result.accessDetailRequirements.length === 0 ? (
              <p className="text-sm text-gray-500 italic">
                No access detail requirements defined for this functional area.
              </p>
            ) : (
              <div className="overflow-x-auto">
                <table className="min-w-full text-sm">
                  <thead>
                    <tr className="border-b bg-gray-50">
                      <th className="px-4 py-2 text-left font-medium text-gray-700">Detail Type</th>
                      <th className="px-4 py-2 text-left font-medium text-gray-700">Required</th>
                      <th className="px-4 py-2 text-left font-medium text-gray-700">Description</th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.accessDetailRequirements.map((req, i) => (
                      <tr key={i} className="border-b last:border-0">
                        <td className="px-4 py-2 font-mono text-xs">{req.accessDetailType}</td>
                        <td className="px-4 py-2">
                          <StatusBadge status={req.isRequired ? 'Active' : 'Inactive'} />
                        </td>
                        <td className="px-4 py-2 text-gray-600">{req.description ?? '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Direct User Access Details */}
          <div className="rounded-lg border bg-white p-6 shadow-sm">
            <h2 className="mb-3 text-lg font-medium text-gray-900">
              Direct User Access Details
              <span className="ml-2 text-sm font-normal text-gray-500">
                ({result.directUserAccessDetails.length} records)
              </span>
            </h2>
            {result.directUserAccessDetails.length === 0 ? (
              <p className="text-sm text-gray-500 italic">No direct user access details found.</p>
            ) : (
              <AccessDetailTable entries={result.directUserAccessDetails} />
            )}
          </div>

          {/* Group-Inherited Access Details */}
          <div className="rounded-lg border bg-white p-6 shadow-sm">
            <h2 className="mb-3 text-lg font-medium text-gray-900">
              Group-Inherited Access Details
              <span className="ml-2 text-sm font-normal text-gray-500">
                ({result.groupInheritedAccessDetails.length} records)
              </span>
            </h2>
            {result.groupInheritedAccessDetails.length === 0 ? (
              <p className="text-sm text-gray-500 italic">No group-inherited access details found.</p>
            ) : (
              <AccessDetailTable entries={result.groupInheritedAccessDetails} showGroup />
            )}
          </div>

          {/* Final Combined Set */}
          <div className="rounded-lg border border-heimdall-primary/30 bg-heimdall-primary/5 p-6 shadow-sm">
            <h2 className="mb-3 text-lg font-medium text-heimdall-dark">
              Final Combined Access Details
              <span className="ml-2 text-sm font-normal text-gray-600">
                ({result.combinedAccessDetails.length} records)
              </span>
            </h2>
            {result.combinedAccessDetails.length === 0 ? (
              <p className="text-sm text-gray-500 italic">No access details resolved for this lookup.</p>
            ) : (
              <AccessDetailTable entries={result.combinedAccessDetails} showSource />
            )}
          </div>
        </div>
      )}
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Access Detail Table Component                                                */
/* -------------------------------------------------------------------------- */

interface AccessDetailTableProps {
  entries: AccessDetailResultEntry[];
  showGroup?: boolean;
  showSource?: boolean;
}

function AccessDetailTable({ entries, showGroup, showSource }: AccessDetailTableProps) {
  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-sm">
        <thead>
          <tr className="border-b bg-gray-50">
            <th className="px-4 py-2 text-left font-medium text-gray-700">Detail Type</th>
            <th className="px-4 py-2 text-left font-medium text-gray-700">Code</th>
            <th className="px-4 py-2 text-left font-medium text-gray-700">Value</th>
            {showSource && (
              <th className="px-4 py-2 text-left font-medium text-gray-700">Source</th>
            )}
            {showGroup && (
              <th className="px-4 py-2 text-left font-medium text-gray-700">Group</th>
            )}
            <th className="px-4 py-2 text-left font-medium text-gray-700">Description</th>
          </tr>
        </thead>
        <tbody>
          {entries.map((entry) => (
            <tr key={entry.accessDetailId} className="border-b last:border-0">
              <td className="px-4 py-2 font-mono text-xs">{entry.accessDetailType}</td>
              <td className="px-4 py-2 font-mono text-xs">{entry.accessDetailCode}</td>
              <td className="px-4 py-2">{entry.accessDetailValue}</td>
              {showSource && (
                <td className="px-4 py-2">
                  <SourceIndicator source={entry.source} groupName={entry.groupName} />
                </td>
              )}
              {showGroup && (
                <td className="px-4 py-2 text-gray-600">{entry.groupName ?? '—'}</td>
              )}
              <td className="px-4 py-2 text-gray-600">{entry.description ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Source Indicator Component                                                    */
/* -------------------------------------------------------------------------- */

interface SourceIndicatorProps {
  source: string;
  groupName: string | null;
}

function SourceIndicator({ source, groupName }: SourceIndicatorProps) {
  if (source === 'DirectUser') {
    return (
      <span className="inline-flex items-center rounded-full bg-blue-100 px-2.5 py-0.5 text-xs font-medium text-blue-800">
        Direct
      </span>
    );
  }

  return (
    <span className="inline-flex items-center rounded-full bg-purple-100 px-2.5 py-0.5 text-xs font-medium text-purple-800">
      Group: {groupName ?? 'Unknown'}
    </span>
  );
}
