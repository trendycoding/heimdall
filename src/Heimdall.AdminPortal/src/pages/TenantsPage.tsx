import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { DataTable, Column, StatusBadge, Modal, ConfirmDialog, Form, FormField, FormInput, FormSelect, FormActions } from '../components';
import { useListQuery, useCreateMutation, useUpdateMutation, useDeactivateMutation } from '../hooks/useApi';
import type { Tenant } from '../types';

const tenantSchema = z.object({
  name: z.string().min(1, 'Name is required').max(128, 'Max 128 characters'),
  slug: z.string().min(1, 'Slug is required').max(64, 'Max 64 characters')
    .regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, 'Slug must be lowercase alphanumeric with hyphens'),
  primaryIdentityMode: z.string().min(1, 'Identity mode is required'),
});

type TenantFormData = z.infer<typeof tenantSchema>;

const identityModeOptions = [
  { value: 'EntraExternalId', label: 'Entra External ID' },
  { value: 'ExternalOidc', label: 'External OIDC' },
  { value: 'CustomJwtIssuer', label: 'Custom JWT Issuer' },
  { value: 'SamlIdp', label: 'SAML IdP' },
];

export function TenantsPage() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editingTenant, setEditingTenant] = useState<Tenant | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);

  const { data, isLoading } = useListQuery<Tenant>(['tenants'], '/tenants', { search });
  const createMutation = useCreateMutation<TenantFormData>('/tenants', ['tenants']);
  const updateMutation = useUpdateMutation<Partial<TenantFormData>>((id) => `/tenants/${id}`, ['tenants']);
  const deactivateMutation = useDeactivateMutation((id) => `/tenants/${id}`, ['tenants']);

  const tenants = data?.items ?? [];

  const columns: Column<Tenant>[] = [
    { key: 'name', header: 'Name' },
    { key: 'slug', header: 'Slug' },
    { key: 'primaryIdentityMode', header: 'Identity Mode' },
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
          <button type="button" className="text-sm text-heimdall-primary hover:underline" onClick={() => setEditingTenant(row)}>Edit</button>
          <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => setDeactivatingId(row.id)}>Deactivate</button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Tenants</h2>
        <button
          type="button"
          className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors"
          onClick={() => setShowCreate(true)}
        >
          Create Tenant
        </button>
      </div>

      <DataTable
        columns={columns}
        data={tenants}
        keyField="id"
        searchPlaceholder="Search tenants..."
        onSearch={setSearch}
        isLoading={isLoading}
      />

      {/* Create Modal */}
      <TenantFormModal
        isOpen={showCreate}
        onClose={() => setShowCreate(false)}
        onSubmit={async (data) => {
          await createMutation.mutateAsync(data);
          setShowCreate(false);
        }}
        title="Create Tenant"
        isLoading={createMutation.isPending}
      />

      {/* Edit Modal */}
      <TenantFormModal
        isOpen={!!editingTenant}
        onClose={() => setEditingTenant(null)}
        onSubmit={async (formData) => {
          if (editingTenant) {
            await updateMutation.mutateAsync({ id: editingTenant.id, input: formData });
            setEditingTenant(null);
          }
        }}
        title="Edit Tenant"
        defaultValues={editingTenant ? {
          name: editingTenant.name,
          slug: editingTenant.slug,
          primaryIdentityMode: editingTenant.primaryIdentityMode,
        } : undefined}
        isLoading={updateMutation.isPending}
        isEdit
      />

      {/* Deactivate Confirmation */}
      <ConfirmDialog
        isOpen={!!deactivatingId}
        onClose={() => setDeactivatingId(null)}
        onConfirm={async () => {
          if (deactivatingId) {
            await deactivateMutation.mutateAsync(deactivatingId);
            setDeactivatingId(null);
          }
        }}
        title="Deactivate Tenant"
        message="Are you sure you want to deactivate this tenant? This action can be reversed later."
        confirmLabel="Deactivate"
        variant="danger"
        isLoading={deactivateMutation.isPending}
      />
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Tenant Form Modal                                                           */
/* -------------------------------------------------------------------------- */

interface TenantFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: TenantFormData) => Promise<void>;
  title: string;
  defaultValues?: TenantFormData;
  isLoading?: boolean;
  isEdit?: boolean;
}

function TenantFormModal({ isOpen, onClose, onSubmit, title, defaultValues, isLoading, isEdit }: TenantFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<TenantFormData>({
    resolver: zodResolver(tenantSchema),
    defaultValues,
    values: defaultValues,
  });

  const handleClose = () => {
    reset();
    onClose();
  };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="lg">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="Name" htmlFor="name" error={errors.name?.message} required>
          <FormInput id="name" {...register('name')} error={!!errors.name} placeholder="Tenant name" />
        </FormField>

        <FormField label="Slug" htmlFor="slug" error={errors.slug?.message} required>
          <FormInput id="slug" {...register('slug')} error={!!errors.slug} placeholder="tenant-slug" disabled={isEdit} />
        </FormField>

        <FormField label="Primary Identity Mode" htmlFor="primaryIdentityMode" error={errors.primaryIdentityMode?.message} required>
          <FormSelect
            id="primaryIdentityMode"
            {...register('primaryIdentityMode')}
            error={!!errors.primaryIdentityMode}
            options={identityModeOptions}
            placeholder="Select identity mode"
          />
        </FormField>

        <FormActions>
          <button type="button" onClick={handleClose} className="rounded-md border px-4 py-2 text-sm text-gray-700 hover:bg-gray-50 transition-colors">
            Cancel
          </button>
          <button type="submit" disabled={isLoading} className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors disabled:opacity-50">
            {isLoading ? 'Saving...' : isEdit ? 'Update' : 'Create'}
          </button>
        </FormActions>
      </Form>
    </Modal>
  );
}
