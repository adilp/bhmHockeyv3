import { Platform } from 'react-native';
import Constants from 'expo-constants';

/**
 * Get the API URL based on the platform and environment
 *
 * Returns the base URL with /api suffix for API client initialization.
 *
 * For local development:
 * - iOS Simulator: http://localhost:5001/api
 * - Android Emulator: http://10.0.2.2:5001/api (special IP for host machine)
 * - Physical Device: http://192.168.3.10:5001/api (your computer's local IP)
 *
 * For production: Uses the configured API_URL from app.config.js
 *
 * Note: /health endpoint is at root level, not under /api
 */
export function getApiUrl(): string {
  return 'https://bhmhockey-mb3md.ondigitalocean.app/api';
  // return 'http://192.168.3.218:5001/api';
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
