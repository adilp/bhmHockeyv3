import axios, { AxiosInstance, AxiosError } from 'axios';
import { authStorage } from './storage/auth';
import type { ApiError } from '@bhmhockey/shared';

let apiInstance: AxiosInstance | null = null;

interface ApiClientConfig {
  baseURL: string;
  onAuthError?: () => void;
}

/**
 * Initialize the API client with configuration
 */
export function initializeApiClient(config: ApiClientConfig): void {
  apiInstance = axios.create({
    baseURL: config.baseURL,
    timeout: 30000,
    headers: {
      'Content-Type': 'application/json',
    },
  });

  // Request interceptor to add auth token
  apiInstance.interceptors.request.use(
    async (axiosConfig) => {
      const token = await authStorage.getToken();
      if (token) {
        axiosConfig.headers.Authorization = `Bearer ${token}`;
      }
      return axiosConfig;
    },
    (error) => Promise.reject(error)
  );

  // Response interceptor for error handling
  apiInstance.interceptors.response.use(
    (response) => response,
    async (error: AxiosError<ApiError>) => {
      // Handle 401 unauthorized
      if (error.response?.status === 401) {
        await authStorage.removeToken();
        config.onAuthError?.();
      }

      // Transform error response
      const apiError: ApiError = {
        message: error.response?.data?.message || error.message || 'An error occurred',
        statusCode: error.response?.status || 500,
        errors: error.response?.data?.errors,
      };

      return Promise.reject(apiError);
    }
  );

  console.log('✅ API Client initialized:', config.baseURL);
}

/**
 * Get the API client instance
 */
export function getApiClient(): AxiosInstance {
  if (!apiInstance) {
    throw new Error('API client not initialized. Call initializeApiClient() first.');
  }
  return apiInstance;
}

// Export for convenience
export const apiClient = {
  get instance() {
    return getApiClient();
  },
};
