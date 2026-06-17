import React, { createContext, useContext, useMemo } from 'react';
import {
  MsalProvider,
  useMsal,
  useIsAuthenticated,
} from '@azure/msal-react';
import {
  PublicClientApplication,
  InteractionRequiredAuthError,
  AccountInfo,
} from '@azure/msal-browser';
import { msalConfig, loginRequest, apiTokenRequest } from './msalConfig';

const msalInstance = new PublicClientApplication(msalConfig);

interface AuthContextValue {
  isAuthenticated: boolean;
  account: AccountInfo | null;
  login: () => Promise<void>;
  logout: () => Promise<void>;
  getAccessToken: () => Promise<string>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function AuthContextProvider({ children }: { children: React.ReactNode }) {
  const { instance, accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();

  const account = accounts[0] ?? null;

  const login = async () => {
    await instance.loginPopup(loginRequest);
  };

  const logout = async () => {
    await instance.logoutPopup({
      postLogoutRedirectUri: msalConfig.auth.postLogoutRedirectUri,
    });
  };

  const getAccessToken = async (): Promise<string> => {
    if (!account) {
      throw new Error('No active account. Please login first.');
    }

    try {
      const response = await instance.acquireTokenSilent({
        ...apiTokenRequest,
        account,
      });
      return response.accessToken;
    } catch (error) {
      if (error instanceof InteractionRequiredAuthError) {
        const response = await instance.acquireTokenPopup(apiTokenRequest);
        return response.accessToken;
      }
      throw error;
    }
  };

  const value = useMemo(
    () => ({ isAuthenticated, account, login, logout, getAccessToken }),
    [isAuthenticated, account]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  return (
    <MsalProvider instance={msalInstance}>
      <AuthContextProvider>{children}</AuthContextProvider>
    </MsalProvider>
  );
}
