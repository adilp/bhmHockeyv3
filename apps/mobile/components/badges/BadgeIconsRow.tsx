import React, { useState } from 'react';
import { View, Text, StyleSheet, ViewStyle, StyleProp, Pressable } from 'react-native';
import type { UserBadgeDto } from '@bhmhockey/shared';
import { BadgeIcon } from './BadgeIcon';
import { BadgeDetailModal } from './BadgeDetailModal';
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
  /** Whose badges these are, when they are not the viewer's own */
  ownerName?: string;
}

/**
 * BadgeIconsRow - Displays up to 3 badge icons with overflow indicator
 *
 * Used on roster cards to show a compact view of user badges.
 * If totalCount > badges.length, shows "+N" overflow text.
 *
 * Layout: [icon] [icon] [icon] +N
 */
export function BadgeIconsRow({ badges, size = 24, maxDisplay = 3, totalCount, style, ownerName }: BadgeIconsRowProps) {
  const [selected, setSelected] = useState<UserBadgeDto | null>(null);
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
        // Tappable so anyone can read what a badge was awarded for. hitSlop
        // keeps the small roster icons reachable without enlarging the row.
        <Pressable
          key={badge.id}
          onPress={() => setSelected(badge)}
          hitSlop={8}
          accessibilityRole="button"
          accessibilityLabel={badge.badgeType.name}
        >
          <BadgeIcon iconName={badge.badgeType.iconName} size={size} />
        </Pressable>
      ))}
      {overflow > 0 && (
        <Text style={styles.overflow} allowFontScaling={false}>+{overflow}</Text>
      )}

      <BadgeDetailModal
        badge={selected}
        ownerName={ownerName}
        onClose={() => setSelected(null)}
      />
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
