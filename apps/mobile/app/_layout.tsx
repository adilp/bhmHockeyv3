import { Stack } from 'expo-router';
import { useEffect, useRef } from 'react';
import { Text, TextInput, View } from 'react-native';
import { SafeAreaProvider, useSafeAreaInsets } from 'react-native-safe-area-context';

import * as Notifications from 'expo-notifications';
import { initializeApiClient } from '@bhmhockey/api-client';
import { getApiUrl } from '../config/api';
import { colors } from '../theme';
import { useAuthStore } from '../stores/authStore';
import { EnvBanner } from '../components';
import {
  registerForPushNotificationsAsync,
  savePushTokenToBackend,
  addNotificationReceivedListener,
  addNotificationResponseReceivedListener,
  getLastNotificationResponse,
  handleNotificationData,
  handleForegroundNotification,
} from '../utils/notifications';

// Disable font scaling globally to prevent text overflow on devices with large font settings
// This must be set before any components render
Text.defaultProps = Text.defaultProps || {};
Text.defaultProps.allowFontScaling = false;

TextInput.defaultProps = TextInput.defaultProps || {};
TextInput.defaultProps.allowFontScaling = false;

function RootLayoutContent() {
  const insets = useSafeAreaInsets();
  const { isAuthenticated } = useAuthStore();
  const notificationListener = useRef<Notifications.Subscription | null>(null);
  const responseListener = useRef<Notifications.Subscription | null>(null);
  const lastHandledNotificationId = useRef<string | null>(null);

  useEffect(() => {
    // Initialize API client on app startup
    initializeApiClient({
      baseURL: getApiUrl(),
      onAuthError: () => {
        // When 401 occurs, update Zustand store to trigger redirect to login
        console.log('🔒 Auth error (401) - logging out user');
        useAuthStore.setState({
          user: null,
          isAuthenticated: false,
          isLoading: false,
        });
      },
    });
  }, []);

  // Set up push notifications when authenticated
  // This runs in _layout.tsx which stays mounted for the entire app lifecycle
  useEffect(() => {
    console.log('🔔 [Layout] Auth state changed, isAuthenticated:', isAuthenticated);
    if (!isAuthenticated) {
      console.log('🔔 [Layout] Not authenticated, skipping push notification setup');
      return;
    }

    console.log('🔔 [Layout] Setting up push notifications...');

    // Register for push notifications and save token to backend
    registerForPushNotificationsAsync().then((token) => {
      if (token) {
        console.log('🔔 [Layout] Got push token, saving to backend:', token);
        savePushTokenToBackend(token);
      } else {
        console.log('🔔 [Layout] No push token received (might be simulator or permissions denied)');
      }
    });

    // Check for cold start: app was launched by tapping a notification while killed
    // This catches the notification that addNotificationResponseReceivedListener misses
    getLastNotificationResponse().then((response) => {
      if (response) {
        const notificationId = response.notification.request.identifier;
        // Avoid handling the same notification twice (stale response check)
        if (lastHandledNotificationId.current === notificationId) {
          console.log('🔔 [Layout] Cold start: Already handled this notification, skipping');
          return;
        }
        lastHandledNotificationId.current = notificationId;

        console.log('🔔 [Layout] Cold start: App was launched via notification tap');
        const data = response.notification.request.content.data;
        // Delay navigation slightly to ensure the app is fully mounted
        setTimeout(() => {
          console.log('🔔 [Layout] Handling cold start notification data:', data);
          handleNotificationData(data);
        }, 500);
      }
    });

    // Listen for notifications received while app is in foreground
    notificationListener.current = addNotificationReceivedListener((notification) => {
      console.log('🔔 [Layout] Notification received:', notification);
      const data = notification.request.content.data;
      handleForegroundNotification(data);
    });

    // Listen for user tapping on a notification (while app is running or in background)
    responseListener.current = addNotificationResponseReceivedListener((response) => {
      const notificationId = response.notification.request.identifier;
      // Avoid handling the same notification twice
      if (lastHandledNotificationId.current === notificationId) {
        console.log('🔔 [Layout] Notification tapped: Already handled, skipping');
        return;
      }
      lastHandledNotificationId.current = notificationId;

      console.log('🔔 [Layout] Notification tapped:', response);
      const data = response.notification.request.content.data;
      handleNotificationData(data);
    });

    return () => {
      console.log('🔔 [Layout] Cleaning up notification listeners');
      if (notificationListener.current) {
        notificationListener.current.remove();
      }
      if (responseListener.current) {
        responseListener.current.remove();
      }
    };
  }, [isAuthenticated]);

  return (
    <View style={{ flex: 1, paddingTop: insets.top, backgroundColor: colors.bg.darkest }}>
      <EnvBanner />
      <Stack
        screenOptions={{
          headerStyle: {
            backgroundColor: colors.bg.dark,
          },
          headerTintColor: colors.primary.teal,
          headerTitleStyle: {
            color: colors.text.primary,
            fontWeight: '600',
          },
          contentStyle: {
            backgroundColor: colors.bg.darkest,
          },
          headerBackTitle: 'Back',
        }}
      >
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="(auth)" options={{ headerShown: false }} />
        <Stack.Screen name="index" options={{ headerShown: false }} />
        {/* Event screens */}
        <Stack.Screen name="events/create" options={{ title: 'Create Event', presentation: 'modal' }} />
        <Stack.Screen name="events/edit" options={{ title: 'Edit Event' }} />
        <Stack.Screen name="events/[id]/index" options={{ title: 'Event' }} />
        {/* Organization screens */}
        <Stack.Screen name="organizations/create" options={{ title: 'Create Organization', presentation: 'modal' }} />
        <Stack.Screen name="organizations/[id]" options={{ title: 'Organization' }} />
        <Stack.Screen name="organizations/edit" options={{ title: 'Edit Organization' }} />
      </Stack>
    </View>
  );
}

export default function RootLayout() {
  return (
    <SafeAreaProvider>
      <RootLayoutContent />
    </SafeAreaProvider>
  );
}
