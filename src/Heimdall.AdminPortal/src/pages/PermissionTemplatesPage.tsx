import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { DataTable, Column, StatusBadge, Modal, ConfirmDialog, Form, FormField, FormInput, FormTextArea, FormActions } from '../components';
import { useListQuery, useCreateMutation, useUpdateMutation, useDeactivateMutation } from '../hooks/useApi';
import type { PermissionTemplate, TemplateApplicationHistory } from '../types';
import { Pagination } from '../components';

const templateSchema = z.object({
  templateCode: z.string().min(1, 'Code is required').max(100, 'Max 100 characters')
    .regex(/^[A-Z0-9_]+$/, 'Must be uppercase alphanumeric with underscores'),
  name: z.string().min(1, 'Name is required').max(200, 'Max 200 characters'),
  description: z.string().max(1000, 'Max 1000 characters').optional().default(''),
});

type TemplateFormData = z.infer<typeof templateSchema>;

export function PermissionTemplatesPage() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editingItem, setEditingItem] = useState<PermissionTemplate | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);
  const [viewingHistory, setViewingHistory] = useState<PermissionTemplate | null>(null);

  const { data, isLoading } = useListQuery<PermissionTemplate>(['permission-templates'], '/permission-templates', { search });
  const createMutation = useCreateMutation<TemplateFormData>('/permission-templates', ['permission-templates']);
  const updateMutation = useUpdateMutation<Partial<TemplateFormData>>((id) => `/permission-templates/${id}`, ['permission-templates']);
  const deactivateMutation = useDeactivateMutation((id) => `/permission-templates/${id}`, ['permission-templates']);

  const items = data?.items ?? [];

  const columns: Column<PermissionTemplate>[] = [
    { key: 'templateCode', header: 'Code' },
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
          <button type="button" className="text-sm text-heimdall-primary hover:underline" onClick={() => setViewingHistory(row)}>History</button>
          <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => setDeactivatingId(row.id)}>Deactivate</button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Permission Templates</h2>
        <button type="button" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowCreate(true)}>
          Create Template
        </button>
      </div>

      <DataTable columns={columns} data={items} keyField="id" searchPlaceholder="Search templates..." onSearch={setSearch} isLoading={isLoading} />

      <TemplateFormModal isOpen={showCreate} onClose={() => setShowCreate(false)} onSubmit={async (d) => { await createMutation.mutateAsync(d); setShowCreate(false); }} title="Create Template" isLoading={createMutation.isPending} />

      <TemplateFormModal isOpen={!!editingItem} onClose={() => setEditingItem(null)} onSubmit={async (d) => { if (editingItem) { await updateMutation.mutateAsync({ id: editingItem.id, input: d }); setEditingItem(null); } }} title="Edit Template" defaultValues={editingItem ? { templateCode: editingItem.templateCode, name: editingItem.name, description: editingItem.description } : undefined} isLoading={updateMutation.isPending} isEdit />

      <ConfirmDialog isOpen={!!deactivatingId} onClose={() => setDeactivatingId(null)} onConfirm={async () => { if (deactivatingId) { await deactivateMutation.mutateAsync(deactivatingId); setDeactivatingId(null); } }} title="Deactivate Template" message="Are you sure? This template will no longer be available for application." confirmLabel="Deactivate" variant="danger" isLoading={deactivateMutation.isPending} />

      {viewingHistory && (
        <TemplateHistoryModal template={viewingHistory} onClose={() => setViewingHistory(null)} />
      )}
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Template Form Modal                                                         */
/* -------------------------------------------------------------------------- */

interface TemplateFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: TemplateFormData) => Promise<void>;
  title: string;
  defaultValues?: TemplateFormData;
  isLoading?: boolean;
  isEdit?: boolean;
}

function TemplateFormModal({ isOpen, onClose, onSubmit, title, defaultValues, isLoading, isEdit }: TemplateFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<TemplateFormData>({
    resolver: zodResolver(templateSchema),
    defaultValues,
    values: defaultValues,
  });

  const handleClose = () => { reset(); onClose(); };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="lg">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="Template Code" htmlFor="tmpl-code" error={errors.templateCode?.message} required>
          <FormInput id="tmpl-code" {...register('templateCode')} error={!!errors.templateCode} placeholder="STANDARD_USER" disabled={isEdit} />
        </FormField>
        <FormField label="Name" htmlFor="tmpl-name" error={errors.name?.message} required>
          <FormInput id="tmpl-name" {...register('name')} error={!!errors.name} placeholder="Template name" />
        </FormField>
        <FormField label="Description" htmlFor="tmpl-desc" error={errors.description?.message}>
          <FormTextArea id="tmpl-desc" {...register('description')} error={!!errors.description} placeholder="Description" />
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

/* -------------------------------------------------------------------------- */
/* Template Application History Modal                                          */
/* -------------------------------------------------------------------------- */

function TemplateHistoryModal({ template, onClose }: { template: PermissionTemplate; onClose: () => void }) {
  const [page, setPage] = useState(1);

  const { data, isLoading } = useListQuery<TemplateApplicationHistory>(
    ['template-history', template.id],
    `/permission-templates/${template.id}/history`,
    { page, pageSize: 20 }
  );

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  const columns: Column<TemplateApplicationHistory>[] = [
    { key: 'userDisplayName', header: 'User' },
    { key: 'templateCode', header: 'Template' },
    { key: 'appliedBy', header: 'Applied By' },
    { key: 'appliedAt', header: 'Applied At', render: (row) => new Date(row.appliedAt).toLocaleString() },
  ];

  return (
    <Modal isOpen onClose={onClose} title={`Application History — ${template.name}`} size="xl">
      <div className="space-y-4">
        <DataTable columns={columns} data={items} keyField="id" searchPlaceholder="Search history..." isLoading={isLoading} emptyMessage="No application history." />
        <Pagination currentPage={page} totalPages={totalPages} onPageChange={setPage} />
      </div>
    </Modal>
  );
}
