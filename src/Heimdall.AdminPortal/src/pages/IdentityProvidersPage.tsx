import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { DataTable, Column, StatusBadge, Modal, ConfirmDialog, Form, FormField, FormInput, FormSelect, FormActions } from '../components';
import { useListQuery, useCreateMutation, useUpdateMutation, useDeactivateMutation } from '../hooks/useApi';
import type { IdentityProvider } from '../types';

const idpSchema = z.object({
  name: z.string().min(1, 'Name is required').max(200, 'Max 200 characters'),
  providerType: z.string().min(1, 'Provider type is required'),
  issuer: z.string().min(1, 'Issuer is required').url('Must be a valid URL'),
  audience: z.string().min(1, 'Audience is required'),
  jwksUri: z.string().url('Must be a valid URL').optional().or(z.literal('')),
  allowedAlgorithms: z.string().optional().default(''),
  clockSkewSeconds: z.coerce.number().min(0).max(600).default(300),
});

type IdpFormData = z.infer<typeof idpSchema>;

const providerTypeOptions = [
  { value: 'EntraExternalId', label: 'Entra External ID' },
  { value: 'ExternalOidc', label: 'External OIDC' },
  { value: 'CustomJwtIssuer', label: 'Custom JWT Issuer' },
  { value: 'SamlIdp', label: 'SAML IdP' },
];

export function IdentityProvidersPage() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editingIdp, setEditingIdp] = useState<IdentityProvider | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);

  const { data, isLoading } = useListQuery<IdentityProvider>(['identity-providers'], '/identity-providers', { search });
  const createMutation = useCreateMutation<Record<string, unknown>>('/identity-providers', ['identity-providers']);
  const updateMutation = useUpdateMutation<Record<string, unknown>>((id) => `/identity-providers/${id}`, ['identity-providers']);
  const deactivateMutation = useDeactivateMutation((id) => `/identity-providers/${id}`, ['identity-providers']);

  const items = data?.items ?? [];

  const columns: Column<IdentityProvider>[] = [
    { key: 'name', header: 'Name' },
    { key: 'providerType', header: 'Provider Type' },
    { key: 'issuer', header: 'Issuer' },
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
          <button type="button" className="text-sm text-heimdall-primary hover:underline" onClick={() => setEditingIdp(row)}>Edit</button>
          <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => setDeactivatingId(row.id)}>Deactivate</button>
        </div>
      ),
    },
  ];

  const transformFormData = (formData: IdpFormData) => ({
    ...formData,
    allowedAlgorithms: formData.allowedAlgorithms ? formData.allowedAlgorithms.split(',').map((s) => s.trim()).filter(Boolean) : [],
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Identity Providers</h2>
        <button type="button" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowCreate(true)}>
          Create Identity Provider
        </button>
      </div>

      <DataTable columns={columns} data={items} keyField="id" searchPlaceholder="Search identity providers..." onSearch={setSearch} isLoading={isLoading} />

      <IdpFormModal
        isOpen={showCreate}
        onClose={() => setShowCreate(false)}
        onSubmit={async (formData) => { await createMutation.mutateAsync(transformFormData(formData)); setShowCreate(false); }}
        title="Create Identity Provider"
        isLoading={createMutation.isPending}
      />

      <IdpFormModal
        isOpen={!!editingIdp}
        onClose={() => setEditingIdp(null)}
        onSubmit={async (formData) => { if (editingIdp) { await updateMutation.mutateAsync({ id: editingIdp.id, input: transformFormData(formData) }); setEditingIdp(null); } }}
        title="Edit Identity Provider"
        defaultValues={editingIdp ? {
          name: editingIdp.name,
          providerType: editingIdp.providerType,
          issuer: editingIdp.issuer,
          audience: editingIdp.audience,
          jwksUri: editingIdp.jwksUri ?? '',
          allowedAlgorithms: editingIdp.allowedAlgorithms?.join(', ') ?? '',
          clockSkewSeconds: editingIdp.clockSkewSeconds,
        } : undefined}
        isLoading={updateMutation.isPending}
        isEdit
      />

      <ConfirmDialog
        isOpen={!!deactivatingId}
        onClose={() => setDeactivatingId(null)}
        onConfirm={async () => { if (deactivatingId) { await deactivateMutation.mutateAsync(deactivatingId); setDeactivatingId(null); } }}
        title="Deactivate Identity Provider"
        message="Are you sure you want to deactivate this identity provider? Token validation for this provider will stop working."
        confirmLabel="Deactivate"
        variant="danger"
        isLoading={deactivateMutation.isPending}
      />
    </div>
  );
}

interface IdpFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: IdpFormData) => Promise<void>;
  title: string;
  defaultValues?: IdpFormData;
  isLoading?: boolean;
  isEdit?: boolean;
}

function IdpFormModal({ isOpen, onClose, onSubmit, title, defaultValues, isLoading, isEdit }: IdpFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<IdpFormData>({
    resolver: zodResolver(idpSchema),
    defaultValues,
    values: defaultValues,
  });

  const handleClose = () => { reset(); onClose(); };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="xl">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="Name" htmlFor="idp-name" error={errors.name?.message} required>
          <FormInput id="idp-name" {...register('name')} error={!!errors.name} placeholder="Provider name" />
        </FormField>

        <FormField label="Provider Type" htmlFor="providerType" error={errors.providerType?.message} required>
          <FormSelect id="providerType" {...register('providerType')} error={!!errors.providerType} options={providerTypeOptions} placeholder="Select type" disabled={isEdit} />
        </FormField>

        <FormField label="Issuer" htmlFor="issuer" error={errors.issuer?.message} required>
          <FormInput id="issuer" {...register('issuer')} error={!!errors.issuer} placeholder="https://issuer.example.com" />
        </FormField>

        <FormField label="Audience" htmlFor="audience" error={errors.audience?.message} required>
          <FormInput id="audience" {...register('audience')} error={!!errors.audience} placeholder="api://my-app" />
        </FormField>

        <FormField label="JWKS URI" htmlFor="jwksUri" error={errors.jwksUri?.message}>
          <FormInput id="jwksUri" {...register('jwksUri')} error={!!errors.jwksUri} placeholder="https://issuer.example.com/.well-known/jwks.json" />
        </FormField>

        <FormField label="Allowed Algorithms (comma-separated)" htmlFor="allowedAlgorithms" error={errors.allowedAlgorithms?.message}>
          <FormInput id="allowedAlgorithms" {...register('allowedAlgorithms')} error={!!errors.allowedAlgorithms} placeholder="RS256, ES256" />
        </FormField>

        <FormField label="Clock Skew (seconds)" htmlFor="clockSkewSeconds" error={errors.clockSkewSeconds?.message}>
          <FormInput id="clockSkewSeconds" type="number" {...register('clockSkewSeconds')} error={!!errors.clockSkewSeconds} placeholder="300" />
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
