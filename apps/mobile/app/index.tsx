import { useEffect, useRef } from 'react';
import { View, ActivityIndicator, StyleSheet } from 'react-native';
import { useRouter, Redirect } from 'expo-router';
import * as Notifications from 'expo-notifications';
import { useAuthStore } from '../stores/authStore';
import {
  registerForPushNotificationsAsync,
  savePushTokenToBackend,
  addNotificationReceivedListener,
  addNotificationResponseReceivedListener,
} from '../utils/notifications';

export default function IndexScreen() {
  const router = useRouter();
  const { isAuthenticated, isLoading, checkAuth } = useAuthStore();
  const notificationListener = useRef<Notifications.Subscription | null>(null);
  const responseListener = useRef<Notifications.Subscription | null>(null);

  useEffect(() => {
    console.log('🔍 Index: checking auth...');
    checkAuth();
  }, []);

  // Set up push notifications when authenticated
  useEffect(() => {
    console.log('🔔 Auth state changed, isAuthenticated:', isAuthenticated);
    if (!isAuthenticated) {
      console.log('🔔 Not authenticated, skipping push notification setup');
      return;
    }

    console.log('🔔 Setting up push notifications...');

    // Register for push notifications and save token to backend
    registerForPushNotificationsAsync().then((token) => {
      if (token) {
        console.log('🔔 Got push token, saving to backend:', token);
        savePushTokenToBackend(token);
      } else {
        console.log('🔔 No push token received (might be simulator or permissions denied)');
      }
    });

    // Listen for notifications received while app is in foreground
    notificationListener.current = addNotificationReceivedListener((notification) => {
      console.log('Notification received:', notification);
    });

    // Listen for user tapping on a notification
    responseListener.current = addNotificationResponseReceivedListener((response) => {
      console.log('Notification tapped:', response);
      const data = response.notification.request.content.data;

      // Navigate to event detail if notification contains eventId
      if (data?.eventId) {
        router.push(`/events/${data.eventId}`);
      }
    });

    return () => {
      if (notificationListener.current) {
        notificationListener.current.remove();
      }
      if (responseListener.current) {
        responseListener.current.remove();
      }
    };
  }, [isAuthenticated]);

  // Show loading while checking authentication
  if (isLoading) {
    return (
      <View style={styles.container}>
        <ActivityIndicator size="large" color="#007AFF" />
      </View>
    );
  }

  // Redirect based on authentication status
  if (isAuthenticated) {
    return <Redirect href="/(tabs)" />;
  }

  return <Redirect href="/(auth)/login" />;
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#fff',
  },
});
