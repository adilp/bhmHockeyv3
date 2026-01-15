import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { colors, spacing, radius } from '../theme';
import { Badge } from './Badge';
import { OrgAvatar } from './OrgAvatar';
import { TournamentStatusBadge } from './TournamentStatusBadge';
import type { TournamentDto, TournamentFormat } from '@bhmhockey/shared';

interface TournamentCardProps {
  tournament: TournamentDto;
  onPress: () => void;
}

// Format display labels
const formatLabels: Record<TournamentFormat, string> = {
  SingleElimination: 'Single Elim',
  DoubleElimination: 'Double Elim',
  RoundRobin: 'Round Robin',
};

function formatDateRange(startDate: string, endDate: string): string {
  const start = new Date(startDate);
  const end = new Date(endDate);
  const now = new Date();

  // Get date parts for comparison (in local timezone)
  const startDay = new Date(start.getFullYear(), start.getMonth(), start.getDate());
  const endDay = new Date(end.getFullYear(), end.getMonth(), end.getDate());
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const tomorrow = new Date(today);
  tomorrow.setDate(tomorrow.getDate() + 1);

  // Format options
  const monthDayOptions: Intl.DateTimeFormatOptions = {
    month: 'short',
    day: 'numeric',
  };

  const fullOptions: Intl.DateTimeFormatOptions = {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  };

  // Check if single day tournament
  if (startDay.getTime() === endDay.getTime()) {
    if (startDay.getTime() === today.getTime()) {
      return 'Today';
    }
    if (startDay.getTime() === tomorrow.getTime()) {
      return 'Tomorrow';
    }
    return start.toLocaleDateString('en-US', fullOptions);
  }

  // Multi-day tournament
  // If same year as current, omit year from start date
  const sameYear = start.getFullYear() === end.getFullYear();
  const startStr = start.toLocaleDateString('en-US', sameYear ? monthDayOptions : fullOptions);
  const endStr = end.toLocaleDateString('en-US', fullOptions);

  return `${startStr} - ${endStr}`;
}

export function TournamentCard({ tournament, onPress }: TournamentCardProps) {
  const displayName = tournament.organizationName || tournament.name;
  const showTournamentName = tournament.organizationName && tournament.name;
  const accentColor = tournament.canManage ? colors.primary.purple : null;

  // Calculate team count display
  // Note: TournamentDto doesn't include current team count, so we show max teams
  // When team count is available, update to "X/Y teams" format
  const teamCountText = `${tournament.maxTeams} teams max`;

  return (
    <TouchableOpacity style={styles.card} onPress={onPress} activeOpacity={0.7}>
      {/* Left accent bar - purple when canManage */}
      {accentColor && <View style={[styles.accent, { backgroundColor: accentColor }]} />}

      <View style={styles.content}>
        {/* Organization row - most prominent */}
        <View style={styles.orgRow}>
          <OrgAvatar name={displayName} size="small" />
          <Text style={styles.orgName} numberOfLines={1}>
            {displayName}
          </Text>
        </View>

        {/* Tournament name - only show if org exists */}
        {showTournamentName && (
          <Text style={styles.name} numberOfLines={1}>{tournament.name}</Text>
        )}

        {/* Date range */}
        <Text style={styles.dateRange}>
          {formatDateRange(tournament.startDate, tournament.endDate)}
        </Text>

        {/* Venue if available */}
        {tournament.venue && (
          <Text style={styles.venue} numberOfLines={1}>{tournament.venue}</Text>
        )}

        {/* Footer badges */}
        <View style={styles.footer}>
          <TournamentStatusBadge status={tournament.status} />
          <Badge variant="default">{teamCountText}</Badge>
          <Badge variant="purple">{formatLabels[tournament.format]}</Badge>
        </View>
      </View>

      {/* Price column */}
      <View style={styles.priceColumn}>
        {tournament.entryFee > 0 ? (
          <>
            <Text style={styles.priceAmount}>${tournament.entryFee}</Text>
            <Text style={styles.priceLabel}>
              {tournament.feeType === 'PerPlayer' ? 'per player' : 'per team'}
            </Text>
          </>
        ) : (
          <Text style={styles.freeLabel}>Free</Text>
        )}
      </View>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    marginBottom: spacing.sm,
    flexDirection: 'row',
    borderWidth: 1,
    borderColor: colors.border.default,
    overflow: 'hidden',
  },
  accent: {
    width: 4,
  },
  content: {
    flex: 1,
    padding: 14,
  },
  orgRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    marginBottom: spacing.sm,
  },
  orgName: {
    fontSize: 14,
    fontWeight: '400',
    color: colors.text.secondary,
    flex: 1,
  },
  name: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  dateRange: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: 2,
  },
  venue: {
    fontSize: 13,
    color: colors.text.secondary,
    marginBottom: spacing.sm,
  },
  footer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    flexWrap: 'wrap',
  },
  priceColumn: {
    justifyContent: 'center',
    alignItems: 'flex-end',
    paddingRight: 14,
    paddingLeft: spacing.sm,
    minWidth: 70,
  },
  priceAmount: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text.primary,
  },
  priceLabel: {
    fontSize: 10,
    color: colors.text.subtle,
    marginTop: 2,
  },
  freeLabel: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.primary.green,
  },
});
