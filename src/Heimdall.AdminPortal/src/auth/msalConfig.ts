import { Configuration, LogLevel } from '@azure/msal-browser';

/**
 * MSAL configuration for Azure Entra ID authentication.
 * Replace placeholder values with actual Azure AD app registration details.
 */
export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_MSAL_CLIENT_ID || 'YOUR_CLIENT_ID',
    authority:
      import.meta.env.VITE_MSAL_AUTHORITY ||
      'https://login.microsoftonline.com/YOUR_TENANT_ID',
    redirectUri: import.meta.env.VITE_MSAL_REDIRECT_URI || 'http://localhost:3000',
    postLogoutRedirectUri:
      import.meta.env.VITE_MSAL_POST_LOGOUT_REDIRECT_URI || 'http://localhost:3000',
  },
  cache: {
    cacheLocation: 'sessionStorage',
    storeAuthStateInCookie: false,
  },
  system: {
    loggerOptions: {
      loggerCallback: (level, message, containsPii) => {
        if (containsPii) return;
        switch (level) {
          case LogLevel.Error:
            console.error(message);
            break;
          case LogLevel.Warning:
            console.warn(message);
            break;
          case LogLevel.Info:
            console.info(message);
            break;
          case LogLevel.Verbose:
            console.debug(message);
            break;
        }
      },
      logLevel: LogLevel.Warning,
    },
  },
};

/**
 * Scopes requested during login.
 */
export const loginRequest = {
  scopes: [
    import.meta.env.VITE_MSAL_API_SCOPE || 'api://YOUR_CLIENT_ID/.default',
  ],
};

/**
 * Scopes for acquiring tokens silently for API calls.
 */
export const apiTokenRequest = {
  scopes: [
    import.meta.env.VITE_MSAL_API_SCOPE || 'api://YOUR_CLIENT_ID/.default',
  ],
};
