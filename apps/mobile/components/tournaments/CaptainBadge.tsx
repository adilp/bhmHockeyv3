import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { colors, spacing, radius } from '../../theme';

interface CaptainBadgeProps {
  size?: 'small' | 'medium';
}

/**
 * CaptainBadge - Small badge/indicator showing captain status
 *
 * @param size - 'small' for roster list (default), 'medium' for headers
 */
export function CaptainBadge({ size = 'small' }: CaptainBadgeProps) {
  const isSmall = size === 'small';

  return (
    <View style={[styles.badge, isSmall ? styles.badgeSmall : styles.badgeMedium]}>
      <Text style={[styles.icon, isSmall ? styles.iconSmall : styles.iconMedium]} allowFontScaling={false}>
        👑
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  badge: {
    backgroundColor: 'rgba(255, 215, 0, 0.15)', // Gold subtle background
    borderRadius: radius.sm,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: 'rgba(255, 215, 0, 0.3)', // Gold border
  },
  badgeSmall: {
    paddingHorizontal: spacing.xs,
    paddingVertical: 2,
    minWidth: 24,
  },
  badgeMedium: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    minWidth: 32,
  },
  icon: {
    // Gold text color for crown
  },
  iconSmall: {
    fontSize: 12,
  },
  iconMedium: {
    fontSize: 16,
  },
});
