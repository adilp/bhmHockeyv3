import { Stack } from 'expo-router';
import { useEffect, useRef } from 'react';
import { Text, TextInput, View, AppState } from 'react-native';
import { SafeAreaProvider, useSafeAreaInsets } from 'react-native-safe-area-context';

import * as Notifications from 'expo-notifications';
import { initializeApiClient } from '@bhmhockey/api-client';
import { getApiUrl } from '../config/api';
import { colors } from '../theme';
import { useAuthStore } from '../stores/authStore';
import { useCelebrationStore } from '../stores/celebrationStore';
import { BadgeCelebrationModal, EnvBanner } from '../components';
import { useBadgeCelebration } from '../hooks';
import {
  registerForPushNotificationsAsync,
  savePushTokenToBackend,
  addNotificationReceivedListener,
  addNotificationResponseReceivedListener,
  getLastNotificationResponse,
  handleNotificationData,
  handleForegroundNotification,
} from '../utils/notifications';
import {
  getInitialDeepLink,
  addDeepLinkListener,
  handleDeepLink,
} from '../utils/deepLinks';
import { useOtaUpdates } from '../hooks';

// Disable font scaling globally to prevent text overflow on devices with large font settings
// This must be set before any components render
// Note: defaultProps is deprecated in RN 0.76+ types but still works at runtime
// @ts-expect-error defaultProps removed from RN types but still functional
Text.defaultProps = Text.defaultProps || {};
// @ts-expect-error defaultProps removed from RN types but still functional
Text.defaultProps.allowFontScaling = false;

// @ts-expect-error defaultProps removed from RN types but still functional
TextInput.defaultProps = TextInput.defaultProps || {};
// @ts-expect-error defaultProps removed from RN types but still functional
TextInput.defaultProps.allowFontScaling = false;

