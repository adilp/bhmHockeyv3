import { Stack } from 'expo-router';
import { useEffect } from 'react';
import { View } from 'react-native';
import { SafeAreaProvider, useSafeAreaInsets } from 'react-native-safe-area-context';
import { initializeApiClient } from '@bhmhockey/api-client';
import { getApiUrl } from '../config/api';
import { colors } from '../theme';
import { useAuthStore } from '../stores/authStore';
import { EnvBanner } from '../components';

function RootLayoutContent() {
  const insets = useSafeAreaInsets();

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
        <Stack.Screen name="events/[id]" options={{ title: 'Event' }} />
        <Stack.Screen name="events/[id]/registrations" options={{ title: 'Registrations' }} />
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
