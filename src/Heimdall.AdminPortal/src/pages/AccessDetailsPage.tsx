import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { DataTable, Column, StatusBadge, Modal, ConfirmDialog, Form, FormField, FormInput, FormSelect, FormActions } from '../components';
import { useListQuery, useCreateMutation, useUpdateMutation, useDeactivateMutation } from '../hooks/useApi';
import type { UserAccessDetail, GroupAccessDetail, FunctionalAreaAccessRequirement } from '../types';

/* -------------------------------------------------------------------------- */
/* Schemas                                                                     */
/* -------------------------------------------------------------------------- */

const accessDetailSchema = z.object({
  ownerType: z.enum(['User', 'Group'], { required_error: 'Type is required' }),
  ownerId: z.string().min(1, 'Owner ID is required'),
  accessDetailType: z.string().min(1, 'Type is required').max(100, 'Max 100 characters'),
  accessDetailCode: z.string().min(1, 'Code is required').max(200, 'Max 200 characters'),
  accessDetailValue: z.string().min(1, 'Value is required').max(500, 'Max 500 characters'),
  validFrom: z.string().optional().default(''),
  validTo: z.string().optional().default(''),
});

type AccessDetailFormData = z.infer<typeof accessDetailSchema>;

const faReqSchema = z.object({
  functionalAreaId: z.string().min(1, 'Functional area is required'),
  accessDetailType: z.string().min(1, 'Access detail type is required').max(100, 'Max 100 characters'),
});

type FaReqFormData = z.infer<typeof faReqSchema>;

const ownerTypeOptions = [
  { value: 'User', label: 'User' },
  { value: 'Group', label: 'Group' },
];

type CombinedAccessDetail = {
  id: string;
  ownerType: string;
  ownerName: string;
  accessDetailType: string;
  accessDetailCode: string;
  accessDetailValue: string;
  isActive: boolean;
};

/* -------------------------------------------------------------------------- */
/* Tabs                                                                        */
/* -------------------------------------------------------------------------- */

type TabId = 'details' | 'requirements';

export function AccessDetailsPage() {
  const [activeTab, setActiveTab] = useState<TabId>('details');

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-4 border-b">
        <button type="button" className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${activeTab === 'details' ? 'border-heimdall-primary text-heimdall-primary' : 'border-transparent text-gray-500 hover:text-gray-700'}`} onClick={() => setActiveTab('details')}>
          User &amp; Group Access Details
        </button>
        <button type="button" className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${activeTab === 'requirements' ? 'border-heimdall-primary text-heimdall-primary' : 'border-transparent text-gray-500 hover:text-gray-700'}`} onClick={() => setActiveTab('requirements')}>
          FA Access Requirements
        </button>
      </div>

      {activeTab === 'details' ? <AccessDetailsTab /> : <FaRequirementsTab />}
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Access Details Tab                                                           */
/* -------------------------------------------------------------------------- */

