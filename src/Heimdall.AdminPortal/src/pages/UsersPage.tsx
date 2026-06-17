import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { DataTable, Column, StatusBadge, Modal, ConfirmDialog, Form, FormField, FormInput, FormSelect, FormActions } from '../components';
import { useListQuery, useCreateMutation, useUpdateMutation, useDeactivateMutation } from '../hooks/useApi';
import type { UserProfile } from '../types';

const userSchema = z.object({
  externalSubjectId: z.string().min(1, 'External subject ID is required'),
  identityProvider: z.string().min(1, 'Identity provider is required'),
  email: z.string().email('Must be a valid email'),
  displayName: z.string().min(1, 'Display name is required').max(256, 'Max 256 characters'),
});

type UserFormData = z.infer<typeof userSchema>;

const idpOptions = [
  { value: 'EntraExternalId', label: 'Entra External ID' },
  { value: 'ExternalOidc', label: 'External OIDC' },
  { value: 'CustomJwtIssuer', label: 'Custom JWT Issuer' },
];

export function UsersPage() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editingUser, setEditingUser] = useState<UserProfile | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);

  const { data, isLoading } = useListQuery<UserProfile>(['users'], '/users', { search });
  const createMutation = useCreateMutation<UserFormData>('/users', ['users']);
  const updateMutation = useUpdateMutation<Partial<UserFormData>>((id) => `/users/${id}`, ['users']);
  const deactivateMutation = useDeactivateMutation((id) => `/users/${id}`, ['users']);

  const users = data?.items ?? [];

  const columns: Column<UserProfile>[] = [
    { key: 'displayName', header: 'Display Name' },
    { key: 'email', header: 'Email' },
    { key: 'identityProvider', header: 'Identity Provider' },
    {
      key: 'status',
      header: 'Status',
      render: (row) => <StatusBadge status={row.status} />,
    },
    {
      key: 'lastLoginAt',
      header: 'Last Login',
      render: (row) => row.lastLoginAt ? new Date(row.lastLoginAt).toLocaleDateString() : 'Never',
    },
    {
      key: 'actions',
      header: 'Actions',
      sortable: false,
      render: (row) => (
        <div className="flex gap-2">
          <button type="button" className="text-sm text-heimdall-primary hover:underline" onClick={() => setEditingUser(row)}>Edit</button>
          <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => setDeactivatingId(row.id)}>Deactivate</button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Users</h2>
        <button type="button" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowCreate(true)}>
          Sync User
        </button>
      </div>

      <DataTable columns={columns} data={users} keyField="id" searchPlaceholder="Search users..." onSearch={setSearch} isLoading={isLoading} />

      <UserFormModal
        isOpen={showCreate}
        onClose={() => setShowCreate(false)}
        onSubmit={async (formData) => { await createMutation.mutateAsync(formData); setShowCreate(false); }}
        title="Sync User"
        isLoading={createMutation.isPending}
      />

      <UserFormModal
        isOpen={!!editingUser}
        onClose={() => setEditingUser(null)}
        onSubmit={async (formData) => { if (editingUser) { await updateMutation.mutateAsync({ id: editingUser.id, input: formData }); setEditingUser(null); } }}
        title="Edit User"
        defaultValues={editingUser ? {
          externalSubjectId: editingUser.externalSubjectId,
          identityProvider: editingUser.identityProvider,
          email: editingUser.email,
          displayName: editingUser.displayName,
        } : undefined}
        isLoading={updateMutation.isPending}
        isEdit
      />

      <ConfirmDialog
        isOpen={!!deactivatingId}
        onClose={() => setDeactivatingId(null)}
        onConfirm={async () => { if (deactivatingId) { await deactivateMutation.mutateAsync(deactivatingId); setDeactivatingId(null); } }}
        title="Deactivate User"
        message="Are you sure you want to deactivate this user? They will no longer be able to access the system."
        confirmLabel="Deactivate"
        variant="danger"
        isLoading={deactivateMutation.isPending}
      />
    </div>
  );
}

interface UserFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: UserFormData) => Promise<void>;
  title: string;
  defaultValues?: UserFormData;
  isLoading?: boolean;
  isEdit?: boolean;
}

function UserFormModal({ isOpen, onClose, onSubmit, title, defaultValues, isLoading, isEdit }: UserFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<UserFormData>({
    resolver: zodResolver(userSchema),
    defaultValues,
    values: defaultValues,
  });

  const handleClose = () => { reset(); onClose(); };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="lg">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="External Subject ID" htmlFor="externalSubjectId" error={errors.externalSubjectId?.message} required>
          <FormInput id="externalSubjectId" {...register('externalSubjectId')} error={!!errors.externalSubjectId} placeholder="ext-subject-id" disabled={isEdit} />
        </FormField>

        <FormField label="Identity Provider" htmlFor="identityProvider" error={errors.identityProvider?.message} required>
          <FormSelect id="identityProvider" {...register('identityProvider')} error={!!errors.identityProvider} options={idpOptions} placeholder="Select provider" disabled={isEdit} />
        </FormField>

        <FormField label="Email" htmlFor="user-email" error={errors.email?.message} required>
          <FormInput id="user-email" type="email" {...register('email')} error={!!errors.email} placeholder="user@example.com" />
        </FormField>

        <FormField label="Display Name" htmlFor="displayName" error={errors.displayName?.message} required>
          <FormInput id="displayName" {...register('displayName')} error={!!errors.displayName} placeholder="Full name" />
        </FormField>

        <FormActions>
          <button type="button" onClick={handleClose} className="rounded-md border px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 transition-colors">Cancel</button>
          <button type="submit" disabled={isLoading} className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors disabled:opacity-50">
            {isLoading ? 'Saving...' : isEdit ? 'Update' : 'Sync'}
          </button>
        </FormActions>
      </Form>
    </Modal>
  );
}
