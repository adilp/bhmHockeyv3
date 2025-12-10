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
            backgroundColor: colors.bg.darkest,
          },
          headerTintColor: colors.primary.teal,
          headerTitleStyle: {
            color: colors.text.primary,
            fontWeight: '600',
          },
          contentStyle: {
            backgroundColor: colors.bg.darkest,
          },
        }}
      >
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="(auth)" options={{ headerShown: false }} />
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
