import { View, Text, StyleSheet } from 'react-native';
import { colors, spacing, radius } from '../theme';
import type { SkillLevel } from '@bhmhockey/shared';

interface SkillLevelBadgesProps {
  levels?: SkillLevel[];
  size?: 'small' | 'medium';
}

// Skill level badge colors (metallic theme)
export const skillLevelColors: Record<SkillLevel, string> = {
  Gold: '#FFD700',
  Silver: '#C0C0C0',
  Bronze: '#CD7F32',
  'D-League': colors.primary.blue,
};

// Order for display (highest to lowest)
const skillLevelOrder: SkillLevel[] = ['Gold', 'Silver', 'Bronze', 'D-League'];

export function SkillLevelBadges({ levels, size = 'medium' }: SkillLevelBadgesProps) {
  if (!levels || levels.length === 0) return null;

  // Sort levels by skill order
  const sortedLevels = [...levels].sort(
    (a, b) => skillLevelOrder.indexOf(a) - skillLevelOrder.indexOf(b)
  );

  const isSmall = size === 'small';

  return (
    <View style={styles.container}>
      {sortedLevels.map((level) => (
        <View
          key={level}
          style={[
            styles.badge,
            isSmall && styles.badgeSmall,
            { backgroundColor: skillLevelColors[level] },
          ]}
        >
          <Text style={[styles.badgeText, isSmall && styles.badgeTextSmall]}>
            {level}
          </Text>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.xs,
  },
  badge: {
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: radius.round,
  },
  badgeSmall: {
    paddingHorizontal: 8,
    paddingVertical: 2,
  },
  badgeText: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.bg.darkest,
  },
  badgeTextSmall: {
    fontSize: 10,
  },
});
