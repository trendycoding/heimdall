import axios from 'axios';
import { PublicClientApplication } from '@azure/msal-browser';
import { msalConfig, apiTokenRequest } from '../auth/msalConfig';

const isMockAuth = import.meta.env.VITE_AUTH_MOCK === 'true';

const msalInstance = isMockAuth ? null : new PublicClientApplication(msalConfig);

/**
 * Axios instance pre-configured with base URL and bearer token interceptor.
 */
const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  headers: {
    'Content-Type': 'application/json',
  },
});

/**
 * Request interceptor: attaches bearer token and active tenant ID.
 */
apiClient.interceptors.request.use(
  async (config) => {
    // Attach bearer token
    if (isMockAuth) {
      config.headers.Authorization = 'Bearer mock-token';
    } else if (msalInstance) {
      const accounts = msalInstance.getAllAccounts();
      if (accounts.length > 0) {
        try {
          const response = await msalInstance.acquireTokenSilent({
            ...apiTokenRequest,
            account: accounts[0],
          });
          config.headers.Authorization = `Bearer ${response.accessToken}`;
        } catch {
          console.warn('Failed to acquire token silently for API request.');
        }
      }
    }

    // Attach active tenant ID from session storage (for tenant-scoped requests)
    const activeTenantId = sessionStorage.getItem('heimdall_active_tenant_id');
    if (activeTenantId) {
      config.headers['X-Tenant-Id'] = activeTenantId;
    }

    return config;
  },
  (error) => Promise.reject(error)
);

/**
 * Response interceptor: mock API responses in mock auth mode, handle errors otherwise.
 */
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    // In mock mode, intercept /me/tenants 404s and return mock data
    if (isMockAuth && error.response?.status === 404) {
      const url = error.config?.url ?? '';
      if (url.includes('/me/tenants')) {
        return Promise.resolve({
          data: {
            success: true,
            data: [{
              tenantId: '00000000-0000-0000-0000-000000000001',
              tenantName: 'Local Dev Tenant',
              slug: 'local-dev',
              role: 'Owner',
              membershipStatus: 'Active',
              tenantStatus: 'Active',
              memberSince: new Date().toISOString(),
            }],
            errors: null,
            correlationId: 'mock-correlation-id',
          },
          status: 200,
          statusText: 'OK',
          headers: {},
          config: error.config,
        });
      }
    }

    if (error.response?.status === 401 && !isMockAuth) {
      window.location.href = '/';
    }
    return Promise.reject(error);
  }
);

export default apiClient;
