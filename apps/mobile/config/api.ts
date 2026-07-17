import { Platform } from 'react-native';

/**
 * Get the API URL based on the platform and environment
 *
 * Resolution order:
 * 1. EXPO_PUBLIC_API_URL env var — set in apps/mobile/.env for local dev
 *    (gitignored; see apps/mobile/.env.example for per-platform values)
 * 2. Production URL (fallback, used by release builds)
 *
 * Env vars are inlined at bundle time: restart Metro after changing .env
 * (npx expo start --clear).
 *
 * Note: /health endpoint is at root level, not under /api
 */
export function getApiUrl(): string {
  return process.env.EXPO_PUBLIC_API_URL || 'https://bhmhockey-mb3md.ondigitalocean.app/api';
}

/**
 * Get the base server URL (without /api suffix) for non-API endpoints like /health
 */
export function getBaseUrl(): string {
  const apiUrl = getApiUrl();
  return apiUrl.replace(/\/api$/, '');
}

/**
 * Log the current API configuration (useful for debugging)
 */
export function logApiConfig() {
  console.log('🔧 API Configuration:');
  console.log(`  Platform: ${Platform.OS}`);
  console.log(`  Environment: ${__DEV__ ? 'Development' : 'Production'}`);
  console.log(`  API URL: ${getApiUrl()}`);
}
