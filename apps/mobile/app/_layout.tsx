import { Stack } from 'expo-router';
import { useEffect } from 'react';
import { initializeApiClient } from '@bhmhockey/api-client';
import { getApiUrl } from '../config/api';

export default function RootLayout() {
  useEffect(() => {
    // Initialize API client on app startup
    initializeApiClient({
      baseURL: getApiUrl(),
    });
  }, []);

  return (
    <Stack>
      <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
      <Stack.Screen name="(auth)" options={{ headerShown: false }} />
    </Stack>
  );
}
