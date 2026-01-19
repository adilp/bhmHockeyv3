import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing, radius } from '../theme';
import { Badge } from './Badge';
import type { UpcomingTournamentMatchDto } from '@bhmhockey/shared';

interface TournamentGameCardProps {
  match: UpcomingTournamentMatchDto;
  onPress: () => void;
}

// Gold accent color for tournaments
const GOLD_ACCENT = '#FFD700';

function formatDateTime(dateString: string): { date: string; time: string } {
  const date = new Date(dateString);
  const now = new Date();

  // Get date parts for comparison (in local timezone)
  const eventDay = new Date(date.getFullYear(), date.getMonth(), date.getDate());
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const tomorrow = new Date(today);
  tomorrow.setDate(tomorrow.getDate() + 1);

  // Format time
  const timeStr = date.toLocaleTimeString('en-US', {
    hour: 'numeric',
    minute: '2-digit',
  });

  // Check if today or tomorrow
  if (eventDay.getTime() === today.getTime()) {
    return { date: 'Today', time: timeStr };
  }
  if (eventDay.getTime() === tomorrow.getTime()) {
    return { date: 'Tomorrow', time: timeStr };
  }

  // Otherwise show full date
  const dateStr = date.toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
  });
  return { date: dateStr, time: timeStr };
}

export function TournamentGameCard({ match, onPress }: TournamentGameCardProps) {
  // Format date/time or show "Time TBD"
  const { date, time } = match.scheduledTime
    ? formatDateTime(match.scheduledTime)
    : { date: '', time: '' };

  return (
    <TouchableOpacity style={styles.card} onPress={onPress} activeOpacity={0.7}>
      {/* Gold left accent bar */}
      <View style={[styles.accent, { backgroundColor: GOLD_ACCENT }]} />

      <View style={styles.content}>
        {/* Header row with trophy icon + tournament name */}
        <View style={styles.headerRow}>
          <Ionicons name="trophy" size={16} color={GOLD_ACCENT} />
          <Text style={styles.tournamentName} numberOfLines={1} allowFontScaling={false}>
            {match.tournamentName}
          </Text>
        </View>

        {/* Opponent row */}
        <Text style={styles.opponent} numberOfLines={1} allowFontScaling={false}>
          vs {match.opponentTeamName || 'TBD'}
        </Text>

        {/* Date/time */}
        {match.scheduledTime ? (
          <Text style={styles.dateTime} allowFontScaling={false}>
            <Text style={styles.dateText}>{date}</Text>
            <Text style={styles.timeText}> at {time}</Text>
          </Text>
        ) : (
          <Text style={styles.timeTbd} allowFontScaling={false}>Time TBD</Text>
        )}

        {/* Venue */}
        {match.venue && (
          <Text style={styles.venue} numberOfLines={1} allowFontScaling={false}>
            {match.venue}
          </Text>
        )}

        {/* Status badge */}
        <View style={styles.footer}>
          <Badge variant={match.status === 'InProgress' ? 'green' : 'default'}>
            {match.status === 'InProgress' ? 'Live' : 'Scheduled'}
          </Badge>
        </View>
      </View>

      {/* Round indicator on right side */}
      <View style={styles.roundColumn}>
        <Text style={styles.roundLabel} allowFontScaling={false}>Round</Text>
        <Text style={styles.roundNumber} allowFontScaling={false}>{match.round}</Text>
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
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    marginBottom: spacing.sm,
  },
  tournamentName: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
    flex: 1,
  },
  opponent: {
    fontSize: 18,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  dateTime: {
    fontSize: 14,
    color: colors.text.primary,
    marginBottom: 2,
  },
  dateText: {
    fontWeight: '600',
  },
  timeText: {
    fontWeight: '400',
    color: colors.text.secondary,
  },
  timeTbd: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.muted,
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
  },
  roundColumn: {
    justifyContent: 'center',
    alignItems: 'center',
    paddingRight: 14,
    paddingLeft: spacing.sm,
    minWidth: 60,
  },
  roundLabel: {
    fontSize: 10,
    color: colors.text.subtle,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: 2,
  },
  roundNumber: {
    fontSize: 24,
    fontWeight: '700',
    color: GOLD_ACCENT,
  },
});
