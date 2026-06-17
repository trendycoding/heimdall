import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  DataTable,
  Column,
  StatusBadge,
  Modal,
  ConfirmDialog,
  Pagination,
  Form,
  FormField,
  FormInput,
  FormTextArea,
  FormActions,
} from '../components';
import { useListQuery } from '../hooks/useApi';
import apiClient from '../services/apiClient';
import type {
  Application,
  Permission,
  Group,
  UserProfile,
  PermissionTemplate,
  TemplatePreviewResult,
  TemplateApplicationHistory,
  ApiEnvelope,
} from '../types';

/* -------------------------------------------------------------------------- */
/* Wizard Types & Constants                                                    */
/* -------------------------------------------------------------------------- */

type WizardStep = 'selectApp' | 'templateInfo' | 'permissions' | 'groups' | 'accessDetails' | 'preview' | 'confirm';

const WIZARD_STEPS: { key: WizardStep; label: string }[] = [
  { key: 'selectApp', label: 'Application' },
  { key: 'templateInfo', label: 'Template Info' },
  { key: 'permissions', label: 'Permissions' },
  { key: 'groups', label: 'Groups' },
  { key: 'accessDetails', label: 'Access Details' },
  { key: 'preview', label: 'Preview' },
  { key: 'confirm', label: 'Confirm' },
];

const templateInfoSchema = z.object({
  templateCode: z.string().min(1, 'Code is required').max(100, 'Max 100 chars')
    .regex(/^[A-Z0-9_]+$/, 'Must be uppercase alphanumeric with underscores'),
  name: z.string().min(1, 'Name is required').max(200, 'Max 200 chars'),
  description: z.string().max(1000, 'Max 1000 chars').optional().default(''),
});

type TemplateInfoFormData = z.infer<typeof templateInfoSchema>;

interface WizardPermission {
  permissionId: string;
  permissionCode: string;
  permissionName: string;
  effect: 'Allow' | 'Deny';
  validFromOffsetDays: number | null;
  validToOffsetDays: number | null;
}

interface WizardGroup {
  groupId: string;
  groupName: string;
}

interface WizardAccessDetail {
  accessDetailType: string;
  accessDetailCode: string;
  accessDetailValue: string;
  description: string;
  validFromOffsetDays: number | null;
  validToOffsetDays: number | null;
}

interface WizardState {
  applicationId: string;
  templateInfo: TemplateInfoFormData | null;
  permissions: WizardPermission[];
  groups: WizardGroup[];
  accessDetails: WizardAccessDetail[];
}

/* -------------------------------------------------------------------------- */
/* Main Page Component                                                         */
/* -------------------------------------------------------------------------- */

