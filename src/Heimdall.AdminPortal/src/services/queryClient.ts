import { QueryClient } from '@tanstack/react-query';

/**
 * TanStack Query client configured with sensible defaults for the Admin Portal.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000, // 30 seconds before data is considered stale
      gcTime: 5 * 60_000, // 5 minutes garbage collection time
      retry: 1,
      refetchOnWindowFocus: false,
    },
    mutations: {
      retry: 0,
    },
  },
});
