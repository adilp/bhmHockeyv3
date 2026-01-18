import { View, Text, Switch, StyleSheet, Platform } from 'react-native';
import { Picker } from '@react-native-picker/picker';
import { SKILL_LEVELS } from '@bhmhockey/shared';
import type { SkillLevel, UserPositions } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../theme';

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
          <View style={styles.skillPickerContainer}>
            <Picker
              selectedValue={goalieSkill}
              onValueChange={(value) => onGoalieSkillChange(value as SkillLevel)}
              style={styles.skillPicker}
              itemStyle={styles.pickerItem}
              dropdownIconColor={colors.text.primary}
              enabled={!disabled}
            >
              {SKILL_LEVELS.map((level) => (
                <Picker.Item
                  key={level}
                  label={level}
                  value={level}
                  color={Platform.OS === 'ios' ? colors.text.primary : undefined}
                />
              ))}
            </Picker>
          </View>
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
          <View style={styles.skillPickerContainer}>
            <Picker
              selectedValue={skaterSkill}
              onValueChange={(value) => onSkaterSkillChange(value as SkillLevel)}
              style={styles.skillPicker}
              itemStyle={styles.pickerItem}
              dropdownIconColor={colors.text.primary}
              enabled={!disabled}
            >
              {SKILL_LEVELS.map((level) => (
                <Picker.Item
                  key={level}
                  label={level}
                  value={level}
                  color={Platform.OS === 'ios' ? colors.text.primary : undefined}
                />
              ))}
            </Picker>
          </View>
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
  skillPickerContainer: {
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderRadius: radius.md,
    overflow: 'hidden',
    marginLeft: Platform.OS === 'ios' ? 52 : 56,
    marginRight: spacing.xs,
  },
  skillPicker: {
    height: Platform.OS === 'ios' ? 120 : 56,
    width: '100%',
    color: colors.text.primary,
  },
  pickerItem: {
    fontSize: 16,
    color: colors.text.primary,
  },
});