function RootLayoutContent() {
  const insets = useSafeAreaInsets();
  const { isAuthenticated } = useAuthStore();
  const { fetchUncelebrated } = useCelebrationStore();
  const isShowingCelebration = useCelebrationStore((state) => state.isShowingCelebration);
  const { currentBadge, remaining, dismiss, navigateToTrophyCase } = useBadgeCelebration();

  useOtaUpdates();
  const notificationListener = useRef<Notifications.Subscription | null>(null);
  const responseListener = useRef<Notifications.Subscription | null>(null);
  const lastHandledNotificationId = useRef<string | null>(null);
  const deepLinkListener = useRef<ReturnType<typeof addDeepLinkListener> | null>(null);
  const lastHandledDeepLink = useRef<string | null>(null);

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

  // Handle deep links (cold start and while app is running)
  useEffect(() => {
    // Handle cold start: app launched via deep link while killed
    getInitialDeepLink().then((url) => {
      if (!url) {
        return;
      }

      console.log('🔗 Initial deep link detected:', url);

      // Prevent duplicate handling
      if (lastHandledDeepLink.current === url) {
        return;
      }

      lastHandledDeepLink.current = url;

      // Delay navigation to ensure app is fully mounted
      setTimeout(() => {
        handleDeepLink(url);
      }, 500);
    });

    // Handle deep links while app is running (foreground or background)
    deepLinkListener.current = addDeepLinkListener((url) => {
      console.log('🔗 Deep link received while app running:', url);

      // Prevent duplicate handling
      if (lastHandledDeepLink.current === url) {
        return;
      }

      lastHandledDeepLink.current = url;
      handleDeepLink(url);
    });

    return () => {
      deepLinkListener.current?.remove();
    };
  }, []);

  // Set up push notifications when authenticated
  // Runs in _layout.tsx which stays mounted for the entire app lifecycle
  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }

    registerForPushNotificationsAsync().then((token) => {
      if (token) {
        savePushTokenToBackend(token);
      }
    });

    // Handle cold start: app launched by tapping notification while killed
    // Catches the notification that addNotificationResponseReceivedListener misses
    getLastNotificationResponse().then((response) => {
      if (!response) {
        return;
      }

      const notificationId = response.notification.request.identifier;
      if (lastHandledNotificationId.current === notificationId) {
        return;
      }

      lastHandledNotificationId.current = notificationId;
      const data = response.notification.request.content.data;

      // Delay navigation to ensure app is fully mounted
      setTimeout(() => {
        handleNotificationData(data);
      }, 500);
    });

    notificationListener.current = addNotificationReceivedListener((notification) => {
      const data = notification.request.content.data;
      handleForegroundNotification(data);
    });

    responseListener.current = addNotificationResponseReceivedListener((response) => {
      const notificationId = response.notification.request.identifier;
      if (lastHandledNotificationId.current === notificationId) {
        return;
      }

      lastHandledNotificationId.current = notificationId;
      const data = response.notification.request.content.data;
      handleNotificationData(data);
    });

    return () => {
      notificationListener.current?.remove();
      responseListener.current?.remove();
    };
  }, [isAuthenticated]);

  // AppState listener for badge celebration queue
  // Checks for uncelebrated badges when app comes to foreground
  useEffect(() => {
    if (!isAuthenticated) {
      return;
    }

    // Fetch on mount when authenticated
    fetchUncelebrated();

    // Listen for app state changes
    const subscription = AppState.addEventListener('change', (nextAppState) => {
      if (nextAppState === 'active') {
        // App came to foreground - check for uncelebrated badges
        fetchUncelebrated();
      }
    });

    return () => {
      subscription.remove();
    };
  }, [isAuthenticated, fetchUncelebrated]);

  return (
    <View style={{ flex: 1, paddingTop: insets.top, backgroundColor: colors.bg.darkest }}>
      <EnvBanner />

      {/* Badge celebration modal - shown when uncelebrated badges exist */}
      {isShowingCelebration && currentBadge && (
        <BadgeCelebrationModal
          badge={currentBadge}
          remaining={remaining}
          onDismiss={dismiss}
          onViewTrophyCase={navigateToTrophyCase}
        />
      )}
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
        {/* Tournament screens */}
        <Stack.Screen name="tournaments/create" options={{ title: 'Create Tournament' }} />
        <Stack.Screen name="tournaments/[id]/index" options={{ title: 'Tournament' }} />
        <Stack.Screen name="tournaments/[id]/bracket" options={{ title: 'Bracket' }} />
        <Stack.Screen name="tournaments/[id]/schedule" options={{ title: 'Schedule' }} />
        <Stack.Screen name="tournaments/[id]/standings" options={{ title: 'Standings' }} />
        <Stack.Screen name="tournaments/[id]/register" options={{ title: 'Register' }} />
        <Stack.Screen name="tournaments/[id]/teams" options={{ title: 'Teams' }} />
        <Stack.Screen name="tournaments/[id]/teams/create" options={{ title: 'Create Team' }} />
        <Stack.Screen name="tournaments/[id]/teams/[teamId]/index" options={{ title: 'Team' }} />
        <Stack.Screen name="tournaments/[id]/match/[matchId]" options={{ title: 'Match Details' }} />
        <Stack.Screen name="tournaments/[id]/manage/index" options={{ title: 'Manage Tournament' }} />
        <Stack.Screen name="tournaments/[id]/manage/settings" options={{ title: 'Settings' }} />
        <Stack.Screen name="tournaments/[id]/manage/questions" options={{ title: 'Custom Questions' }} />
        <Stack.Screen name="tournaments/[id]/manage/registrations" options={{ title: 'Registrations' }} />
        <Stack.Screen name="tournaments/[id]/manage/teams" options={{ title: 'Manage Teams' }} />
        <Stack.Screen name="tournaments/[id]/manage/admins" options={{ title: 'Admins' }} />
        <Stack.Screen name="tournaments/[id]/manage/standings" options={{ title: 'Manage Standings' }} />
        <Stack.Screen name="tournaments/[id]/manage/audit" options={{ title: 'Audit Log' }} />
        <Stack.Screen name="admin/index" options={{ title: 'Admin Panel' }} />
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
