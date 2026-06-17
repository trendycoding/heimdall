import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { DataTable, Column, StatusBadge, Modal, ConfirmDialog, Form, FormField, FormInput, FormSelect, FormActions } from '../components';
import { useListQuery, useCreateMutation, useDeactivateMutation } from '../hooks/useApi';
import type { UserPermissionAssignment, GroupPermissionAssignment } from '../types';

type CombinedAssignment = (UserPermissionAssignment | GroupPermissionAssignment) & { assigneeType: string; assigneeName: string };

const assignmentSchema = z.object({
  assigneeType: z.enum(['User', 'Group'], { required_error: 'Type is required' }),
  assigneeId: z.string().min(1, 'Assignee ID is required'),
  permissionId: z.string().min(1, 'Permission is required'),
  effect: z.enum(['Allow', 'Deny'], { required_error: 'Effect is required' }),
  validFrom: z.string().optional().default(''),
  validTo: z.string().optional().default(''),
}).refine(
  (data) => {
    if (data.validFrom && data.validTo) {
      return new Date(data.validFrom) <= new Date(data.validTo);
    }
    return true;
  },
  { message: 'ValidFrom must not exceed ValidTo', path: ['validTo'] }
);

type AssignmentFormData = z.infer<typeof assignmentSchema>;

const effectOptions = [
  { value: 'Allow', label: 'Allow' },
  { value: 'Deny', label: 'Deny' },
];

const typeOptions = [
  { value: 'User', label: 'User' },
  { value: 'Group', label: 'Group' },
];

export function PermissionAssignmentsPage() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [removingId, setRemovingId] = useState<{ id: string; type: string } | null>(null);

  const { data: userData, isLoading: userLoading } = useListQuery<UserPermissionAssignment>(['user-permission-assignments'], '/permission-assignments/users', { search });
  const { data: groupData, isLoading: groupLoading } = useListQuery<GroupPermissionAssignment>(['group-permission-assignments'], '/permission-assignments/groups', { search });

  const createUserMutation = useCreateMutation<Record<string, unknown>>('/permission-assignments/users', ['user-permission-assignments']);
  const createGroupMutation = useCreateMutation<Record<string, unknown>>('/permission-assignments/groups', ['group-permission-assignments']);
  const removeUserMutation = useDeactivateMutation((id) => `/permission-assignments/users/${id}`, ['user-permission-assignments']);
  const removeGroupMutation = useDeactivateMutation((id) => `/permission-assignments/groups/${id}`, ['group-permission-assignments']);

  const userItems: CombinedAssignment[] = (userData?.items ?? []).map((i) => ({
    ...i,
    assigneeType: 'User',
    assigneeName: i.userDisplayName,
  }));
  const groupItems: CombinedAssignment[] = (groupData?.items ?? []).map((i) => ({
    ...i,
    assigneeType: 'Group',
    assigneeName: i.groupName,
  }));
  const allItems = [...userItems, ...groupItems];

  const columns: Column<CombinedAssignment>[] = [
    { key: 'assigneeType', header: 'Type' },
    { key: 'assigneeName', header: 'Assignee' },
    { key: 'permissionCode', header: 'Permission' },
    {
      key: 'effect',
      header: 'Effect',
      render: (row) => <StatusBadge status={row.effect} />,
    },
    {
      key: 'validFrom',
      header: 'Valid From',
      render: (row) => row.validFrom ? new Date(row.validFrom).toLocaleDateString() : '—',
    },
    {
      key: 'validTo',
      header: 'Valid To',
      render: (row) => row.validTo ? new Date(row.validTo).toLocaleDateString() : '—',
    },
    {
      key: 'actions',
      header: 'Actions',
      sortable: false,
      render: (row) => (
        <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => setRemovingId({ id: row.id, type: row.assigneeType })}>Remove</button>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Permission Assignments</h2>
        <button type="button" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowCreate(true)}>
          Create Assignment
        </button>
      </div>

      <DataTable columns={columns} data={allItems} keyField="id" searchPlaceholder="Search assignments..." onSearch={setSearch} isLoading={userLoading || groupLoading} />

      <AssignmentFormModal
        isOpen={showCreate}
        onClose={() => setShowCreate(false)}
        onSubmit={async (formData) => {
          const payload = {
            permissionId: formData.permissionId,
            effect: formData.effect,
            validFrom: formData.validFrom || null,
            validTo: formData.validTo || null,
            ...(formData.assigneeType === 'User' ? { userProfileId: formData.assigneeId } : { groupId: formData.assigneeId }),
          };
          if (formData.assigneeType === 'User') {
            await createUserMutation.mutateAsync(payload);
          } else {
            await createGroupMutation.mutateAsync(payload);
          }
          setShowCreate(false);
        }}
        isLoading={createUserMutation.isPending || createGroupMutation.isPending}
      />

      <ConfirmDialog
        isOpen={!!removingId}
        onClose={() => setRemovingId(null)}
        onConfirm={async () => {
          if (removingId) {
            if (removingId.type === 'User') {
              await removeUserMutation.mutateAsync(removingId.id);
            } else {
              await removeGroupMutation.mutateAsync(removingId.id);
            }
            setRemovingId(null);
          }
        }}
        title="Remove Assignment"
        message="Are you sure you want to remove this permission assignment?"
        confirmLabel="Remove"
        variant="danger"
        isLoading={removeUserMutation.isPending || removeGroupMutation.isPending}
      />
    </div>
  );
}

interface AssignmentFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: AssignmentFormData) => Promise<void>;
  isLoading?: boolean;
}

function AssignmentFormModal({ isOpen, onClose, onSubmit, isLoading }: AssignmentFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<AssignmentFormData>({
    resolver: zodResolver(assignmentSchema),
  });

  const handleClose = () => { reset(); onClose(); };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="Create Permission Assignment" size="lg">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="Assignee Type" htmlFor="assign-type" error={errors.assigneeType?.message} required>
          <FormSelect id="assign-type" {...register('assigneeType')} error={!!errors.assigneeType} options={typeOptions} placeholder="Select type" />
        </FormField>
        <FormField label="Assignee ID" htmlFor="assign-id" error={errors.assigneeId?.message} required>
          <FormInput id="assign-id" {...register('assigneeId')} error={!!errors.assigneeId} placeholder="User profile or group ID" />
        </FormField>
        <FormField label="Permission ID" htmlFor="assign-perm" error={errors.permissionId?.message} required>
          <FormInput id="assign-perm" {...register('permissionId')} error={!!errors.permissionId} placeholder="Permission ID" />
        </FormField>
        <FormField label="Effect" htmlFor="assign-effect" error={errors.effect?.message} required>
          <FormSelect id="assign-effect" {...register('effect')} error={!!errors.effect} options={effectOptions} placeholder="Select effect" />
        </FormField>
        <FormField label="Valid From" htmlFor="assign-from" error={errors.validFrom?.message}>
          <FormInput id="assign-from" type="datetime-local" {...register('validFrom')} error={!!errors.validFrom} />
        </FormField>
        <FormField label="Valid To" htmlFor="assign-to" error={errors.validTo?.message}>
          <FormInput id="assign-to" type="datetime-local" {...register('validTo')} error={!!errors.validTo} />
        </FormField>
        <FormActions>
          <button type="button" onClick={handleClose} className="rounded-md border px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 transition-colors">Cancel</button>
          <button type="submit" disabled={isLoading} className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors disabled:opacity-50">
            {isLoading ? 'Creating...' : 'Create'}
          </button>
        </FormActions>
      </Form>
    </Modal>
  );
}
