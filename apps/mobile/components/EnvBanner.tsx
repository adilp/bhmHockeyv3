import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { colors, spacing } from '../theme';
import { getApiUrl } from '../config/api';

/**
 * Environment banner that shows which API the app is connected to.
 * Only visible in development mode (__DEV__).
 *
 * Shows:
 * - Green banner for production API
 * - Orange banner for local/development API
 */
export function EnvBanner() {
  // Only show in development builds
  if (!__DEV__) return null;

  const apiUrl = getApiUrl();
  const isProduction = apiUrl.includes('digitalocean') || apiUrl.includes('https://');
  const isLocal = apiUrl.includes('localhost') || apiUrl.includes('10.0.2.2') || apiUrl.includes('192.168');

  const label = isProduction ? 'PROD API' : isLocal ? 'LOCAL API' : 'DEV API';
  const backgroundColor = isProduction ? colors.status.success : colors.status.warning;

  return (
    <View style={[styles.banner, { backgroundColor }]}>
      <Text style={styles.text}>{label}: {apiUrl.replace('/api', '')}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  banner: {
    paddingVertical: spacing.xs,
    paddingHorizontal: spacing.md,
    alignItems: 'center',
  },
  text: {
    color: colors.bg.darkest,
    fontSize: 10,
    fontWeight: '600',
  },
});
