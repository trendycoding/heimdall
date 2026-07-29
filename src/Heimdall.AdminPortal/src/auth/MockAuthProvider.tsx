import React, { createContext, useContext, useState, useMemo } from 'react';
import { AccountInfo } from '@azure/msal-browser';

interface AuthContextValue {
  isAuthenticated: boolean;
  account: AccountInfo | null;
  login: () => Promise<void>;
  logout: () => Promise<void>;
  getAccessToken: () => Promise<string>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

const mockAccount: AccountInfo = {
  homeAccountId: 'mock-home-account-id',
  localAccountId: 'mock-local-account-id',
  environment: 'login.microsoftonline.com',
  tenantId: '00000000-0000-0000-0000-000000000000',
  username: 'admin@heimdall.local',
  name: 'Local Admin',
};

/**
 * A mock auth provider that simulates a logged-in user without hitting Azure AD.
 * Activated by setting VITE_AUTH_MOCK=true in .env.
 */
export function MockAuthProvider({ children }: { children: React.ReactNode }) {
  const [isAuthenticated, setIsAuthenticated] = useState(true);

  const login = async () => {
    setIsAuthenticated(true);
  };

  const logout = async () => {
    setIsAuthenticated(false);
  };

  const getAccessToken = async (): Promise<string> => {
    // Return a fake JWT-shaped token for local dev
    return 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJtb2NrLXVzZXIiLCJuYW1lIjoiTG9jYWwgQWRtaW4iLCJlbWFpbCI6ImFkbWluQGhlaW1kYWxsLmxvY2FsIiwidGlkIjoiMDAwMDAwMDAtMDAwMC0wMDAwLTAwMDAtMDAwMDAwMDAwMDAwIiwiaWF0IjoxNjE2MjM5MDIyfQ.mock-signature';
  };

  const value = useMemo(
    () => ({
      isAuthenticated,
      account: isAuthenticated ? mockAccount : null,
      login,
      logout,
      getAccessToken,
    }),
    [isAuthenticated]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useMockAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useMockAuth must be used within a MockAuthProvider');
  }
  return context;
}
