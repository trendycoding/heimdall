import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { DataTable, Column, StatusBadge, Modal, ConfirmDialog, Form, FormField, FormInput, FormTextArea, FormActions } from '../components';
import { useListQuery, useCreateMutation, useUpdateMutation, useDeactivateMutation } from '../hooks/useApi';
import type { Application } from '../types';

const applicationSchema = z.object({
  name: z.string().min(1, 'Name is required').max(200, 'Max 200 characters'),
  clientIdentifier: z.string().min(1, 'Client identifier is required').max(128, 'Max 128 characters'),
  description: z.string().max(1000, 'Max 1000 characters').optional().default(''),
  allowedRedirectUris: z.string().optional().default(''),
  allowedOrigins: z.string().optional().default(''),
});

type ApplicationFormData = z.infer<typeof applicationSchema>;

export function ApplicationsPage() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editingApp, setEditingApp] = useState<Application | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);

  const { data, isLoading } = useListQuery<Application>(['applications'], '/applications', { search });
  const createMutation = useCreateMutation<Record<string, unknown>>('/applications', ['applications']);
  const updateMutation = useUpdateMutation<Record<string, unknown>>((id) => `/applications/${id}`, ['applications']);
  const deactivateMutation = useDeactivateMutation((id) => `/applications/${id}`, ['applications']);

  const applications = data?.items ?? [];

  const columns: Column<Application>[] = [
    { key: 'name', header: 'Name' },
    { key: 'clientIdentifier', header: 'Client Identifier' },
    { key: 'description', header: 'Description' },
    {
      key: 'status',
      header: 'Status',
      render: (row) => <StatusBadge status={row.status} />,
    },
    { key: 'createdAt', header: 'Created', render: (row) => new Date(row.createdAt).toLocaleDateString() },
    {
      key: 'actions',
      header: 'Actions',
      sortable: false,
      render: (row) => (
        <div className="flex gap-2">
          <button type="button" className="text-sm text-heimdall-primary hover:underline" onClick={() => setEditingApp(row)}>Edit</button>
          <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => setDeactivatingId(row.id)}>Deactivate</button>
        </div>
      ),
    },
  ];

  const transformFormData = (formData: ApplicationFormData) => ({
    name: formData.name,
    clientIdentifier: formData.clientIdentifier,
    description: formData.description,
    allowedRedirectUris: formData.allowedRedirectUris ? formData.allowedRedirectUris.split('\n').filter(Boolean) : [],
    allowedOrigins: formData.allowedOrigins ? formData.allowedOrigins.split('\n').filter(Boolean) : [],
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Applications</h2>
        <button
          type="button"
          className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors"
          onClick={() => setShowCreate(true)}
        >
          Create Application
        </button>
      </div>

      <DataTable columns={columns} data={applications} keyField="id" searchPlaceholder="Search applications..." onSearch={setSearch} isLoading={isLoading} />

      <ApplicationFormModal
        isOpen={showCreate}
        onClose={() => setShowCreate(false)}
        onSubmit={async (formData) => {
          await createMutation.mutateAsync(transformFormData(formData));
          setShowCreate(false);
        }}
        title="Create Application"
        isLoading={createMutation.isPending}
      />

      <ApplicationFormModal
        isOpen={!!editingApp}
        onClose={() => setEditingApp(null)}
        onSubmit={async (formData) => {
          if (editingApp) {
            await updateMutation.mutateAsync({ id: editingApp.id, input: transformFormData(formData) });
            setEditingApp(null);
          }
        }}
        title="Edit Application"
        defaultValues={editingApp ? {
          name: editingApp.name,
          clientIdentifier: editingApp.clientIdentifier,
          description: editingApp.description,
          allowedRedirectUris: editingApp.allowedRedirectUris?.join('\n') ?? '',
          allowedOrigins: editingApp.allowedOrigins?.join('\n') ?? '',
        } : undefined}
        isLoading={updateMutation.isPending}
        isEdit
      />

      <ConfirmDialog
        isOpen={!!deactivatingId}
        onClose={() => setDeactivatingId(null)}
        onConfirm={async () => {
          if (deactivatingId) {
            await deactivateMutation.mutateAsync(deactivatingId);
            setDeactivatingId(null);
          }
        }}
        title="Deactivate Application"
        message="Are you sure you want to deactivate this application? This will affect all users and permissions."
        confirmLabel="Deactivate"
        variant="danger"
        isLoading={deactivateMutation.isPending}
      />
    </div>
  );
}

interface ApplicationFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: ApplicationFormData) => Promise<void>;
  title: string;
  defaultValues?: ApplicationFormData;
  isLoading?: boolean;
  isEdit?: boolean;
}

function ApplicationFormModal({ isOpen, onClose, onSubmit, title, defaultValues, isLoading, isEdit }: ApplicationFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<ApplicationFormData>({
    resolver: zodResolver(applicationSchema),
    defaultValues,
    values: defaultValues,
  });

  const handleClose = () => { reset(); onClose(); };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="lg">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="Name" htmlFor="app-name" error={errors.name?.message} required>
          <FormInput id="app-name" {...register('name')} error={!!errors.name} placeholder="Application name" />
        </FormField>

        <FormField label="Client Identifier" htmlFor="clientIdentifier" error={errors.clientIdentifier?.message} required>
          <FormInput id="clientIdentifier" {...register('clientIdentifier')} error={!!errors.clientIdentifier} placeholder="client-id" disabled={isEdit} />
        </FormField>

        <FormField label="Description" htmlFor="description" error={errors.description?.message}>
          <FormTextArea id="description" {...register('description')} error={!!errors.description} placeholder="Application description" />
        </FormField>

        <FormField label="Allowed Redirect URIs (one per line)" htmlFor="allowedRedirectUris" error={errors.allowedRedirectUris?.message}>
          <FormTextArea id="allowedRedirectUris" {...register('allowedRedirectUris')} error={!!errors.allowedRedirectUris} placeholder="https://example.com/callback" />
        </FormField>

        <FormField label="Allowed Origins (one per line)" htmlFor="allowedOrigins" error={errors.allowedOrigins?.message}>
          <FormTextArea id="allowedOrigins" {...register('allowedOrigins')} error={!!errors.allowedOrigins} placeholder="https://example.com" />
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
