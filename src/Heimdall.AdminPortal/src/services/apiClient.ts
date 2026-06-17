import axios from 'axios';
import { PublicClientApplication } from '@azure/msal-browser';
import { msalConfig, apiTokenRequest } from '../auth/msalConfig';

const msalInstance = new PublicClientApplication(msalConfig);

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
 * Request interceptor: attaches bearer token from MSAL to every outgoing request.
 */
apiClient.interceptors.request.use(
  async (config) => {
    const accounts = msalInstance.getAllAccounts();
    if (accounts.length > 0) {
      try {
        const response = await msalInstance.acquireTokenSilent({
          ...apiTokenRequest,
          account: accounts[0],
        });
        config.headers.Authorization = `Bearer ${response.accessToken}`;
      } catch {
        // If silent acquisition fails, the auth provider will handle interactive login
        console.warn('Failed to acquire token silently for API request.');
      }
    }
    return config;
  },
  (error) => Promise.reject(error)
);

/**
 * Response interceptor: handles common API error patterns.
 */
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      // Token expired or invalid — redirect to login
      window.location.href = '/';
    }
    return Promise.reject(error);
  }
);

export default apiClient;
