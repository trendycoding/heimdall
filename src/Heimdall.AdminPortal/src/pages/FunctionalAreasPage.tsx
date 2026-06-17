import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { DataTable, Column, StatusBadge, Modal, ConfirmDialog, Form, FormField, FormInput, FormTextArea, FormActions } from '../components';
import { useListQuery, useCreateMutation, useUpdateMutation, useDeactivateMutation } from '../hooks/useApi';
import type { FunctionalArea } from '../types';

const faSchema = z.object({
  functionalAreaCode: z.string().min(1, 'Code is required').max(50, 'Max 50 characters')
    .regex(/^[A-Z0-9_]+$/, 'Must be uppercase alphanumeric with underscores'),
  name: z.string().min(1, 'Name is required').max(200, 'Max 200 characters'),
  description: z.string().max(1000, 'Max 1000 characters').optional().default(''),
});

type FaFormData = z.infer<typeof faSchema>;

export function FunctionalAreasPage() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editingItem, setEditingItem] = useState<FunctionalArea | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);

  const { data, isLoading } = useListQuery<FunctionalArea>(['functional-areas'], '/functional-areas', { search });
  const createMutation = useCreateMutation<FaFormData>('/functional-areas', ['functional-areas']);
  const updateMutation = useUpdateMutation<Partial<FaFormData>>((id) => `/functional-areas/${id}`, ['functional-areas']);
  const deactivateMutation = useDeactivateMutation((id) => `/functional-areas/${id}`, ['functional-areas']);

  const items = data?.items ?? [];

  const columns: Column<FunctionalArea>[] = [
    { key: 'functionalAreaCode', header: 'Code' },
    { key: 'name', header: 'Name' },
    { key: 'description', header: 'Description' },
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
        <h2 className="text-xl font-semibold text-heimdall-dark">Functional Areas</h2>
        <button type="button" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowCreate(true)}>
          Create Functional Area
        </button>
      </div>

      <DataTable columns={columns} data={items} keyField="id" searchPlaceholder="Search functional areas..." onSearch={setSearch} isLoading={isLoading} />

      <FaFormModal isOpen={showCreate} onClose={() => setShowCreate(false)} onSubmit={async (d) => { await createMutation.mutateAsync(d); setShowCreate(false); }} title="Create Functional Area" isLoading={createMutation.isPending} />

      <FaFormModal isOpen={!!editingItem} onClose={() => setEditingItem(null)} onSubmit={async (d) => { if (editingItem) { await updateMutation.mutateAsync({ id: editingItem.id, input: d }); setEditingItem(null); } }} title="Edit Functional Area" defaultValues={editingItem ? { functionalAreaCode: editingItem.functionalAreaCode, name: editingItem.name, description: editingItem.description } : undefined} isLoading={updateMutation.isPending} isEdit />

      <ConfirmDialog isOpen={!!deactivatingId} onClose={() => setDeactivatingId(null)} onConfirm={async () => { if (deactivatingId) { await deactivateMutation.mutateAsync(deactivatingId); setDeactivatingId(null); } }} title="Deactivate Functional Area" message="Are you sure? Permissions in this area will no longer resolve." confirmLabel="Deactivate" variant="danger" isLoading={deactivateMutation.isPending} />
    </div>
  );
}

interface FaFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: FaFormData) => Promise<void>;
  title: string;
  defaultValues?: FaFormData;
  isLoading?: boolean;
  isEdit?: boolean;
}

function FaFormModal({ isOpen, onClose, onSubmit, title, defaultValues, isLoading, isEdit }: FaFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<FaFormData>({
    resolver: zodResolver(faSchema),
    defaultValues,
    values: defaultValues,
  });

  const handleClose = () => { reset(); onClose(); };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="lg">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="Code" htmlFor="fa-code" error={errors.functionalAreaCode?.message} required>
          <FormInput id="fa-code" {...register('functionalAreaCode')} error={!!errors.functionalAreaCode} placeholder="ORDERS" disabled={isEdit} />
        </FormField>
        <FormField label="Name" htmlFor="fa-name" error={errors.name?.message} required>
          <FormInput id="fa-name" {...register('name')} error={!!errors.name} placeholder="Functional area name" />
        </FormField>
        <FormField label="Description" htmlFor="fa-desc" error={errors.description?.message}>
          <FormTextArea id="fa-desc" {...register('description')} error={!!errors.description} placeholder="Description" />
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
