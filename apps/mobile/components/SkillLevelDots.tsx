import { View, StyleSheet } from 'react-native';
import { colors, spacing } from '../theme';
import type { SkillLevel } from '@bhmhockey/shared';
import { skillLevelColors } from './SkillLevelBadges';

interface SkillLevelDotsProps {
  levels?: SkillLevel[];
}

// Order for display (highest to lowest)
const skillLevelOrder: SkillLevel[] = ['Gold', 'Silver', 'Bronze', 'D-League'];

/**
 * Compact skill level indicator using small colored dots
 * Used in EventCard list view to reduce visual noise
 */
export function SkillLevelDots({ levels }: SkillLevelDotsProps) {
  if (!levels || levels.length === 0) return null;

  // Sort levels by skill order
  const sortedLevels = [...levels].sort(
    (a, b) => skillLevelOrder.indexOf(a) - skillLevelOrder.indexOf(b)
  );

  return (
    <View style={styles.container}>
      {sortedLevels.map((level) => (
        <View
          key={level}
          style={[
            styles.dot,
            { backgroundColor: skillLevelColors[level] },
          ]}
        />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.xs,
  },
  dot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
});
