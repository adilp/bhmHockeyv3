import * as Notifications from 'expo-notifications';
import * as Device from 'expo-device';
import Constants from 'expo-constants';
import { Platform } from 'react-native';
import { userService } from '@bhmhockey/api-client';

// Configure how notifications are handled when app is in foreground
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldPlaySound: true,
    shouldSetBadge: false,
    shouldShowBanner: true,
    shouldShowList: true,
  }),
});

/**
 * Register for push notifications and return the Expo push token
 */
export async function registerForPushNotificationsAsync(): Promise<string | null> {
  let token: string | null = null;

  // Must be a physical device for push notifications
  if (!Device.isDevice) {
    console.log('Push notifications require a physical device');
    return null;
  }

  // Check existing permissions
  const { status: existingStatus } = await Notifications.getPermissionsAsync();
  let finalStatus = existingStatus;

  // Request permissions if not already granted
  if (existingStatus !== 'granted') {
    const { status } = await Notifications.requestPermissionsAsync();
    finalStatus = status;
  }

  if (finalStatus !== 'granted') {
    console.log('Failed to get push notification permissions');
    return null;
  }

  // Get the Expo push token
  try {
    // Get projectId from Constants (set by EAS or Expo Go)
    const projectId =
      Constants.expoConfig?.extra?.eas?.projectId ??
      Constants.easConfig?.projectId;

    if (!projectId) {
      console.log('🔔 No EAS projectId found - push notifications require EAS setup');
      console.log('🔔 Run: npx eas init (or eas build:configure) to set up your project');
      return null;
    }

    console.log('🔔 Using projectId:', projectId);
    const response = await Notifications.getExpoPushTokenAsync({ projectId });
    token = response.data;
    console.log('🔔 Expo push token:', token);
  } catch (error) {
    console.error('🔔 Error getting push token:', error);
    if (error instanceof Error) {
      console.error('🔔 Error details:', error.message);
    }
    return null;
  }

  // Android-specific notification channel configuration
  if (Platform.OS === 'android') {
    await Notifications.setNotificationChannelAsync('default', {
      name: 'Default',
      importance: Notifications.AndroidImportance.MAX,
      vibrationPattern: [0, 250, 250, 250],
      lightColor: '#00D9C0', // Teal accent color
    });
  }

  return token;
}

/**
 * Send the push token to the backend to save for the current user
 */
export async function savePushTokenToBackend(token: string): Promise<void> {
  try {
    console.log('🔔 Sending push token to backend...');
    await userService.updatePushToken(token);
    console.log('🔔 Push token saved to backend successfully!');
  } catch (error) {
    console.error('🔔 Error saving push token to backend:', error);
    if (error instanceof Error) {
      console.error('🔔 Error details:', error.message);
    }
  }
}

/**
 * Add notification received listener (while app is in foreground)
 */
export function addNotificationReceivedListener(
  callback: (notification: Notifications.Notification) => void
): Notifications.Subscription {
  return Notifications.addNotificationReceivedListener(callback);
}

/**
 * Add notification response listener (when user taps notification)
 */
export function addNotificationResponseReceivedListener(
  callback: (response: Notifications.NotificationResponse) => void
): Notifications.Subscription {
  return Notifications.addNotificationResponseReceivedListener(callback);
}

/**
 * Get the last notification response (if app was opened via notification)
 */
export async function getLastNotificationResponse(): Promise<Notifications.NotificationResponse | null> {
  return await Notifications.getLastNotificationResponseAsync();
}
