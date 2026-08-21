import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { DLEAGUE_TEAMS } from '@bhmhockey/shared';
import type { DLeagueTeam } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../theme';
import { dLeagueTeamColor } from '../utils/dLeagueTeam';

interface DLeagueTeamSelectorProps {
  value: DLeagueTeam | null;
  onChange: (team: DLeagueTeam | null) => void;
  disabled?: boolean;
}

/**
 * Optional team picker for D-League players. Tapping the selected team
 * clears it, so a player can leave the field unset.
 */
export function DLeagueTeamSelector({ value, onChange, disabled = false }: DLeagueTeamSelectorProps) {
  return (
    <View style={styles.container}>
      {DLEAGUE_TEAMS.map((team) => {
        const selected = value === team.name;
        return (
          <TouchableOpacity
            key={team.name}
            style={[styles.option, selected && styles.optionSelected, disabled && styles.disabled]}
            onPress={() => onChange(selected ? null : team.name)}
            disabled={disabled}
            activeOpacity={0.7}
          >
            <View style={[styles.dot, { backgroundColor: dLeagueTeamColor(team.name) ?? colors.text.muted }]} />
            <Text
              style={[styles.label, selected && styles.labelSelected]}
              allowFontScaling={false}
            >
              {team.name}
            </Text>
            <Text style={styles.color} allowFontScaling={false}>{team.color}</Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: spacing.xs,
  },
  option: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderRadius: radius.md,
    paddingVertical: spacing.sm + 2,
    paddingHorizontal: spacing.md,
  },
  optionSelected: {
    borderColor: colors.primary.teal,
    backgroundColor: colors.bg.dark,
  },
  disabled: {
    opacity: 0.5,
  },
  dot: {
    width: 14,
    height: 14,
    borderRadius: 7,
    marginRight: spacing.sm,
  },
  label: {
    flex: 1,
    fontSize: 16,
    color: colors.text.secondary,
  },
  labelSelected: {
    color: colors.text.primary,
    fontWeight: '600',
  },
  color: {
    fontSize: 13,
    color: colors.text.muted,
  },
});
