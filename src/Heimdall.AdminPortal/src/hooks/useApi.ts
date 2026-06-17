import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '../services/apiClient';
import type { ApiEnvelope, PaginatedResult } from '../types';

/**
 * Generic hook for paginated list queries.
 */
export function useListQuery<T>(
  key: string[],
  url: string,
  params?: Record<string, string | number | boolean | undefined>
) {
  return useQuery({
    queryKey: [...key, params],
    queryFn: async () => {
      const { data } = await apiClient.get<ApiEnvelope<PaginatedResult<T>>>(url, { params });
      return data.data;
    },
  });
}

/**
 * Generic hook for single entity queries.
 */
export function useDetailQuery<T>(key: string[], url: string, enabled = true) {
  return useQuery({
    queryKey: key,
    queryFn: async () => {
      const { data } = await apiClient.get<ApiEnvelope<T>>(url);
      return data.data;
    },
    enabled,
  });
}

/**
 * Generic hook for create mutations.
 */
export function useCreateMutation<TInput, TResult = unknown>(
  url: string,
  invalidateKeys: string[]
) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (input: TInput) => {
      const { data } = await apiClient.post<ApiEnvelope<TResult>>(url, input);
      return data.data;
    },
    onSuccess: () => {
      invalidateKeys.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: [key] });
      });
    },
  });
}

/**
 * Generic hook for update mutations.
 */
export function useUpdateMutation<TInput, TResult = unknown>(
  urlFn: (id: string) => string,
  invalidateKeys: string[]
) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ id, input }: { id: string; input: TInput }) => {
      const { data } = await apiClient.put<ApiEnvelope<TResult>>(urlFn(id), input);
      return data.data;
    },
    onSuccess: () => {
      invalidateKeys.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: [key] });
      });
    },
  });
}

/**
 * Generic hook for deactivate (soft delete) mutations.
 */
export function useDeactivateMutation(
  urlFn: (id: string) => string,
  invalidateKeys: string[]
) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      const { data } = await apiClient.delete<ApiEnvelope<void>>(urlFn(id));
      return data;
    },
    onSuccess: () => {
      invalidateKeys.forEach((key) => {
        queryClient.invalidateQueries({ queryKey: [key] });
      });
    },
  });
}