export function TemplateBuilderPage() {
  const [showWizard, setShowWizard] = useState(false);
  const [duplicatingTemplate, setDuplicatingTemplate] = useState<PermissionTemplate | null>(null);
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null);
  const [applyingTemplate, setApplyingTemplate] = useState<PermissionTemplate | null>(null);
  const [viewingHistory, setViewingHistory] = useState<PermissionTemplate | null>(null);
  const [search, setSearch] = useState('');

  const queryClient = useQueryClient();
  const { data, isLoading } = useListQuery<PermissionTemplate>(
    ['permission-templates'], '/permission-templates', { search }
  );

  const deactivateMutation = useMutation({
    mutationFn: async (id: string) => {
      await apiClient.delete(`/permission-templates/${id}`);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['permission-templates'] });
    },
  });

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
          <button type="button" className="text-sm text-heimdall-primary hover:underline"
            onClick={() => setDuplicatingTemplate(row)}>Duplicate</button>
          <button type="button" className="text-sm text-heimdall-primary hover:underline"
            onClick={() => setApplyingTemplate(row)}>Apply</button>
          <button type="button" className="text-sm text-heimdall-primary hover:underline"
            onClick={() => setViewingHistory(row)}>History</button>
          {row.isActive && (
            <button type="button" className="text-sm text-red-600 hover:underline"
              onClick={() => setDeactivatingId(row.id)}>Deactivate</button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-heimdall-dark">Template Builder</h2>
        <button type="button" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary transition-colors" onClick={() => setShowWizard(true)}>
          Create Template
        </button>
      </div>

      <DataTable columns={columns} data={items} keyField="id" searchPlaceholder="Search templates..." onSearch={setSearch} isLoading={isLoading} />

      {showWizard && <TemplateWizardModal onClose={() => setShowWizard(false)} />}
      {duplicatingTemplate && <DuplicateTemplateModal template={duplicatingTemplate} onClose={() => setDuplicatingTemplate(null)} />}
      {applyingTemplate && <ApplyTemplateModal template={applyingTemplate} onClose={() => setApplyingTemplate(null)} />}
      {viewingHistory && <TemplateHistoryModal template={viewingHistory} onClose={() => setViewingHistory(null)} />}

      <ConfirmDialog
        isOpen={!!deactivatingId}
        onClose={() => setDeactivatingId(null)}
        onConfirm={async () => { if (deactivatingId) { await deactivateMutation.mutateAsync(deactivatingId); setDeactivatingId(null); } }}
        title="Deactivate Template"
        message="Are you sure? This template will no longer be available for application."
        confirmLabel="Deactivate"
        variant="danger"
        isLoading={deactivateMutation.isPending}
      />
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Template Wizard Modal                                                       */
/* -------------------------------------------------------------------------- */

function TemplateWizardModal({ onClose }: { onClose: () => void }) {
  const [step, setStep] = useState<WizardStep>('selectApp');
  const [wizardState, setWizardState] = useState<WizardState>({
    applicationId: '',
    templateInfo: null,
    permissions: [],
    groups: [],
    accessDetails: [],
  });
  const [isSubmitting, setIsSubmitting] = useState(false);
  const queryClient = useQueryClient();

  const currentStepIndex = WIZARD_STEPS.findIndex((s) => s.key === step);
  const goNext = () => { const i = currentStepIndex + 1; if (i < WIZARD_STEPS.length) setStep(WIZARD_STEPS[i].key); };
  const goBack = () => { const i = currentStepIndex - 1; if (i >= 0) setStep(WIZARD_STEPS[i].key); };

  const handleSubmit = async () => {
    if (!wizardState.templateInfo) return;
    setIsSubmitting(true);
    try {
      await apiClient.post('/permission-templates', {
        applicationId: wizardState.applicationId,
        templateCode: wizardState.templateInfo.templateCode,
        name: wizardState.templateInfo.name,
        description: wizardState.templateInfo.description,
        permissions: wizardState.permissions.map((p) => ({
          permissionId: p.permissionId,
          effect: p.effect,
          validFromOffsetDays: p.validFromOffsetDays,
          validToOffsetDays: p.validToOffsetDays,
        })),
        groups: wizardState.groups.map((g) => ({ groupId: g.groupId })),
        accessDetails: wizardState.accessDetails.map((a) => ({
          accessDetailType: a.accessDetailType,
          accessDetailCode: a.accessDetailCode,
          accessDetailValue: a.accessDetailValue,
          description: a.description,
          validFromOffsetDays: a.validFromOffsetDays,
          validToOffsetDays: a.validToOffsetDays,
        })),
      });
      queryClient.invalidateQueries({ queryKey: ['permission-templates'] });
      setStep('confirm');
    } catch { /* handled by interceptor */ } finally { setIsSubmitting(false); }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4" role="dialog" aria-modal="true">
      <div className="w-full max-w-4xl rounded-lg bg-white shadow-xl max-h-[90vh] flex flex-col">
        <div className="flex items-center justify-between border-b px-6 py-4">
          <h2 className="text-lg font-semibold text-heimdall-dark">Template Builder</h2>
          <button type="button" onClick={onClose} className="rounded-md p-1 text-gray-400 hover:bg-gray-100" aria-label="Close">
            <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" /></svg>
          </button>
        </div>
        {/* Step Indicator */}
        <div className="border-b px-6 py-3">
          <div className="flex items-center gap-1">
            {WIZARD_STEPS.map((s, idx) => (
              <div key={s.key} className="flex items-center">
                <div className={`flex h-6 w-6 items-center justify-center rounded-full text-xs font-medium ${idx < currentStepIndex ? 'bg-green-100 text-green-700' : idx === currentStepIndex ? 'bg-heimdall-primary text-white' : 'bg-gray-100 text-gray-400'}`}>
                  {idx < currentStepIndex ? '✓' : idx + 1}
                </div>
                <span className={`ml-1 text-xs hidden md:inline ${idx === currentStepIndex ? 'font-medium text-heimdall-dark' : 'text-gray-400'}`}>{s.label}</span>
                {idx < WIZARD_STEPS.length - 1 && <div className="mx-1 h-px w-3 bg-gray-300" />}
              </div>
            ))}
          </div>
        </div>
        {/* Body */}
        <div className="flex-1 overflow-y-auto px-6 py-4">
          {step === 'selectApp' && <SelectAppStep selectedId={wizardState.applicationId} onSelect={(id) => { setWizardState((s) => ({ ...s, applicationId: id })); goNext(); }} />}
          {step === 'templateInfo' && <TemplateInfoStep defaultValues={wizardState.templateInfo ?? undefined} onSubmit={(d) => { setWizardState((s) => ({ ...s, templateInfo: d })); goNext(); }} onBack={goBack} />}
          {step === 'permissions' && <PermissionsStep applicationId={wizardState.applicationId} selected={wizardState.permissions} onChange={(p) => setWizardState((s) => ({ ...s, permissions: p }))} onNext={goNext} onBack={goBack} />}
          {step === 'groups' && <GroupsStep applicationId={wizardState.applicationId} selected={wizardState.groups} onChange={(g) => setWizardState((s) => ({ ...s, groups: g }))} onNext={goNext} onBack={goBack} />}
          {step === 'accessDetails' && <AccessDetailsStep selected={wizardState.accessDetails} onChange={(d) => setWizardState((s) => ({ ...s, accessDetails: d }))} onNext={goNext} onBack={goBack} />}
          {step === 'preview' && <PreviewStep wizardState={wizardState} onSubmit={handleSubmit} onBack={goBack} isSubmitting={isSubmitting} />}
          {step === 'confirm' && <ConfirmStep onClose={onClose} />}
        </div>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Step: Select Application                                                    */
/* -------------------------------------------------------------------------- */

function SelectAppStep({ selectedId, onSelect }: { selectedId: string; onSelect: (id: string) => void }) {
  const { data, isLoading } = useListQuery<Application>(['applications'], '/applications', {});
  const apps = data?.items ?? [];
  if (isLoading) return <p className="text-sm text-gray-500">Loading applications...</p>;
  return (
    <div className="space-y-3">
      <p className="text-sm text-gray-600">Select the application this template belongs to:</p>
      <div className="grid gap-2 sm:grid-cols-2">
        {apps.map((app) => (
          <button key={app.id} type="button" onClick={() => onSelect(app.id)}
            className={`rounded-md border p-3 text-left transition-colors hover:border-heimdall-primary ${selectedId === app.id ? 'border-heimdall-primary bg-heimdall-primary/5' : 'border-gray-200'}`}>
            <span className="block text-sm font-medium text-heimdall-dark">{app.name}</span>
            <span className="block text-xs text-gray-500">{app.clientIdentifier}</span>
          </button>
        ))}
      </div>
      {apps.length === 0 && <p className="text-sm text-gray-500">No applications found.</p>}
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Step: Template Info                                                         */
/* -------------------------------------------------------------------------- */

function TemplateInfoStep({ defaultValues, onSubmit, onBack }: { defaultValues?: TemplateInfoFormData; onSubmit: (data: TemplateInfoFormData) => void; onBack: () => void }) {
  const { register, handleSubmit, formState: { errors } } = useForm<TemplateInfoFormData>({ resolver: zodResolver(templateInfoSchema), defaultValues });
  return (
    <Form onSubmit={handleSubmit(onSubmit)}>
      <FormField label="Template Code" htmlFor="wiz-code" error={errors.templateCode?.message} required>
        <FormInput id="wiz-code" {...register('templateCode')} error={!!errors.templateCode} placeholder="STANDARD_USER" />
      </FormField>
      <FormField label="Name" htmlFor="wiz-name" error={errors.name?.message} required>
        <FormInput id="wiz-name" {...register('name')} error={!!errors.name} placeholder="Template name" />
      </FormField>
      <FormField label="Description" htmlFor="wiz-desc" error={errors.description?.message}>
        <FormTextArea id="wiz-desc" {...register('description')} error={!!errors.description} placeholder="Optional description" />
      </FormField>
      <FormActions>
        <button type="button" onClick={onBack} className="rounded-md border px-4 py-2 text-sm text-gray-700 hover:bg-gray-50">Back</button>
        <button type="submit" className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary">Next</button>
      </FormActions>
    </Form>
  );
}

/* -------------------------------------------------------------------------- */
/* Step: Permissions                                                           */
/* -------------------------------------------------------------------------- */

function PermissionsStep({ applicationId, selected, onChange, onNext, onBack }: {
  applicationId: string; selected: WizardPermission[];
  onChange: (p: WizardPermission[]) => void; onNext: () => void; onBack: () => void;
}) {
  const { data } = useListQuery<Permission>(['permissions', applicationId], '/permissions', { applicationId });
  const permissions = data?.items ?? [];
  const [adding, setAdding] = useState(false);
  const [newPermId, setNewPermId] = useState('');
  const [newEffect, setNewEffect] = useState<'Allow' | 'Deny'>('Allow');
  const [newFromOffset, setNewFromOffset] = useState('');
  const [newToOffset, setNewToOffset] = useState('');

  const availablePerms = permissions.filter((p) => !selected.some((s) => s.permissionId === p.id));

  const handleAdd = () => {
    const perm = permissions.find((p) => p.id === newPermId);
    if (!perm) return;
    onChange([...selected, {
      permissionId: perm.id, permissionCode: perm.permissionCode, permissionName: perm.name, effect: newEffect,
      validFromOffsetDays: newFromOffset ? parseInt(newFromOffset, 10) : null,
      validToOffsetDays: newToOffset ? parseInt(newToOffset, 10) : null,
    }]);
    setAdding(false); setNewPermId(''); setNewEffect('Allow'); setNewFromOffset(''); setNewToOffset('');
  };

  const handleRemove = (id: string) => onChange(selected.filter((s) => s.permissionId !== id));

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-600">Add permissions to this template:</p>
        <button type="button" onClick={() => setAdding(true)} className="rounded-md bg-heimdall-primary px-3 py-1.5 text-xs text-white hover:bg-heimdall-secondary">Add Permission</button>
      </div>
      {selected.length > 0 && (
        <div className="rounded-md border">
          <table className="w-full text-sm">
            <thead className="bg-gray-50"><tr>
              <th className="px-3 py-2 text-left text-xs font-medium text-gray-500">Code</th>
              <th className="px-3 py-2 text-left text-xs font-medium text-gray-500">Effect</th>
              <th className="px-3 py-2 text-left text-xs font-medium text-gray-500">From Offset</th>
              <th className="px-3 py-2 text-left text-xs font-medium text-gray-500">To Offset</th>
              <th className="px-3 py-2"></th>
            </tr></thead>
            <tbody className="divide-y">
              {selected.map((p) => (
                <tr key={p.permissionId}>
                  <td className="px-3 py-2 font-mono text-xs">{p.permissionCode}</td>
                  <td className="px-3 py-2"><span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${p.effect === 'Allow' ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'}`}>{p.effect}</span></td>
                  <td className="px-3 py-2 text-xs text-gray-500">{p.validFromOffsetDays ?? '—'}</td>
                  <td className="px-3 py-2 text-xs text-gray-500">{p.validToOffsetDays ?? '—'}</td>
                  <td className="px-3 py-2"><button type="button" onClick={() => handleRemove(p.permissionId)} className="text-xs text-red-500 hover:underline">Remove</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {selected.length === 0 && !adding && <p className="text-xs text-gray-400">No permissions added yet. You can skip this step.</p>}

      {adding && (
        <div className="rounded-md border border-heimdall-primary/30 bg-heimdall-primary/5 p-3 space-y-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label htmlFor="perm-select" className="block text-xs font-medium text-gray-700 mb-1">Permission</label>
              <select id="perm-select" value={newPermId} onChange={(e) => setNewPermId(e.target.value)} className="block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm">
                <option value="">Select...</option>
                {availablePerms.map((p) => <option key={p.id} value={p.id}>{p.permissionCode} — {p.name}</option>)}
              </select>
            </div>
            <div>
              <label htmlFor="effect-select" className="block text-xs font-medium text-gray-700 mb-1">Effect</label>
              <select id="effect-select" value={newEffect} onChange={(e) => setNewEffect(e.target.value as 'Allow' | 'Deny')} className="block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm">
                <option value="Allow">Allow</option><option value="Deny">Deny</option>
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">ValidFrom Offset (days)</label>
              <input type="number" value={newFromOffset} onChange={(e) => setNewFromOffset(e.target.value)} className="block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm" placeholder="Optional" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">ValidTo Offset (days)</label>
              <input type="number" value={newToOffset} onChange={(e) => setNewToOffset(e.target.value)} className="block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm" placeholder="Optional" />
            </div>
          </div>
          <div className="flex gap-2">
            <button type="button" onClick={handleAdd} disabled={!newPermId} className="rounded-md bg-heimdall-primary px-3 py-1 text-xs text-white hover:bg-heimdall-secondary disabled:opacity-50">Add</button>
            <button type="button" onClick={() => setAdding(false)} className="rounded-md border px-3 py-1 text-xs text-gray-600 hover:bg-gray-50">Cancel</button>
          </div>
        </div>
      )}
      <div className="flex justify-between pt-4">
        <button type="button" onClick={onBack} className="rounded-md border px-4 py-2 text-sm text-gray-700 hover:bg-gray-50">Back</button>
        <button type="button" onClick={onNext} className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary">Next</button>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Step: Groups                                                                */
/* -------------------------------------------------------------------------- */

function GroupsStep({ applicationId, selected, onChange, onNext, onBack }: {
  applicationId: string; selected: WizardGroup[];
  onChange: (g: WizardGroup[]) => void; onNext: () => void; onBack: () => void;
}) {
  const { data } = useListQuery<Group>(['groups', applicationId], '/groups', { applicationId });
  const groups = data?.items ?? [];
  const available = groups.filter((g) => !selected.some((s) => s.groupId === g.id));

  return (
    <div className="space-y-4">
      <p className="text-sm text-gray-600">Add groups to this template:</p>
      {selected.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {selected.map((g) => (
            <span key={g.groupId} className="inline-flex items-center gap-1 rounded-full bg-blue-100 px-3 py-1 text-xs font-medium text-blue-700">
              {g.groupName}
              <button type="button" onClick={() => onChange(selected.filter((s) => s.groupId !== g.groupId))} className="ml-1 text-blue-500 hover:text-blue-800">×</button>
            </span>
          ))}
        </div>
      )}
      {available.length > 0 && (
        <div className="rounded-md border divide-y max-h-48 overflow-y-auto">
          {available.map((g) => (
            <div key={g.id} className="flex items-center justify-between px-3 py-2">
              <span className="text-sm">{g.name}</span>
              <button type="button" onClick={() => onChange([...selected, { groupId: g.id, groupName: g.name }])} className="text-xs text-heimdall-primary hover:underline">Add</button>
            </div>
          ))}
        </div>
      )}
      {selected.length === 0 && available.length === 0 && <p className="text-xs text-gray-400">No groups available.</p>}
      <div className="flex justify-between pt-4">
        <button type="button" onClick={onBack} className="rounded-md border px-4 py-2 text-sm text-gray-700 hover:bg-gray-50">Back</button>
        <button type="button" onClick={onNext} className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary">Next</button>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Step: Access Details                                                        */
/* -------------------------------------------------------------------------- */

function AccessDetailsStep({ selected, onChange, onNext, onBack }: {
  selected: WizardAccessDetail[]; onChange: (d: WizardAccessDetail[]) => void; onNext: () => void; onBack: () => void;
}) {
  const [adding, setAdding] = useState(false);
  const [form, setForm] = useState({ accessDetailType: '', accessDetailCode: '', accessDetailValue: '', description: '', validFromOffsetDays: '', validToOffsetDays: '' });

  const handleAdd = () => {
    if (!form.accessDetailType || !form.accessDetailCode || !form.accessDetailValue) return;
    onChange([...selected, {
      accessDetailType: form.accessDetailType, accessDetailCode: form.accessDetailCode,
      accessDetailValue: form.accessDetailValue, description: form.description,
      validFromOffsetDays: form.validFromOffsetDays ? parseInt(form.validFromOffsetDays, 10) : null,
      validToOffsetDays: form.validToOffsetDays ? parseInt(form.validToOffsetDays, 10) : null,
    }]);
    setForm({ accessDetailType: '', accessDetailCode: '', accessDetailValue: '', description: '', validFromOffsetDays: '', validToOffsetDays: '' });
    setAdding(false);
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-600">Add access details to this template:</p>
        <button type="button" onClick={() => setAdding(true)} className="rounded-md bg-heimdall-primary px-3 py-1.5 text-xs text-white hover:bg-heimdall-secondary">Add Access Detail</button>
      </div>
      {selected.length > 0 && (
        <div className="rounded-md border">
          <table className="w-full text-sm">
            <thead className="bg-gray-50"><tr>
              <th className="px-3 py-2 text-left text-xs font-medium text-gray-500">Type</th>
              <th className="px-3 py-2 text-left text-xs font-medium text-gray-500">Code</th>
              <th className="px-3 py-2 text-left text-xs font-medium text-gray-500">Value</th>
              <th className="px-3 py-2 text-left text-xs font-medium text-gray-500">From</th>
              <th className="px-3 py-2 text-left text-xs font-medium text-gray-500">To</th>
              <th className="px-3 py-2"></th>
            </tr></thead>
            <tbody className="divide-y">
              {selected.map((d, idx) => (
                <tr key={idx}>
                  <td className="px-3 py-2 text-xs">{d.accessDetailType}</td>
                  <td className="px-3 py-2 text-xs">{d.accessDetailCode}</td>
                  <td className="px-3 py-2 text-xs">{d.accessDetailValue}</td>
                  <td className="px-3 py-2 text-xs text-gray-500">{d.validFromOffsetDays ?? '—'}</td>
                  <td className="px-3 py-2 text-xs text-gray-500">{d.validToOffsetDays ?? '—'}</td>
                  <td className="px-3 py-2"><button type="button" onClick={() => onChange(selected.filter((_, i) => i !== idx))} className="text-xs text-red-500 hover:underline">Remove</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {selected.length === 0 && !adding && <p className="text-xs text-gray-400">No access details added yet. You can skip this step.</p>}

      {adding && (
        <div className="rounded-md border border-heimdall-primary/30 bg-heimdall-primary/5 p-3 space-y-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Type</label>
              <input type="text" value={form.accessDetailType} onChange={(e) => setForm({ ...form, accessDetailType: e.target.value })} className="block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm" placeholder="e.g. REGION" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Code</label>
              <input type="text" value={form.accessDetailCode} onChange={(e) => setForm({ ...form, accessDetailCode: e.target.value })} className="block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm" placeholder="e.g. US_EAST" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Value</label>
              <input type="text" value={form.accessDetailValue} onChange={(e) => setForm({ ...form, accessDetailValue: e.target.value })} className="block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm" placeholder="Value" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">Description</label>
              <input type="text" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} className="block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm" placeholder="Optional" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">ValidFrom Offset (days)</label>
              <input type="number" value={form.validFromOffsetDays} onChange={(e) => setForm({ ...form, validFromOffsetDays: e.target.value })} className="block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm" placeholder="Optional" />
            </div>
            <div>
              <label className="block text-xs font-medium text-gray-700 mb-1">ValidTo Offset (days)</label>
              <input type="number" value={form.validToOffsetDays} onChange={(e) => setForm({ ...form, validToOffsetDays: e.target.value })} className="block w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm" placeholder="Optional" />
            </div>
          </div>
          <div className="flex gap-2">
            <button type="button" onClick={handleAdd} disabled={!form.accessDetailType || !form.accessDetailCode || !form.accessDetailValue} className="rounded-md bg-heimdall-primary px-3 py-1 text-xs text-white hover:bg-heimdall-secondary disabled:opacity-50">Add</button>
            <button type="button" onClick={() => setAdding(false)} className="rounded-md border px-3 py-1 text-xs text-gray-600 hover:bg-gray-50">Cancel</button>
          </div>
        </div>
      )}
      <div className="flex justify-between pt-4">
        <button type="button" onClick={onBack} className="rounded-md border px-4 py-2 text-sm text-gray-700 hover:bg-gray-50">Back</button>
        <button type="button" onClick={onNext} className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary">Next</button>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Step: Preview                                                               */
/* -------------------------------------------------------------------------- */

function PreviewStep({ wizardState, onSubmit, onBack, isSubmitting }: { wizardState: WizardState; onSubmit: () => void; onBack: () => void; isSubmitting: boolean }) {
  return (
    <div className="space-y-4">
      <h3 className="text-sm font-semibold text-heimdall-dark">Template Summary</h3>
      <div className="rounded-md border p-3 space-y-2">
        <p className="text-xs text-gray-500">Code: <span className="font-mono font-medium text-heimdall-dark">{wizardState.templateInfo?.templateCode}</span></p>
        <p className="text-xs text-gray-500">Name: <span className="font-medium text-heimdall-dark">{wizardState.templateInfo?.name}</span></p>
        {wizardState.templateInfo?.description && <p className="text-xs text-gray-500">Description: {wizardState.templateInfo.description}</p>}
      </div>
      {wizardState.permissions.length > 0 && (
        <div>
          <h4 className="text-xs font-medium text-gray-700 mb-1">Permissions ({wizardState.permissions.length})</h4>
          <div className="rounded-md border divide-y max-h-32 overflow-y-auto">
            {wizardState.permissions.map((p) => (
              <div key={p.permissionId} className="flex items-center justify-between px-3 py-1.5">
                <span className="text-xs font-mono">{p.permissionCode}</span>
                <span className={`text-xs font-medium ${p.effect === 'Allow' ? 'text-green-600' : 'text-red-600'}`}>{p.effect}</span>
              </div>
            ))}
          </div>
        </div>
      )}
      {wizardState.groups.length > 0 && (
        <div>
          <h4 className="text-xs font-medium text-gray-700 mb-1">Groups ({wizardState.groups.length})</h4>
          <div className="flex flex-wrap gap-1">
            {wizardState.groups.map((g) => <span key={g.groupId} className="inline-flex rounded-full bg-blue-100 px-2 py-0.5 text-xs text-blue-700">{g.groupName}</span>)}
          </div>
        </div>
      )}
      {wizardState.accessDetails.length > 0 && (
        <div>
          <h4 className="text-xs font-medium text-gray-700 mb-1">Access Details ({wizardState.accessDetails.length})</h4>
          <div className="rounded-md border divide-y max-h-32 overflow-y-auto">
            {wizardState.accessDetails.map((d, idx) => <div key={idx} className="px-3 py-1.5 text-xs"><span className="font-medium">{d.accessDetailType}</span>: {d.accessDetailCode} = {d.accessDetailValue}</div>)}
          </div>
        </div>
      )}
      <div className="flex justify-between pt-4">
        <button type="button" onClick={onBack} className="rounded-md border px-4 py-2 text-sm text-gray-700 hover:bg-gray-50">Back</button>
        <button type="button" onClick={onSubmit} disabled={isSubmitting} className="rounded-md bg-green-600 px-4 py-2 text-sm text-white hover:bg-green-700 disabled:opacity-50">
          {isSubmitting ? 'Creating...' : 'Create Template'}
        </button>
      </div>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Step: Confirm                                                               */
/* -------------------------------------------------------------------------- */

function ConfirmStep({ onClose }: { onClose: () => void }) {
  return (
    <div className="flex flex-col items-center justify-center py-8 space-y-4">
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-green-100">
        <svg className="h-8 w-8 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
        </svg>
      </div>
      <h3 className="text-lg font-semibold text-heimdall-dark">Template Created</h3>
      <p className="text-sm text-gray-600">Your template has been created successfully.</p>
      <button type="button" onClick={onClose} className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary">Done</button>
    </div>
  );
}

/* -------------------------------------------------------------------------- */
/* Duplicate Template Modal                                                    */
/* -------------------------------------------------------------------------- */

function DuplicateTemplateModal({ template, onClose }: { template: PermissionTemplate; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [newCode, setNewCode] = useState(`${template.templateCode}_COPY`);
  const [newName, setNewName] = useState(`${template.name} (Copy)`);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState('');

  const handleDuplicate = async () => {
    if (!newCode || !newName) { setError('Code and Name are required'); return; }
    setIsSubmitting(true);
    setError('');
    try {
      const { data: detail } = await apiClient.get<ApiEnvelope<PermissionTemplate>>(`/permission-templates/${template.id}`);
      const src = detail.data;
      await apiClient.post('/permission-templates', {
        applicationId: src.applicationId,
        templateCode: newCode,
        name: newName,
        description: src.description,
        permissions: (src.permissions ?? []).map((p) => ({ permissionId: p.permissionId, effect: p.effect, validFromOffsetDays: p.validFromOffsetDays, validToOffsetDays: p.validToOffsetDays })),
        groups: (src.groups ?? []).map((g) => ({ groupId: g.groupId })),
        accessDetails: (src.accessDetails ?? []).map((a) => ({ accessDetailType: a.accessDetailType, accessDetailCode: a.accessDetailCode, accessDetailValue: a.accessDetailValue, description: a.description, validFromOffsetDays: a.validFromOffsetDays, validToOffsetDays: a.validToOffsetDays })),
      });
      queryClient.invalidateQueries({ queryKey: ['permission-templates'] });
      onClose();
    } catch { setError('Failed to duplicate. The code may already exist.'); } finally { setIsSubmitting(false); }
  };

  return (
    <Modal isOpen onClose={onClose} title="Duplicate Template" size="lg">
      <div className="space-y-4">
        <p className="text-sm text-gray-600">Create a copy of <span className="font-medium">{template.name}</span>.</p>
        <div>
          <label htmlFor="dup-code" className="block text-sm font-medium text-gray-700 mb-1">New Template Code</label>
          <input id="dup-code" type="text" value={newCode} onChange={(e) => setNewCode(e.target.value.toUpperCase())} className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm" />
        </div>
        <div>
          <label htmlFor="dup-name" className="block text-sm font-medium text-gray-700 mb-1">New Name</label>
          <input id="dup-name" type="text" value={newName} onChange={(e) => setNewName(e.target.value)} className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm" />
        </div>
        {error && <p className="text-sm text-red-600">{error}</p>}
        <div className="flex justify-end gap-3 pt-2">
          <button type="button" onClick={onClose} className="rounded-md border px-4 py-2 text-sm text-gray-700 hover:bg-gray-50">Cancel</button>
          <button type="button" onClick={handleDuplicate} disabled={isSubmitting} className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary disabled:opacity-50">
            {isSubmitting ? 'Duplicating...' : 'Duplicate'}
          </button>
        </div>
      </div>
    </Modal>
  );
}

/* -------------------------------------------------------------------------- */
/* Apply Template Modal                                                        */
/* -------------------------------------------------------------------------- */

function ApplyTemplateModal({ template, onClose }: { template: PermissionTemplate; onClose: () => void }) {
  const [selectedUserId, setSelectedUserId] = useState('');
  const [replacePermissions, setReplacePermissions] = useState(false);
  const [replaceGroups, setReplaceGroups] = useState(false);
  const [replaceAccessDetails, setReplaceAccessDetails] = useState(false);
  const [preview, setPreview] = useState<TemplatePreviewResult | null>(null);
  const [isLoadingPreview, setIsLoadingPreview] = useState(false);
  const [isApplying, setIsApplying] = useState(false);
  const [applied, setApplied] = useState(false);
  const [error, setError] = useState('');
  const queryClient = useQueryClient();

  const { data: usersData } = useListQuery<UserProfile>(['users'], '/users', {});
  const users = usersData?.items ?? [];

  const handlePreview = async () => {
    if (!selectedUserId) return;
    setIsLoadingPreview(true); setError('');
    try {
      const { data } = await apiClient.post<ApiEnvelope<TemplatePreviewResult>>(`/permission-templates/${template.id}/preview`, {
        userProfileId: selectedUserId, replacePermissions, replaceGroups, replaceAccessDetails,
      });
      setPreview(data.data);
    } catch { setError('Failed to generate preview.'); } finally { setIsLoadingPreview(false); }
  };

  const handleApply = async () => {
    if (!selectedUserId) return;
    setIsApplying(true); setError('');
    try {
      await apiClient.post(`/permission-templates/${template.id}/apply`, {
        userProfileId: selectedUserId, replacePermissions, replaceGroups, replaceAccessDetails,
      });
      queryClient.invalidateQueries({ queryKey: ['permission-templates'] });
      setApplied(true);
    } catch { setError('Failed to apply template.'); } finally { setIsApplying(false); }
  };

  if (applied) {
    return (
      <Modal isOpen onClose={onClose} title="Template Applied" size="lg">
        <div className="flex flex-col items-center py-6 space-y-3">
          <div className="flex h-12 w-12 items-center justify-center rounded-full bg-green-100">
            <svg className="h-6 w-6 text-green-600" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" /></svg>
          </div>
          <p className="text-sm text-gray-600">Template applied successfully.</p>
          <button type="button" onClick={onClose} className="rounded-md bg-heimdall-primary px-4 py-2 text-sm text-white hover:bg-heimdall-secondary">Done</button>
        </div>
      </Modal>
    );
  }

  return (
    <Modal isOpen onClose={onClose} title={`Apply Template: ${template.name}`} size="xl">
      <div className="space-y-4 max-h-[70vh] overflow-y-auto">
        {/* User Selection */}
        <div>
          <label htmlFor="apply-user-select" className="block text-sm font-medium text-gray-700 mb-1">Select User</label>
          <select id="apply-user-select" value={selectedUserId} onChange={(e) => { setSelectedUserId(e.target.value); setPreview(null); }}
            className="block w-full rounded-md border border-gray-300 px-3 py-2 text-sm">
            <option value="">Choose a user...</option>
            {users.map((u) => <option key={u.id} value={u.id}>{u.displayName} ({u.email})</option>)}
          </select>
        </div>

        {/* Replace Options */}
        <div className="space-y-2">
          <p className="text-sm font-medium text-gray-700">Replace Options</p>
          <label className="flex items-center gap-2 text-sm text-gray-600">
            <input type="checkbox" checked={replacePermissions} onChange={(e) => { setReplacePermissions(e.target.checked); setPreview(null); }} className="rounded border-gray-300 text-heimdall-primary focus:ring-heimdall-primary" />
            Replace existing permissions
          </label>
          <label className="flex items-center gap-2 text-sm text-gray-600">
            <input type="checkbox" checked={replaceGroups} onChange={(e) => { setReplaceGroups(e.target.checked); setPreview(null); }} className="rounded border-gray-300 text-heimdall-primary focus:ring-heimdall-primary" />
            Replace existing group memberships
          </label>
          <label className="flex items-center gap-2 text-sm text-gray-600">
            <input type="checkbox" checked={replaceAccessDetails} onChange={(e) => { setReplaceAccessDetails(e.target.checked); setPreview(null); }} className="rounded border-gray-300 text-heimdall-primary focus:ring-heimdall-primary" />
            Replace existing access details
          </label>
        </div>

        {/* Preview Button */}
        <button type="button" onClick={handlePreview} disabled={!selectedUserId || isLoadingPreview}
          className="rounded-md border border-heimdall-primary px-4 py-2 text-sm text-heimdall-primary hover:bg-heimdall-primary/5 disabled:opacity-50">
          {isLoadingPreview ? 'Loading...' : 'Preview Changes'}
        </button>

        {/* Preview Results */}
        {preview && (
          <div className="rounded-md border p-3 space-y-3 bg-gray-50">
            <h4 className="text-sm font-medium text-heimdall-dark">Preview</h4>
            {preview.directPermissionsToAdd.length > 0 && (
              <div>
                <p className="text-xs font-medium text-gray-600 mb-1">Permissions to Add:</p>
                {preview.directPermissionsToAdd.map((p, i) => (
                  <div key={i} className="flex items-center gap-2 text-xs">
                    <span className="font-mono">{p.permissionCode}</span>
                    <span className={p.effect === 'Allow' ? 'text-green-600' : 'text-red-600'}>{p.effect}</span>
                  </div>
                ))}
              </div>
            )}
            {preview.groupsToAdd.length > 0 && (
              <div>
                <p className="text-xs font-medium text-gray-600 mb-1">Groups to Add:</p>
                <div className="flex flex-wrap gap-1">
                  {preview.groupsToAdd.map((g, i) => <span key={i} className="inline-flex rounded-full bg-blue-100 px-2 py-0.5 text-xs text-blue-700">{g.groupName}</span>)}
                </div>
              </div>
            )}
            {preview.accessDetailsToAdd.length > 0 && (
              <div>
                <p className="text-xs font-medium text-gray-600 mb-1">Access Details to Add:</p>
                {preview.accessDetailsToAdd.map((d, i) => <div key={i} className="text-xs">{d.accessDetailType}: {d.accessDetailCode} = {d.accessDetailValue}</div>)}
              </div>
            )}
            {preview.conflicts.length > 0 && (
              <div>
                <p className="text-xs font-medium text-orange-600 mb-1">Conflicts:</p>
                {preview.conflicts.map((c, i) => <div key={i} className="text-xs text-orange-700">{c.permissionCode}: existing {c.existingEffect} → template {c.templateEffect}</div>)}
              </div>
            )}
            {preview.existingPermissionsUnaffected.length > 0 && (
              <p className="text-xs text-gray-400">{preview.existingPermissionsUnaffected.length} existing permission(s) unaffected</p>
            )}
          </div>
        )}

        {error && <p className="text-sm text-red-600">{error}</p>}

        <div className="flex justify-end gap-3 pt-2">
          <button type="button" onClick={onClose} className="rounded-md border px-4 py-2 text-sm text-gray-700 hover:bg-gray-50">Cancel</button>
          <button type="button" onClick={handleApply} disabled={!selectedUserId || isApplying}
            className="rounded-md bg-green-600 px-4 py-2 text-sm text-white hover:bg-green-700 disabled:opacity-50">
            {isApplying ? 'Applying...' : 'Apply Template'}
          </button>
        </div>
      </div>
    </Modal>
  );
}

/* -------------------------------------------------------------------------- */
/* Template History Modal                                                      */
/* -------------------------------------------------------------------------- */

function TemplateHistoryModal({ template, onClose }: { template: PermissionTemplate; onClose: () => void }) {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useListQuery<TemplateApplicationHistory>(
    ['template-history', template.id], `/permission-templates/${template.id}/history`, { page, pageSize: 20 }
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
