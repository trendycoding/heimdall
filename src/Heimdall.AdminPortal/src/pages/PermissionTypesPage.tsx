import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { DataTable, Column, StatusBadge, Modal, ConfirmDialog, Form, FormField, FormInput, FormTextArea, FormActions } from '../components';
import { useListQuery, useCreateMutation, useUpdateMutation, useDeactivateMutation } from '../hooks/useApi';
import type { PermissionType } from '../types';

const ptSchema = z.object({
  code: z.string().min(1, 'Code is required').max(100, 'Max 100 characters')
    .regex(/^[A-Za-z0-9_]+$/, 'Must be alphanumeric with underscores'),
  name: z.string().min(1, 'Name is required').max(200, 'Max 200 characters'),
  description: z.string().max(1000, 'Max 1000 characters').optional().default(''),
});

type PtFormData = z.infer<typeof ptSchema>;

export function PermissionTypesPage() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editingItem, setEditingItem] = useState<PermissionType | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);

  const { data, isLoading } = useListQuery<PermissionType>(['permission-types'], '/permission-types', { search });
  const createMutation = useCreateMutation<PtFormData>('/permission-types', ['permission-types']);
  const updateMutation = useUpdateMutation<Partial<PtFormData>>((id) => `/permission-types/${id}`, ['permission-types']);
  const deactivateMutation = useDeactivateMutation((id) => `/permission-types/${id}`, ['permission-types']);

  const items = data?.items ?? [];

  const columns: Column<PermissionType>[] = [
    { key: 'code', header: 'Code' },
    { key: 'name', header: 'Name' },
    { key: 'description', header: 'Description' },
    {
      key: 'isSystemReserved',
      header: 'System',
      render: (row) => row.isSystemReserved ? <span className="text-xs font-medium text-amber-700 bg-amber-50 px-2 py-0.5 rounded">System</span> : <span className="text-xs text-gray-400">No</span>,
    },
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
          {!row.isSystemReserved && (
            <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => setDeactivatingId(row.id)}>Deactivate</button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Permission Types</h2>
        <button type="button" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowCreate(true)}>
          Create Permission Type
        </button>
      </div>

      <DataTable columns={columns} data={items} keyField="id" searchPlaceholder="Search permission types..." onSearch={setSearch} isLoading={isLoading} />

      <PtFormModal isOpen={showCreate} onClose={() => setShowCreate(false)} onSubmit={async (d) => { await createMutation.mutateAsync(d); setShowCreate(false); }} title="Create Permission Type" isLoading={createMutation.isPending} />

      <PtFormModal isOpen={!!editingItem} onClose={() => setEditingItem(null)} onSubmit={async (d) => { if (editingItem) { await updateMutation.mutateAsync({ id: editingItem.id, input: d }); setEditingItem(null); } }} title="Edit Permission Type" defaultValues={editingItem ? { code: editingItem.code, name: editingItem.name, description: editingItem.description } : undefined} isLoading={updateMutation.isPending} isEdit />

      <ConfirmDialog isOpen={!!deactivatingId} onClose={() => setDeactivatingId(null)} onConfirm={async () => { if (deactivatingId) { await deactivateMutation.mutateAsync(deactivatingId); setDeactivatingId(null); } }} title="Deactivate Permission Type" message="Are you sure? Permissions of this type will no longer resolve." confirmLabel="Deactivate" variant="danger" isLoading={deactivateMutation.isPending} />
    </div>
  );
}

interface PtFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: PtFormData) => Promise<void>;
  title: string;
  defaultValues?: PtFormData;
  isLoading?: boolean;
  isEdit?: boolean;
}

function PtFormModal({ isOpen, onClose, onSubmit, title, defaultValues, isLoading, isEdit }: PtFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<PtFormData>({
    resolver: zodResolver(ptSchema),
    defaultValues,
    values: defaultValues,
  });

  const handleClose = () => { reset(); onClose(); };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="lg">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="Code" htmlFor="pt-code" error={errors.code?.message} required>
          <FormInput id="pt-code" {...register('code')} error={!!errors.code} placeholder="Read" disabled={isEdit} />
        </FormField>
        <FormField label="Name" htmlFor="pt-name" error={errors.name?.message} required>
          <FormInput id="pt-name" {...register('name')} error={!!errors.name} placeholder="Permission type name" />
        </FormField>
        <FormField label="Description" htmlFor="pt-desc" error={errors.description?.message}>
          <FormTextArea id="pt-desc" {...register('description')} error={!!errors.description} placeholder="Description" />
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
