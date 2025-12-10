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
  const configuredUrl = Constants.expoConfig?.extra?.apiUrl;

  if (__DEV__) {
    // TEMPORARY: Force physical device IP for testing
    // Remove this and uncomment detection logic below once working
    console.log('🔍 Forced to use physical device IP (192.168.3.10)');
    return 'http://192.168.3.10:5001/api';

    /* Original detection logic - currently disabled for testing
    // Check if running on physical device vs simulator/emulator
    const isPhysicalDevice = Constants.isDevice;

    // Debug logging
    console.log('🔍 Device Detection:', {
      isDevice: isPhysicalDevice,
      platform: Platform.OS,
      deviceName: Constants.deviceName,
    });

    // For Expo Go on physical devices, we should use the local IP
    // iOS Simulator shows deviceName like "iPhone 15 Pro"
    // Physical devices show actual device names
    const isSimulator = !isPhysicalDevice ||
                       Constants.deviceName?.includes('Simulator') ||
                       Constants.deviceName?.includes('Emulator');

    if (!isSimulator) {
      // Physical device - use computer's local IP
      console.log('📱 Using physical device IP');
      return 'http://192.168.3.10:5001/api';
    } else if (Platform.OS === 'android') {
      // Android emulator uses special IP for localhost
      console.log('🤖 Using Android emulator IP');
      return 'http://10.0.2.2:5001/api';
    } else {
      // iOS simulator can use localhost
      console.log('📱 Using iOS simulator localhost');
      return 'http://localhost:5001/api';
    }
    */
  }

  // Production URL from config
  return configuredUrl || 'https://bhmhockey-mb3md.ondigitalocean.app/api';
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
