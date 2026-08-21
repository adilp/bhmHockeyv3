import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { DLEAGUE_TEAMS } from '@bhmhockey/shared';
import type { DLeagueTeam } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../theme';

interface DLeagueTeamSelectorProps {
  value: DLeagueTeam | null;
  onChange: (team: DLeagueTeam | null) => void;
  disabled?: boolean;
}

// Jersey colors, mapped to something visible on the dark theme
const TEAM_COLORS: Record<string, string> = {
  Red: '#E5484D',
  Blue: '#3E9BFF',
  Gold: '#E0B341',
  Orange: '#F0761A',
  White: '#E6EDF3',
  Black: '#6E7681', // pure black would disappear against the card
};

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
            <View style={[styles.dot, { backgroundColor: TEAM_COLORS[team.color] }]} />
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