function AccessDetailsTab() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editingItem, setEditingItem] = useState<CombinedAccessDetail | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<{ id: string; type: string } | null>(null);

  const { data: userData, isLoading: userLoading } = useListQuery<UserAccessDetail>(['user-access-details'], '/access-details/users', { search });
  const { data: groupData, isLoading: groupLoading } = useListQuery<GroupAccessDetail>(['group-access-details'], '/access-details/groups', { search });

  const createUserMutation = useCreateMutation<Record<string, unknown>>('/access-details/users', ['user-access-details']);
  const createGroupMutation = useCreateMutation<Record<string, unknown>>('/access-details/groups', ['group-access-details']);
  const updateUserMutation = useUpdateMutation<Record<string, unknown>>((id) => `/access-details/users/${id}`, ['user-access-details']);
  const updateGroupMutation = useUpdateMutation<Record<string, unknown>>((id) => `/access-details/groups/${id}`, ['group-access-details']);
  const deactivateUserMutation = useDeactivateMutation((id) => `/access-details/users/${id}`, ['user-access-details']);
  const deactivateGroupMutation = useDeactivateMutation((id) => `/access-details/groups/${id}`, ['group-access-details']);

  const userItems: CombinedAccessDetail[] = (userData?.items ?? []).map((i) => ({
    id: i.id, ownerType: 'User', ownerName: i.userDisplayName, accessDetailType: i.accessDetailType, accessDetailCode: i.accessDetailCode, accessDetailValue: i.accessDetailValue, isActive: i.isActive,
  }));
  const groupItems: CombinedAccessDetail[] = (groupData?.items ?? []).map((i) => ({
    id: i.id, ownerType: 'Group', ownerName: i.groupName, accessDetailType: i.accessDetailType, accessDetailCode: i.accessDetailCode, accessDetailValue: i.accessDetailValue, isActive: i.isActive,
  }));
  const allItems = [...userItems, ...groupItems];

  const columns: Column<CombinedAccessDetail>[] = [
    { key: 'ownerType', header: 'Owner Type' },
    { key: 'ownerName', header: 'Owner' },
    { key: 'accessDetailType', header: 'Detail Type' },
    { key: 'accessDetailCode', header: 'Code' },
    { key: 'accessDetailValue', header: 'Value' },
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
          <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => setDeactivatingId({ id: row.id, type: row.ownerType })}>Remove</button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Access Details</h2>
        <button type="button" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowCreate(true)}>
          Create Access Detail
        </button>
      </div>

      <DataTable columns={columns} data={allItems} keyField="id" searchPlaceholder="Search access details..." onSearch={setSearch} isLoading={userLoading || groupLoading} />

      <AccessDetailFormModal
        isOpen={showCreate}
        onClose={() => setShowCreate(false)}
        onSubmit={async (formData) => {
          const payload = {
            accessDetailType: formData.accessDetailType,
            accessDetailCode: formData.accessDetailCode,
            accessDetailValue: formData.accessDetailValue,
            validFrom: formData.validFrom || null,
            validTo: formData.validTo || null,
            ...(formData.ownerType === 'User' ? { userProfileId: formData.ownerId } : { groupId: formData.ownerId }),
          };
          if (formData.ownerType === 'User') {
            await createUserMutation.mutateAsync(payload);
          } else {
            await createGroupMutation.mutateAsync(payload);
          }
          setShowCreate(false);
        }}
        title="Create Access Detail"
        isLoading={createUserMutation.isPending || createGroupMutation.isPending}
      />

      <AccessDetailFormModal
        isOpen={!!editingItem}
        onClose={() => setEditingItem(null)}
        onSubmit={async (formData) => {
          if (editingItem) {
            const payload = { accessDetailValue: formData.accessDetailValue, validFrom: formData.validFrom || null, validTo: formData.validTo || null };
            if (editingItem.ownerType === 'User') {
              await updateUserMutation.mutateAsync({ id: editingItem.id, input: payload });
            } else {
              await updateGroupMutation.mutateAsync({ id: editingItem.id, input: payload });
            }
            setEditingItem(null);
          }
        }}
        title="Edit Access Detail"
        defaultValues={editingItem ? {
          ownerType: editingItem.ownerType as 'User' | 'Group',
          ownerId: '',
          accessDetailType: editingItem.accessDetailType,
          accessDetailCode: editingItem.accessDetailCode,
          accessDetailValue: editingItem.accessDetailValue,
          validFrom: '',
          validTo: '',
        } : undefined}
        isLoading={updateUserMutation.isPending || updateGroupMutation.isPending}
        isEdit
      />

      <ConfirmDialog
        isOpen={!!deactivatingId}
        onClose={() => setDeactivatingId(null)}
        onConfirm={async () => {
          if (deactivatingId) {
            if (deactivatingId.type === 'User') {
              await deactivateUserMutation.mutateAsync(deactivatingId.id);
            } else {
              await deactivateGroupMutation.mutateAsync(deactivatingId.id);
            }
            setDeactivatingId(null);
          }
        }}
        title="Remove Access Detail"
        message="Are you sure you want to remove this access detail?"
        confirmLabel="Remove"
        variant="danger"
        isLoading={deactivateUserMutation.isPending || deactivateGroupMutation.isPending}
      />
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* FA Requirements Tab                                                         */
/* -------------------------------------------------------------------------- */

