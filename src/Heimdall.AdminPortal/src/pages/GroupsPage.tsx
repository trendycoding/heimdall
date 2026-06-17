import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { DataTable, Column, StatusBadge, Modal, ConfirmDialog, Form, FormField, FormInput, FormTextArea, FormActions } from '../components';
import { useListQuery, useCreateMutation, useUpdateMutation, useDeactivateMutation } from '../hooks/useApi';
import type { Group, GroupMembership } from '../types';

const groupSchema = z.object({
  name: z.string().min(1, 'Name is required').max(200, 'Max 200 characters'),
  description: z.string().max(1000, 'Max 1000 characters').optional().default(''),
});

type GroupFormData = z.infer<typeof groupSchema>;

const memberSchema = z.object({
  userProfileId: z.string().min(1, 'User is required'),
});

type MemberFormData = z.infer<typeof memberSchema>;

export function GroupsPage() {
  const [search, setSearch] = useState('');
  const [showCreate, setShowCreate] = useState(false);
  const [editingItem, setEditingItem] = useState<Group | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);
  const [managingMembers, setManagingMembers] = useState<Group | null>(null);

  const { data, isLoading } = useListQuery<Group>(['groups'], '/groups', { search });
  const createMutation = useCreateMutation<GroupFormData>('/groups', ['groups']);
  const updateMutation = useUpdateMutation<Partial<GroupFormData>>((id) => `/groups/${id}`, ['groups']);
  const deactivateMutation = useDeactivateMutation((id) => `/groups/${id}`, ['groups']);

  const items = data?.items ?? [];

  const columns: Column<Group>[] = [
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
          <button type="button" className="text-sm text-heimdall-primary hover:underline" onClick={() => setManagingMembers(row)}>Members</button>
          <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => setDeactivatingId(row.id)}>Deactivate</button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Groups</h2>
        <button type="button" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowCreate(true)}>
          Create Group
        </button>
      </div>

      <DataTable columns={columns} data={items} keyField="id" searchPlaceholder="Search groups..." onSearch={setSearch} isLoading={isLoading} />

      <GroupFormModal isOpen={showCreate} onClose={() => setShowCreate(false)} onSubmit={async (d) => { await createMutation.mutateAsync(d); setShowCreate(false); }} title="Create Group" isLoading={createMutation.isPending} />

      <GroupFormModal isOpen={!!editingItem} onClose={() => setEditingItem(null)} onSubmit={async (d) => { if (editingItem) { await updateMutation.mutateAsync({ id: editingItem.id, input: d }); setEditingItem(null); } }} title="Edit Group" defaultValues={editingItem ? { name: editingItem.name, description: editingItem.description } : undefined} isLoading={updateMutation.isPending} isEdit />

      <ConfirmDialog isOpen={!!deactivatingId} onClose={() => setDeactivatingId(null)} onConfirm={async () => { if (deactivatingId) { await deactivateMutation.mutateAsync(deactivatingId); setDeactivatingId(null); } }} title="Deactivate Group" message="Are you sure? Group permission assignments will no longer be inherited by members." confirmLabel="Deactivate" variant="danger" isLoading={deactivateMutation.isPending} />

      {managingMembers && (
        <GroupMembersModal group={managingMembers} onClose={() => setManagingMembers(null)} />
      )}
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Group Form Modal                                                            */
/* -------------------------------------------------------------------------- */

interface GroupFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSubmit: (data: GroupFormData) => Promise<void>;
  title: string;
  defaultValues?: GroupFormData;
  isLoading?: boolean;
  isEdit?: boolean;
}

function GroupFormModal({ isOpen, onClose, onSubmit, title, defaultValues, isLoading, isEdit }: GroupFormModalProps) {
  const { register, handleSubmit, formState: { errors }, reset } = useForm<GroupFormData>({
    resolver: zodResolver(groupSchema),
    defaultValues,
    values: defaultValues,
  });

  const handleClose = () => { reset(); onClose(); };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title={title} size="lg">
      <Form onSubmit={handleSubmit(onSubmit)}>
        <FormField label="Name" htmlFor="group-name" error={errors.name?.message} required>
          <FormInput id="group-name" {...register('name')} error={!!errors.name} placeholder="Group name" disabled={isEdit} />
        </FormField>
        <FormField label="Description" htmlFor="group-desc" error={errors.description?.message}>
          <FormTextArea id="group-desc" {...register('description')} error={!!errors.description} placeholder="Group description" />
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
/* Group Members Modal                                                         */
/* -------------------------------------------------------------------------- */

function GroupMembersModal({ group, onClose }: { group: Group; onClose: () => void }) {
  const [showAdd, setShowAdd] = useState(false);

  const { data: membersData, isLoading } = useListQuery<GroupMembership>(['group-memberships', group.id], `/groups/${group.id}/memberships`);
  const addMemberMutation = useCreateMutation<MemberFormData>(`/groups/${group.id}/memberships`, ['group-memberships']);
  const removeMemberMutation = useDeactivateMutation((id) => `/groups/${group.id}/memberships/${id}`, ['group-memberships']);

  const members = membersData?.items ?? [];

  const memberColumns: Column<GroupMembership>[] = [
    { key: 'userDisplayName', header: 'Name' },
    { key: 'userEmail', header: 'Email' },
    { key: 'createdAt', header: 'Added', render: (row) => new Date(row.createdAt).toLocaleDateString() },
    {
      key: 'actions',
      header: '',
      sortable: false,
      render: (row) => (
        <button type="button" className="text-sm text-red-600 hover:underline" onClick={() => removeMemberMutation.mutate(row.id)}>Remove</button>
      ),
    },
  ];

  const { register, handleSubmit, formState: { errors }, reset } = useForm<MemberFormData>({
    resolver: zodResolver(memberSchema),
  });

  return (
    <Modal isOpen onClose={onClose} title={`Members — ${group.name}`} size="xl">
      <div className="space-y-4">
        <div className="flex justify-end">
          <button type="button" className="rounded-md bg-heimdall-primary px-3 py-1.5 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowAdd(!showAdd)}>
            {showAdd ? 'Cancel' : 'Add Member'}
          </button>
        </div>

        {showAdd && (
          <Form onSubmit={handleSubmit(async (d) => { await addMemberMutation.mutateAsync(d); reset(); setShowAdd(false); })}>
            <div className="flex gap-2 items-end">
              <div className="flex-1">
                <FormField label="User Profile ID" htmlFor="member-user" error={errors.userProfileId?.message} required>
                  <FormInput id="member-user" {...register('userProfileId')} error={!!errors.userProfileId} placeholder="User profile ID" />
                </FormField>
              </div>
              <button type="submit" disabled={addMemberMutation.isPending} className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors disabled:opacity-50">
                {addMemberMutation.isPending ? 'Adding...' : 'Add'}
              </button>
            </div>
          </Form>
        )}

        <DataTable columns={memberColumns} data={members} keyField="id" searchPlaceholder="Search members..." isLoading={isLoading} emptyMessage="No members in this group." />
      </div>
    </Modal>
  );
}
