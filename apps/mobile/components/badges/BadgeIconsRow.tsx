import React from 'react';
import { View, Text, StyleSheet, ViewStyle, StyleProp } from 'react-native';
import type { UserBadgeDto } from '@bhmhockey/shared';
import { BadgeIcon } from './BadgeIcon';
import { colors, spacing } from '../../theme';

interface BadgeIconsRowProps {
  /** Array of badges to display */
  badges: UserBadgeDto[];
  /** Size of each badge icon (default 24) */
  size?: number;
  /** Maximum number of badges to display (default 3) */
  maxDisplay?: number;
  /** @deprecated Use badges.length instead - kept for backwards compatibility */
  totalCount?: number;
  /** Optional style for the container */
  style?: StyleProp<ViewStyle>;
}

/**
 * BadgeIconsRow - Displays up to 3 badge icons with overflow indicator
 *
 * Used on roster cards to show a compact view of user badges.
 * If totalCount > badges.length, shows "+N" overflow text.
 *
 * Layout: [icon] [icon] [icon] +N
 */
export function BadgeIconsRow({ badges, size = 24, maxDisplay = 3, totalCount, style }: BadgeIconsRowProps) {
  // If no badges, render empty view to maintain height consistency
  if (!badges || badges.length === 0) {
    return <View style={[styles.container, style]} />;
  }

  // Limit displayed badges and calculate overflow
  const displayedBadges = badges.slice(0, maxDisplay);
  // Use totalCount if provided (backwards compat), otherwise use badges.length
  const total = totalCount ?? badges.length;
  const overflow = total - displayedBadges.length;

  return (
    <View style={[styles.container, style]}>
      {displayedBadges.map((badge) => (
        <BadgeIcon
          key={badge.id}
          iconName={badge.badgeType.iconName}
          size={size}
        />
      ))}
      {overflow > 0 && (
        <Text style={styles.overflow} allowFontScaling={false}>+{overflow}</Text>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.xs, // 4px gap between icons
    minHeight: 24, // Maintain consistent height even when empty
  },
  overflow: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.muted,
    marginLeft: 2, // Slight extra margin before overflow text
  },
});
