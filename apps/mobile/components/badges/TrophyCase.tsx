import React from 'react';
import { View, Text, StyleSheet, ViewStyle, StyleProp } from 'react-native';
import type { UserBadgeDto } from '@bhmhockey/shared';
import { BadgeIcon } from './BadgeIcon';
import { colors, spacing, radius } from '../../theme';

interface TrophyCaseProps {
  /** Array of all user badges */
  badges: UserBadgeDto[];
  /** Optional style for the container */
  style?: StyleProp<ViewStyle>;
}

/**
 * Extracts display context text from badge context JSON
 * Handles tournament badges (tournamentName) and generic badges (description)
 */
function getContextText(context: Record<string, unknown>): string | null {
  if (typeof context.tournamentName === 'string') {
    return context.tournamentName;
  }
  if (typeof context.description === 'string') {
    return context.description;
  }
  return null;
}

/**
 * Formats an ISO date string to "Earned [Month Day, Year]"
 */
function formatEarnedDate(isoDate: string): string {
  try {
    const date = new Date(isoDate);
    const formatted = date.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
    return `Earned ${formatted}`;
  } catch {
    return 'Earned';
  }
}

/**
 * TrophyCase - Displays full badge list with details
 *
 * Used in PlayerDetailModal and Profile screen to show all user badges
 * with name, context, and earned date.
 *
 * Each badge row shows:
 * - 24px icon
 * - Badge name (16px semibold)
 * - Context text (14px muted) - tournament name or description
 * - Earned date (12px subtle)
 */
export function TrophyCase({ badges, style }: TrophyCaseProps) {
  // Empty state
  if (!badges || badges.length === 0) {
    return (
      <View style={[styles.container, styles.emptyContainer, style]}>
        <Text style={styles.emptyText}>No badges earned yet</Text>
      </View>
    );
  }

  return (
    <View style={[styles.container, style]}>
      {badges.map((badge) => {
        const contextText = getContextText(badge.context);
        const earnedText = formatEarnedDate(badge.earnedAt);

        return (
          <View key={badge.id} style={styles.badgeRow}>
            <BadgeIcon
              iconName={badge.badgeType.iconName}
              size={24}
              style={styles.icon}
            />
            <View style={styles.badgeInfo}>
              <Text style={styles.badgeName}>{badge.badgeType.name}</Text>
              {contextText && (
                <Text style={styles.badgeContext}>{contextText}</Text>
              )}
              <Text style={styles.badgeDate}>{earnedText}</Text>
            </View>
          </View>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: spacing.md, // 16px gap between badge rows (spacing only, no dividers)
  },
  emptyContainer: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.lg,
    alignItems: 'center',
  },
  emptyText: {
    fontSize: 14,
    color: colors.text.muted,
  },
  badgeRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  icon: {
    marginRight: spacing.sm,
    marginTop: 2, // Slight offset to align with first line of text
  },
  badgeInfo: {
    flex: 1,
  },
  badgeName: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  badgeContext: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: 2,
  },
  badgeDate: {
    fontSize: 12,
    color: colors.text.subtle,
    marginTop: 2,
  },
});
