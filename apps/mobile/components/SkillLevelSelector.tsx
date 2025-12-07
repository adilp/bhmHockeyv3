import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { colors, spacing, radius } from '../theme';
import { SKILL_LEVELS, type SkillLevel } from '@bhmhockey/shared';
import { skillLevelColors } from './SkillLevelBadges';

interface SkillLevelSelectorProps {
  selected: SkillLevel[];
  onChange: (levels: SkillLevel[]) => void;
  label?: string;
}

export function SkillLevelSelector({
  selected,
  onChange,
  label = 'Skill Levels',
}: SkillLevelSelectorProps) {
  const toggleLevel = (level: SkillLevel) => {
    if (selected.includes(level)) {
      // Remove level
      onChange(selected.filter((l) => l !== level));
    } else {
      // Add level
      onChange([...selected, level]);
    }
  };

  return (
    <View style={styles.container}>
      {label && <Text style={styles.label}>{label}</Text>}
      <View style={styles.buttonsContainer}>
        {SKILL_LEVELS.map((level) => {
          const isSelected = selected.includes(level);
          const levelColor = skillLevelColors[level];

          return (
            <TouchableOpacity
              key={level}
              style={[
                styles.button,
                isSelected && { backgroundColor: levelColor },
              ]}
              onPress={() => toggleLevel(level)}
              activeOpacity={0.7}
            >
              <Text
                style={[
                  styles.buttonText,
                  isSelected && styles.buttonTextSelected,
                ]}
              >
                {level}
              </Text>
            </TouchableOpacity>
          );
        })}
      </View>
      {selected.length === 0 && (
        <Text style={styles.hint}>Select one or more skill levels</Text>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    marginBottom: spacing.md,
  },
  label: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
    marginBottom: spacing.sm,
  },
  buttonsContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.sm,
  },
  button: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.muted,
  },
  buttonText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
  },
  buttonTextSelected: {
    color: colors.bg.darkest,
  },
  hint: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: spacing.xs,
  },
});