function FaRequirementsTab() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);

  const { data, isLoading } = useListQuery<FunctionalAreaAccessRequirement>(['fa-access-requirements'], '/access-details/requirements', { search });
  const createMutation = useCreateMutation<FaReqFormData>('/access-details/requirements', ['fa-access-requirements']);
  const deactivateMutation = useDeactivateMutation((id) => `/access-details/requirements/${id}`, ['fa-access-requirements']);

  const items = data?.items ?? [];

  const columns: Column<FunctionalAreaAccessRequirement>[] = [
    { key: 'functionalAreaCode', header: 'Functional Area' },
    { key: 'accessDetailType', header: 'Required Detail Type' },
    {
      key: 'isActive',
      header: 'Status',
      render: (row) => <StatusBadge status={row.isActive ? 'Active' : 'Inactive'} />,
    },
    { key: 'createdAt', header: 'Created', render: (row) => new Date(row.createdAt).toLocaleDateString() },
    {
      key: 'actions',
      header: 'Actions',
      sortable: false,
      render: (row) => (
        <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => setDeactivatingId(row.id)}>Remove</button>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">FA Access Requirements</h2>
        <button type="button" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowCreate(true)}>
          Create Requirement
        </button>
      </div>

      <DataTable columns={columns} data={items} keyField="id" searchPlaceholder="Search requirements..." onSearch={setSearch} isLoading={isLoading} />

      <FaReqFormModal isOpen={showCreate} onClose={() => setShowCreate(false)} onSubmit={async (d) => { await createMutation.mutateAsync(d); setShowCreate(false); }} isLoading={createMutation.isPending} />

      <ConfirmDialog isOpen={!!deactivatingId} onClose={() => setDeactivatingId(null)} onConfirm={async () => { if (deactivatingId) { await deactivateMutation.mutateAsync(deactivatingId); setDeactivatingId(null); } }} title="Remove Requirement" message="Are you sure? This will affect access detail resolution for this functional area." confirmLabel="Remove" variant="danger" isLoading={deactivateMutation.isPending} />
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Access Detail Form Modal                                                    */
/* -------------------------------------------------------------------------- */

interface AccessDetailFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: AccessDetailFormData) => Promise<void>;
  title: string;
  defaultValues?: AccessDetailFormData;
  isLoading?: boolean;
  isEdit?: boolean;
}

function AccessDetailFormModal({ isOpen, onClose, onSubmit, title, defaultValues, isLoading, isEdit }: AccessDetailFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<AccessDetailFormData>({
    resolver: zodResolver(accessDetailSchema),
    defaultValues,
    values: defaultValues,
  });

  const handleClose = () => { reset(); onClose(); };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="lg">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="Owner Type" htmlFor="ad-type" error={errors.ownerType?.message} required>
          <FormSelect id="ad-type" {...register('ownerType')} error={!!errors.ownerType} options={ownerTypeOptions} placeholder="Select type" disabled={isEdit} />
        </FormField>
        {!isEdit && (
          <FormField label="Owner ID" htmlFor="ad-owner" error={errors.ownerId?.message} required>
            <FormInput id="ad-owner" {...register('ownerId')} error={!!errors.ownerId} placeholder="User or group ID" />
          </FormField>
        )}
        <FormField label="Detail Type" htmlFor="ad-dtype" error={errors.accessDetailType?.message} required>
          <FormInput id="ad-dtype" {...register('accessDetailType')} error={!!errors.accessDetailType} placeholder="Region" disabled={isEdit} />
        </FormField>
        <FormField label="Code" htmlFor="ad-code" error={errors.accessDetailCode?.message} required>
          <FormInput id="ad-code" {...register('accessDetailCode')} error={!!errors.accessDetailCode} placeholder="REGION" disabled={isEdit} />
        </FormField>
        <FormField label="Value" htmlFor="ad-value" error={errors.accessDetailValue?.message} required>
          <FormInput id="ad-value" {...register('accessDetailValue')} error={!!errors.accessDetailValue} placeholder="US-EAST" />
        </FormField>
        <FormField label="Valid From" htmlFor="ad-from" error={errors.validFrom?.message}>
          <FormInput id="ad-from" type="datetime-local" {...register('validFrom')} error={!!errors.validFrom} />
        </FormField>
        <FormField label="Valid To" htmlFor="ad-to" error={errors.validTo?.message}>
          <FormInput id="ad-to" type="datetime-local" {...register('validTo')} error={!!errors.validTo} />
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
/* FA Requirement Form Modal                                                   */
/* -------------------------------------------------------------------------- */

interface FaReqFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: FaReqFormData) => Promise<void>;
  isLoading?: boolean;
}

function FaReqFormModal({ isOpen, onClose, onSubmit, isLoading }: FaReqFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<FaReqFormData>({
    resolver: zodResolver(faReqSchema),
  });

  const handleClose = () => { reset(); onClose(); };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="Create FA Access Requirement" size="lg">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="Functional Area ID" htmlFor="far-fa" error={errors.functionalAreaId?.message} required>
          <FormInput id="far-fa" {...register('functionalAreaId')} error={!!errors.functionalAreaId} placeholder="Functional area ID" />
        </FormField>
        <FormField label="Access Detail Type" htmlFor="far-type" error={errors.accessDetailType?.message} required>
          <FormInput id="far-type" {...register('accessDetailType')} error={!!errors.accessDetailType} placeholder="Region" />
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
