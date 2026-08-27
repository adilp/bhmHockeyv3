import { View, Text, Switch, StyleSheet, Platform, TouchableOpacity } from 'react-native';
import { SKILL_LEVELS } from '@bhmhockey/shared';
import type { SkillLevel, UserPositions } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../theme';
import { skillLevelColors } from './SkillLevelBadges';

export interface PositionState {
  isGoalie: boolean;
  goalieSkill: SkillLevel;
  isSkater: boolean;
  skaterSkill: SkillLevel;
}

interface PositionSelectorProps {
  isGoalie: boolean;
  goalieSkill: SkillLevel;
  isSkater: boolean;
  skaterSkill: SkillLevel;
  onGoalieChange: (value: boolean) => void;
  onGoalieSkillChange: (value: SkillLevel) => void;
  onSkaterChange: (value: boolean) => void;
  onSkaterSkillChange: (value: SkillLevel) => void;
  disabled?: boolean;
}

/**
 * Single-select skill pills. Replaces a native wheel Picker, which clipped
 * its top option at the height it was given and sat oddly indented - four
 * options fit on one row and match the rest of the app's controls.
 */
function SkillPills({
  value,
  onChange,
  disabled,
}: {
  value: SkillLevel;
  onChange: (level: SkillLevel) => void;
  disabled?: boolean;
}) {
  return (
    <View style={styles.skillPills}>
      {SKILL_LEVELS.map((level) => {
        const isSelected = value === level;
        return (
          <TouchableOpacity
            key={level}
            style={[
              styles.skillPill,
              isSelected && { backgroundColor: skillLevelColors[level], borderColor: skillLevelColors[level] },
              disabled && styles.skillPillDisabled,
            ]}
            onPress={() => onChange(level)}
            disabled={disabled}
            activeOpacity={0.7}
          >
            <Text
              style={[styles.skillPillText, isSelected && styles.skillPillTextSelected]}
              allowFontScaling={false}
            >
              {level}
            </Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

export function PositionSelector({
  isGoalie,
  goalieSkill,
  isSkater,
  skaterSkill,
  onGoalieChange,
  onGoalieSkillChange,
  onSkaterChange,
  onSkaterSkillChange,
  disabled = false,
}: PositionSelectorProps) {
  return (
    <View>
      {/* Goalie Position */}
      <View style={styles.positionRow}>
        <View style={styles.positionHeader}>
          <Switch
            value={isGoalie}
            onValueChange={onGoalieChange}
            trackColor={{ false: colors.bg.hover, true: colors.primary.teal }}
            thumbColor={isGoalie ? colors.text.primary : colors.text.muted}
            disabled={disabled}
          />
          <Text
            style={[styles.positionLabel, isGoalie && styles.positionLabelActive]}
            allowFontScaling={false}
          >
            Goalie
          </Text>
        </View>
        {isGoalie && (
          <SkillPills
            value={goalieSkill}
            onChange={onGoalieSkillChange}
            disabled={disabled}
          />
        )}
      </View>

      {/* Skater Position */}
      <View style={[styles.positionRow, styles.lastPositionRow]}>
        <View style={styles.positionHeader}>
          <Switch
            value={isSkater}
            onValueChange={onSkaterChange}
            trackColor={{ false: colors.bg.hover, true: colors.primary.teal }}
            thumbColor={isSkater ? colors.text.primary : colors.text.muted}
            disabled={disabled}
          />
          <Text
            style={[styles.positionLabel, isSkater && styles.positionLabelActive]}
            allowFontScaling={false}
          >
            Skater
          </Text>
        </View>
        {isSkater && (
          <SkillPills
            value={skaterSkill}
            onChange={onSkaterSkillChange}
            disabled={disabled}
          />
        )}
      </View>
    </View>
  );
}

// Helper to build UserPositions from state
export function buildPositionsFromState(state: PositionState): UserPositions {
  const positions: UserPositions = {};
  if (state.isGoalie) {
    positions.goalie = state.goalieSkill;
  }
  if (state.isSkater) {
    positions.skater = state.skaterSkill;
  }
  return positions;
}

// Helper to create state from UserPositions
export function createStateFromPositions(positions?: UserPositions): Partial<PositionState> {
  return {
    isGoalie: !!positions?.goalie,
    goalieSkill: positions?.goalie || 'Bronze',
    isSkater: !!positions?.skater,
    skaterSkill: positions?.skater || 'Bronze',
  };
}

const styles = StyleSheet.create({
  skillPills: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.xs,
    marginLeft: Platform.OS === 'ios' ? 52 : 56,
    marginTop: spacing.xs,
  },
  skillPill: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.muted,
  },
  skillPillDisabled: {
    opacity: 0.5,
  },
  skillPillText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
  },
  skillPillTextSelected: {
    color: colors.bg.darkest,
  },
  positionRow: {
    marginBottom: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
    paddingBottom: spacing.md,
  },
  lastPositionRow: {
    borderBottomWidth: 0,
    marginBottom: 0,
    paddingBottom: 0,
  },
  positionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  positionLabel: {
    flex: 1,
    fontSize: 16,
    marginLeft: spacing.md,
    color: colors.text.muted,
  },
  positionLabelActive: {
    color: colors.primary.teal,
    fontWeight: '600',
  },
});
