import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation } from '@tanstack/react-query';
import { Form, FormField, FormInput, FormActions } from '../components';
import apiClient from '../services/apiClient';
import type { ApiEnvelope, PermissionCheckResult, PermissionCheckMatchedAssignment } from '../types';

/* -------------------------------------------------------------------------- */
/* Form Schema                                                                 */
/* -------------------------------------------------------------------------- */

const simulationSchema = z.object({
  userProfileId: z.string().min(1, 'User profile ID is required'),
  applicationId: z.string().min(1, 'Application ID is required'),
  functionalAreaCode: z.string().min(1, 'Functional area code is required'),
  permissionTypeCode: z.string().min(1, 'Permission type code is required'),
  permissionCode: z.string().optional().default(''),
  evaluationDateTime: z.string().optional().default(''),
});

type SimulationFormData = z.infer<typeof simulationSchema>;

/* -------------------------------------------------------------------------- */
/* Page Component                                                              */
/* -------------------------------------------------------------------------- */

export function PermissionSimulationPage() {
  const [result, setResult] = useState<PermissionCheckResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { register, handleSubmit, formState: { errors } } = useForm<SimulationFormData>({
    resolver: zodResolver(simulationSchema),
  });

  const checkMutation = useMutation({
    mutationFn: async (formData: SimulationFormData) => {
      const tenantId = '00000000-0000-0000-0000-000000000001'; // Uses the context tenant from API
      const payload: Record<string, unknown> = {
        userProfileId: formData.userProfileId,
        functionalAreaCode: formData.functionalAreaCode,
        permissionTypeCode: formData.permissionTypeCode,
      };

      if (formData.permissionCode) {
        payload.permissionCode = formData.permissionCode;
      }

      const url = `/tenants/${tenantId}/applications/${formData.applicationId}/access-checks`;
      const { data } = await apiClient.post<ApiEnvelope<PermissionCheckResult>>(url, payload);
      return data.data;
    },
    onSuccess: (data) => {
      setResult(data);
      setError(null);
    },
    onError: (err: unknown) => {
      setResult(null);
      const message = err instanceof Error ? err.message : 'Permission check failed';
      setError(message);
    },
  });

  const onSubmit = (formData: SimulationFormData) => {
    checkMutation.mutate(formData);
  };

  // Categorize matched assignments
  const directAssignments = result?.matchedAssignments.filter(a => a.source === 'DirectUser') ?? [];
  const groupAssignments = result?.matchedAssignments.filter(a => a.source === 'Group') ?? [];
  const denyAssignments = result?.matchedAssignments.filter(a => a.effect === 'Deny') ?? [];

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-heimdall-dark">Permission Simulation</h2>
        <p className="mt-1 text-sm text-gray-500">
          Simulate a permission check to see what decision would be made and why.
        </p>
      </div>

      {/* Input Form */}
      <div className="rounded-lg border bg-white p-6 shadow-sm">
        <h3 className="mb-4 text-lg font-medium text-gray-900">Simulation Parameters</h3>
        <Form onSubmit={handleSubmit(onSubmit)}>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <FormField label="User Profile ID" htmlFor="sim-user" error={errors.userProfileId?.message} required>
              <FormInput id="sim-user" {...register('userProfileId')} error={!!errors.userProfileId} placeholder="User profile GUID" />
            </FormField>
            <FormField label="Application ID" htmlFor="sim-app" error={errors.applicationId?.message} required>
              <FormInput id="sim-app" {...register('applicationId')} error={!!errors.applicationId} placeholder="Application GUID" />
            </FormField>
            <FormField label="Functional Area Code" htmlFor="sim-fa" error={errors.functionalAreaCode?.message} required>
              <FormInput id="sim-fa" {...register('functionalAreaCode')} error={!!errors.functionalAreaCode} placeholder="e.g. ORDERS" />
            </FormField>
            <FormField label="Permission Type Code" htmlFor="sim-pt" error={errors.permissionTypeCode?.message} required>
              <FormInput id="sim-pt" {...register('permissionTypeCode')} error={!!errors.permissionTypeCode} placeholder="e.g. READ" />
            </FormField>
            <FormField label="Permission Code (optional)" htmlFor="sim-pc" error={errors.permissionCode?.message}>
              <FormInput id="sim-pc" {...register('permissionCode')} error={!!errors.permissionCode} placeholder="e.g. ORDERS_READ" />
            </FormField>
            <FormField label="Evaluation Date/Time (optional)" htmlFor="sim-dt" error={errors.evaluationDateTime?.message}>
              <FormInput id="sim-dt" type="datetime-local" {...register('evaluationDateTime')} error={!!errors.evaluationDateTime} />
            </FormField>
          </div>
          <FormActions>
            <button
              type="submit"
              disabled={checkMutation.isPending}
              className="rounded-md bg-heimdall-primary px-6 py-2 text-sm font-medium text-white hover:bg-heimdall-secondary transition-colors disabled:opacity-50"
            >
              {checkMutation.isPending ? 'Checking...' : 'Run Simulation'}
            </button>
          </FormActions>
        </Form>
      </div>

      {/* Error Display */}
      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4">
          <div className="flex items-center gap-2">
            <span className="text-red-600">✗</span>
            <p className="text-sm font-medium text-red-800">Simulation Error</p>
          </div>
          <p className="mt-1 text-sm text-red-700">{error}</p>
        </div>
      )}

      {/* Results Display */}
      {result && (
        <div className="space-y-4">
          {/* Final Decision Banner */}
          <div
            className={`rounded-lg border p-6 ${
              result.allowed
                ? 'border-green-200 bg-green-50'
                : 'border-red-200 bg-red-50'
            }`}
          >
            <div className="flex items-center gap-3">
              <span className={`text-3xl ${result.allowed ? 'text-green-600' : 'text-red-600'}`}>
                {result.allowed ? '✓' : '✗'}
              </span>
              <div>
                <h3 className={`text-lg font-semibold ${result.allowed ? 'text-green-800' : 'text-red-800'}`}>
                  {result.decision === 'Allow' ? 'ALLOWED' : 'DENIED'}
                </h3>
                <p className={`text-sm ${result.allowed ? 'text-green-700' : 'text-red-700'}`}>
                  {result.reason}
                </p>
              </div>
            </div>
          </div>

          {/* Decision Details */}
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
            {/* Direct Assignments */}
            <AssignmentCard
              title="Direct Assignments"
              description="Assignments made directly to the user"
              assignments={directAssignments}
              emptyMessage="No direct assignments found"
            />

            {/* Group Assignments */}
            <AssignmentCard
              title="Group Assignments"
              description="Assignments inherited from group memberships"
              assignments={groupAssignments}
              emptyMessage="No group assignments found"
              showGroup
            />

            {/* Deny Assignments */}
            <AssignmentCard
              title="Deny Assignments"
              description="Explicit deny assignments that override allows"
              assignments={denyAssignments}
              emptyMessage="No deny assignments"
              variant="danger"
            />
          </div>

          {/* Resolution Summary */}
          <div className="rounded-lg border bg-white p-6 shadow-sm">
            <h4 className="mb-3 text-base font-medium text-gray-900">Resolution Summary</h4>
            <dl className="grid grid-cols-1 gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
              <SummaryItem
                label="Total Assignments Considered"
                value={String(result.matchedAssignments.length)}
              />
              <SummaryItem
                label="Direct Assignments"
                value={String(directAssignments.length)}
              />
              <SummaryItem
                label="Group Assignments"
                value={String(groupAssignments.length)}
              />
              <SummaryItem
                label="Deny Assignments"
                value={String(denyAssignments.length)}
              />
            </dl>
            <div className="mt-4 border-t pt-4">
              <p className="text-xs text-gray-500">
                Note: Expired assignments, inactive records, and template application history are
                resolved server-side. Assignments shown here are those that were temporally valid
                and active at the evaluation time.
              </p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Sub-components                                                               */
/* -------------------------------------------------------------------------- */

interface AssignmentCardProps {
  title: string;
  description: string;
  assignments: PermissionCheckMatchedAssignment[];
  emptyMessage: string;
  showGroup?: boolean;
  variant?: 'default' | 'danger';
}

function AssignmentCard({ title, description, assignments, emptyMessage, showGroup, variant = 'default' }: AssignmentCardProps) {
  const borderColor = variant === 'danger' ? 'border-red-200' : 'border-gray-200';

  return (
    <div className={`rounded-lg border ${borderColor} bg-white p-4 shadow-sm`}>
      <h4 className="text-sm font-medium text-gray-900">{title}</h4>
      <p className="mb-3 text-xs text-gray-500">{description}</p>
      {assignments.length === 0 ? (
        <p className="text-xs italic text-gray-400">{emptyMessage}</p>
      ) : (
        <ul className="space-y-2">
          {assignments.map((a) => (
            <li key={a.assignmentId} className="flex items-center justify-between rounded border px-3 py-2 text-xs">
              <div>
                <span className="font-mono text-gray-600">{a.assignmentId.slice(0, 8)}...</span>
                {showGroup && a.groupName && (
                  <span className="ml-2 text-gray-500">via {a.groupName}</span>
                )}
              </div>
              <span
                className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                  a.effect === 'Allow'
                    ? 'bg-green-100 text-green-700'
                    : 'bg-red-100 text-red-700'
                }`}
              >
                {a.effect}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

interface SummaryItemProps {
  label: string;
  value: string;
}

function SummaryItem({ label, value }: SummaryItemProps) {
  return (
    <div>
      <dt className="text-gray-500">{label}</dt>
      <dd className="mt-0.5 text-lg font-semibold text-gray-900">{value}</dd>
    </div>
  );
}
