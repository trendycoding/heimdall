import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useMutation } from '@tanstack/react-query';
import apiClient from '../services/apiClient';
import { useAuth } from '../auth/AuthProvider';
import { useTenant } from '../auth/TenantProvider';
import type { ApiEnvelope, RegisterTenantResult } from '../types';

const registerSchema = z.object({
  name: z.string().min(1, 'Organization name is required').max(128, 'Max 128 characters'),
  slug: z.string()
    .min(1, 'Slug is required')
    .max(64, 'Max 64 characters')
    .regex(/^[a-z0-9]+(?:-[a-z0-9]+)*$/, 'Lowercase letters, numbers, and hyphens only'),
  primaryIdentityMode: z.string().min(1, 'Identity mode is required'),
});

type RegisterFormData = z.infer<typeof registerSchema>;

const identityModeOptions = [
  { value: 'EntraExternalId', label: 'Entra External ID', description: 'Azure AD B2C / Entra External ID' },
  { value: 'EntraWorkforce', label: 'Entra Workforce', description: 'Azure AD for employees' },
  { value: 'ExternalOidc', label: 'External OIDC', description: 'Any OpenID Connect provider' },
  { value: 'ExternalSaml', label: 'SAML IdP', description: 'Enterprise SAML federation' },
];

export function OnboardingPage() {
  const { account, logout } = useAuth();
  const { refetchTenants } = useTenant();
  const [error, setError] = useState<string | null>(null);

  const { register, handleSubmit, watch, setValue, formState: { errors } } = useForm<RegisterFormData>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      name: '',
      slug: '',
      primaryIdentityMode: 'EntraExternalId',
    },
  });

  const name = watch('name');

  // Auto-generate slug from name
  const generateSlug = (value: string) => {
    return value
      .toLowerCase()
      .replace(/[^a-z0-9\s-]/g, '')
      .replace(/\s+/g, '-')
      .replace(/-+/g, '-')
      .replace(/^-|-$/g, '')
      .slice(0, 64);
  };

  const handleNameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const newName = e.target.value;
    setValue('name', newName);
    setValue('slug', generateSlug(newName));
  };

  const registerMutation = useMutation({
    mutationFn: async (data: RegisterFormData) => {
      const response = await apiClient.post<ApiEnvelope<RegisterTenantResult>>('/me/register-tenant', {
        name: data.name,
        slug: data.slug,
        primaryIdentityMode: data.primaryIdentityMode,
      });
      return response.data.data;
    },
    onSuccess: () => {
      refetchTenants();
    },
    onError: (err: unknown) => {
      const axiosError = err as { response?: { data?: { errors?: { message: string }[] } } };
      const message = axiosError.response?.data?.errors?.[0]?.message
        ?? 'Failed to create organization. Please try again.';
      setError(message);
    },
  });

  const onSubmit = (data: RegisterFormData) => {
    setError(null);
    registerMutation.mutate(data);
  };

  return (
    <div className="flex min-h-screen items-center justify-center bg-heimdall-light px-4">
      <div className="w-full max-w-lg">
        <div className="rounded-lg bg-white p-8 shadow-lg">
          <div className="mb-6 text-center">
            <h1 className="text-2xl font-bold text-heimdall-dark">Create your organization</h1>
            <p className="mt-2 text-gray-600">
              Set up your workspace to manage applications, users, and permissions.
            </p>
          </div>

          {error && (
            <div className="mb-4 rounded-md bg-red-50 p-3 text-sm text-red-700" role="alert">
              {error}
            </div>
          )}

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            <div>
              <label htmlFor="name" className="block text-sm font-medium text-gray-700">
                Organization name
              </label>
              <input
                id="name"
                type="text"
                {...register('name')}
                onChange={handleNameChange}
                placeholder="Acme Corp"
                className={`mt-1 block w-full rounded-md border px-3 py-2 shadow-sm focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary ${
                  errors.name ? 'border-red-300' : 'border-gray-300'
                }`}
              />
              {errors.name && <p className="mt-1 text-xs text-red-600">{errors.name.message}</p>}
            </div>

            <div>
              <label htmlFor="slug" className="block text-sm font-medium text-gray-700">
                URL slug
              </label>
              <div className="mt-1 flex rounded-md shadow-sm">
                <span className="inline-flex items-center rounded-l-md border border-r-0 border-gray-300 bg-gray-50 px-3 text-sm text-gray-500">
                  heimdall.io/
                </span>
                <input
                  id="slug"
                  type="text"
                  {...register('slug')}
                  placeholder="acme-corp"
                  className={`block w-full rounded-r-md border px-3 py-2 focus:border-heimdall-primary focus:outline-none focus:ring-1 focus:ring-heimdall-primary ${
                    errors.slug ? 'border-red-300' : 'border-gray-300'
                  }`}
                />
              </div>
              {errors.slug && <p className="mt-1 text-xs text-red-600">{errors.slug.message}</p>}
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Primary identity provider
              </label>
              <div className="space-y-2">
                {identityModeOptions.map((option) => (
                  <label
                    key={option.value}
                    className={`flex cursor-pointer items-start gap-3 rounded-md border p-3 transition-colors ${
                      watch('primaryIdentityMode') === option.value
                        ? 'border-heimdall-primary bg-heimdall-primary/5'
                        : 'border-gray-200 hover:border-gray-300'
                    }`}
                  >
                    <input
                      type="radio"
                      value={option.value}
                      {...register('primaryIdentityMode')}
                      className="mt-0.5"
                    />
                    <div>
                      <span className="text-sm font-medium text-gray-900">{option.label}</span>
                      <p className="text-xs text-gray-500">{option.description}</p>
                    </div>
                  </label>
                ))}
              </div>
              {errors.primaryIdentityMode && (
                <p className="mt-1 text-xs text-red-600">{errors.primaryIdentityMode.message}</p>
              )}
            </div>

            <button
              type="submit"
              disabled={registerMutation.isPending}
              className="w-full rounded-md bg-heimdall-primary px-4 py-2 text-white hover:bg-heimdall-secondary transition-colors disabled:opacity-50"
            >
              {registerMutation.isPending ? 'Creating...' : 'Create organization'}
            </button>
          </form>

          <div className="mt-6 border-t pt-4 text-center">
            <p className="text-sm text-gray-500">
              Signed in as <span className="font-medium">{account?.name ?? account?.username}</span>
            </p>
            <button
              type="button"
              onClick={logout}
              className="mt-1 text-sm text-heimdall-primary hover:underline"
            >
              Sign out
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
