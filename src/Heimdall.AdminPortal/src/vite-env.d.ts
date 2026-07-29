/// <reference types="vite/client" />

declare const __APP_VERSION__: string;

interface ImportMetaEnv {
  readonly VITE_MSAL_CLIENT_ID: string;
  readonly VITE_MSAL_AUTHORITY: string;
  readonly VITE_MSAL_REDIRECT_URI: string;
  readonly VITE_MSAL_POST_LOGOUT_REDIRECT_URI: string;
  readonly VITE_MSAL_API_SCOPE: string;
  readonly VITE_API_BASE_URL: string;
  readonly VITE_AUTH_MOCK: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
