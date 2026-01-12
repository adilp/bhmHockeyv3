import * as Updates from 'expo-updates';
import { useEffect, useRef } from 'react';
import { Alert, AppState, AppStateStatus } from 'react-native';

/**
 * Handles OTA (Over-The-Air) updates in production builds.
 * Checks for updates on app foreground, auto-downloads, and prompts user to restart.
 *
 * Only functional in production builds (no-op in development).
 */
export function useOtaUpdates(): void {
  const { isUpdateAvailable, isUpdatePending } = Updates.useUpdates();
  const hasShownAlert = useRef(false);

  // Show alert and restart when update has finished downloading
  useEffect(() => {
    if (isUpdatePending && !hasShownAlert.current) {
      hasShownAlert.current = true;
      console.log('📦 [OTA] Update downloaded, prompting user...');

      Alert.alert(
        'Update Available',
        'A new version has been downloaded. The app will restart to apply the update.',
        [
          {
            text: 'Restart Now',
            onPress: () => {
              console.log('📦 [OTA] User confirmed, restarting app...');
              Updates.reloadAsync();
            },
          },
        ],
        { cancelable: false }
      );
    }
  }, [isUpdatePending]);

  // Auto-download when update is available
  useEffect(() => {
    if (isUpdateAvailable) {
      console.log('📦 [OTA] Update available, downloading...');
      Updates.fetchUpdateAsync().catch((error) => {
        console.log('📦 [OTA] Download failed:', error.message);
      });
    }
  }, [isUpdateAvailable]);

  // Check for updates when app comes to foreground
  useEffect(() => {
    const handleAppStateChange = (nextAppState: AppStateStatus) => {
      if (nextAppState === 'active') {
        console.log('📦 [OTA] App foregrounded, checking for updates...');
        Updates.checkForUpdateAsync().catch((error) => {
          // Silently fail - this is expected in development mode
          console.log('📦 [OTA] Update check skipped (dev mode or error):', error.message);
        });
      }
    };

    const subscription = AppState.addEventListener('change', handleAppStateChange);
    return () => subscription.remove();
  }, []);
}
