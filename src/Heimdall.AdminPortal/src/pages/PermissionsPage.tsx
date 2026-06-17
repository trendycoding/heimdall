import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { DataTable, Column, StatusBadge, Modal, ConfirmDialog, Form, FormField, FormInput, FormTextArea, FormActions } from '../components';
import { useListQuery, useCreateMutation, useUpdateMutation, useDeactivateMutation } from '../hooks/useApi';
import type { Permission } from '../types';

const permSchema = z.object({
  permissionCode: z.string().min(1, 'Code is required').max(200, 'Max 200 characters')
    .regex(/^[A-Z0-9_]+$/, 'Must be uppercase alphanumeric with underscores'),
  name: z.string().min(1, 'Name is required').max(200, 'Max 200 characters'),
  description: z.string().max(1000, 'Max 1000 characters').optional().default(''),
  functionalAreaId: z.string().min(1, 'Functional area is required'),
  permissionTypeId: z.string().min(1, 'Permission type is required'),
});

type PermFormData = z.infer<typeof permSchema>;

export function PermissionsPage() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editingItem, setEditingItem] = useState<Permission | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);

  const { data, isLoading } = useListQuery<Permission>(['permissions'], '/permissions', { search });
  const createMutation = useCreateMutation<PermFormData>('/permissions', ['permissions']);
  const updateMutation = useUpdateMutation<Partial<PermFormData>>((id) => `/permissions/${id}`, ['permissions']);
  const deactivateMutation = useDeactivateMutation((id) => `/permissions/${id}`, ['permissions']);

  const items = data?.items ?? [];

  const columns: Column<Permission>[] = [
    { key: 'permissionCode', header: 'Code' },
    { key: 'name', header: 'Name' },
    { key: 'functionalAreaCode', header: 'Functional Area' },
    { key: 'permissionTypeCode', header: 'Permission Type' },
    {
      key: 'isActive',
      header: 'Status',
      render: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} />,
    },
    {
      key: 'actions',
      header: 'Actions',
      sortable: false,
      render: (row) => (
        <div className="flex gap-2">
          <button type="button" className="text-sm text-heimdall-primary hover:underline" onClick={() => setEditingItem(row)}>Edit</button>
          <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => setDeactivatingId(row.id)}>Deactivate</button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Permissions</h2>
        <button type="button" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowCreate(true)}>
          Create Permission
        </button>
      </div>

      <DataTable columns={columns} data={items} keyField="id" searchPlaceholder="Search permissions..." onSearch={setSearch} isLoading={isLoading} />

      <PermFormModal isOpen={showCreate} onClose={() => setShowCreate(false)} onSubmit={async (d) => { await createMutation.mutateAsync(d); setShowCreate(false); }} title="Create Permission" isLoading={createMutation.isPending} />

      <PermFormModal isOpen={!!editingItem} onClose={() => setEditingItem(null)} onSubmit={async (d) => { if (editingItem) { await updateMutation.mutateAsync({ id: editingItem.id, input: d }); setEditingItem(null); } }} title="Edit Permission" defaultValues={editingItem ? { permissionCode: editingItem.permissionCode, name: editingItem.name, description: editingItem.description, functionalAreaId: editingItem.functionalAreaId, permissionTypeId: editingItem.permissionTypeId } : undefined} isLoading={updateMutation.isPending} isEdit />

      <ConfirmDialog isOpen={!!deactivatingId} onClose={() => setDeactivatingId(null)} onConfirm={async () => { if (deactivatingId) { await deactivateMutation.mutateAsync(deactivatingId); setDeactivatingId(null); } }} title="Deactivate Permission" message="Are you sure? This permission will no longer resolve in access checks." confirmLabel="Deactivate" variant="danger" isLoading={deactivateMutation.isPending} />
    </div>
  );
}

interface PermFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: PermFormData) => Promise<void>;
  title: string;
  defaultValues?: PermFormData;
  isLoading?: boolean;
  isEdit?: boolean;
}

function PermFormModal({ isOpen, onClose, onSubmit, title, defaultValues, isLoading, isEdit }: PermFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<PermFormData>({
    resolver: zodResolver(permSchema),
    defaultValues,
    values: defaultValues,
  });

  const handleClose = () => { reset(); onClose(); };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="lg">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="Permission Code" htmlFor="perm-code" error={errors.permissionCode?.message} required>
          <FormInput id="perm-code" {...register('permissionCode')} error={!!errors.permissionCode} placeholder="ORDERS_READ" disabled={isEdit} />
        </FormField>
        <FormField label="Name" htmlFor="perm-name" error={errors.name?.message} required>
          <FormInput id="perm-name" {...register('name')} error={!!errors.name} placeholder="Permission name" />
        </FormField>
        <FormField label="Description" htmlFor="perm-desc" error={errors.description?.message}>
          <FormTextArea id="perm-desc" {...register('description')} error={!!errors.description} placeholder="Description" />
        </FormField>
        <FormField label="Functional Area ID" htmlFor="perm-fa" error={errors.functionalAreaId?.message} required>
          <FormInput id="perm-fa" {...register('functionalAreaId')} error={!!errors.functionalAreaId} placeholder="Functional area ID" disabled={isEdit} />
        </FormField>
        <FormField label="Permission Type ID" htmlFor="perm-pt" error={errors.permissionTypeId?.message} required>
          <FormInput id="perm-pt" {...register('permissionTypeId')} error={!!errors.permissionTypeId} placeholder="Permission type ID" disabled={isEdit} />
        </FormField>
        <FormActions>
          <button type="button" onClick={handleClose} className="rounded-md border px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 transition-colors">Cancel</button>
          <button type="submit" disabled={isLoading} className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors disabled:opacity-50">
            {isLoading ? 'Saving...' : isEdit ? 'Update' : 'Create'}
          </button>
        </FormActions>
      </Form>
    </Modal>
  );
}
