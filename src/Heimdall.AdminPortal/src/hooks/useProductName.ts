import { useQuery } from '@tanstack/react-query';
import apiClient from '../services/apiClient';

interface ProductConfigurationResponse {
  data: {
    displayName: string;
  };
}

const DEFAULT_PRODUCT_NAME = 'Heimdall Access';

/**
 * Fetches the configured product display name from the API.
 * Falls back to "Heimdall Access" if the API is unavailable or returns an invalid value.
 * Requirements: 28.1, 28.2, 28.4
 */
export function useProductName(): { productName: string; isLoading: boolean } {
  const { data, isLoading } = useQuery({
    queryKey: ['product-configuration'],
    queryFn: async () => {
      const response = await apiClient.get<ProductConfigurationResponse>('/configuration/product');
      return response.data?.data?.displayName ?? DEFAULT_PRODUCT_NAME;
    },
    staleTime: 5 * 60_000, // 5 minutes — refresh interval aligned with App Configuration sentinel
    gcTime: 30 * 60_000,
    retry: 2,
    placeholderData: DEFAULT_PRODUCT_NAME,
  });

  // Requirement 28.4: fallback if empty or >100 chars
  const productName =
    data && data.trim().length > 0 && data.length <= 100
      ? data
      : DEFAULT_PRODUCT_NAME;

  return { productName, isLoading };
}
