import { Stack } from 'expo-router';
import { useEffect } from 'react';
import { initializeApiClient } from '@bhmhockey/api-client';
import { getApiUrl } from '../config/api';
import { colors } from '../theme';

export default function RootLayout() {
  useEffect(() => {
    // Initialize API client on app startup
    initializeApiClient({
      baseURL: getApiUrl(),
    });
  }, []);

  return (
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
  );
}
