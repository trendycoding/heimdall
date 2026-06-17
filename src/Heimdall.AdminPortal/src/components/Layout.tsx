import { useState, useEffect } from 'react';
import { Outlet } from 'react-router-dom';
import { useAuth } from '../auth/AuthProvider';
import { useProductName } from '../hooks/useProductName';
import { Sidebar } from './Sidebar';

/**
 * App shell with header, sidebar navigation, and main content area.
 * Displays the configured product name (from API) and user info in the header.
 * Responsive: sidebar collapses behind a hamburger menu on mobile.
 * Requirements: 24.1, 28.2
 */
export function Layout() {
  const { isAuthenticated, login, logout, account } = useAuth();
  const { productName } = useProductName();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  // Update document title when product name changes
  useEffect(() => {
    document.title = `${productName} - Admin Portal`;
  }, [productName]);

  if (!isAuthenticated) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-heimdall-light px-4">
        <div className="w-full max-w-md rounded-lg bg-white p-8 shadow-lg">
          <h1 className="mb-2 text-center text-2xl font-bold text-heimdall-dark">
            {productName}
          </h1>
          <p className="mb-6 text-center text-gray-600">Admin Portal</p>
          <button
            type="button"
            onClick={login}
            className="w-full rounded-md bg-heimdall-primary px-4 py-2 text-white hover:bg-heimdall-secondary transition-colors"
          >
            Sign in with Azure AD
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="flex h-screen flex-col overflow-hidden">
      {/* Header */}
      <header className="flex-shrink-0 border-b bg-heimdall-dark px-4 py-3 text-white sm:px-6">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            {/* Mobile hamburger */}
            <button
              type="button"
              onClick={() => setSidebarOpen(!sidebarOpen)}
              className="rounded-md p-1 text-gray-300 hover:bg-gray-700 transition-colors lg:hidden"
              aria-label="Toggle navigation"
            >
              <svg className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                {sidebarOpen ? (
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                ) : (
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 6h16M4 12h16M4 18h16" />
                )}
              </svg>
            </button>
            <h1 className="text-lg font-semibold">{productName}</h1>
          </div>
          <div className="flex items-center gap-4">
            <span className="hidden text-sm text-gray-300 sm:inline">
              {account?.name ?? account?.username}
            </span>
            <button
              type="button"
              onClick={logout}
              className="rounded-md border border-gray-500 px-3 py-1 text-sm text-gray-300 hover:bg-gray-700 transition-colors"
            >
              Sign out
            </button>
          </div>
        </div>
      </header>

      {/* Body: sidebar + content */}
      <div className="flex flex-1 overflow-hidden">
        {/* Mobile sidebar overlay */}
        {sidebarOpen && (
          <div
            className="fixed inset-0 z-30 bg-black/50 lg:hidden"
            onClick={() => setSidebarOpen(false)}
          />
        )}

        {/* Sidebar: always visible on lg+, slide-in on mobile */}
        <div
          className={`fixed inset-y-0 left-0 z-40 w-64 transform transition-transform duration-200 ease-in-out lg:relative lg:translate-x-0 lg:z-auto ${
            sidebarOpen ? 'translate-x-0' : '-translate-x-full'
          }`}
        >
          <div className="h-full pt-14 lg:pt-0">
            <Sidebar onNavigate={() => setSidebarOpen(false)} />
          </div>
        </div>

        <main className="flex-1 overflow-y-auto bg-heimdall-light p-4 sm:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
