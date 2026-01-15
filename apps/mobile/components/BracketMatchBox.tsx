import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { colors, spacing, radius } from '../theme';
import type { TournamentMatchDto } from '@bhmhockey/shared';

interface BracketMatchBoxProps {
  match: TournamentMatchDto;
  onPress?: () => void;  // For score entry
  canEdit?: boolean;
}

interface TeamRowProps {
  teamName?: string;
  seed?: number;
  score?: number;
  isWinner: boolean;
  isBye?: boolean;
}

function TeamRow({ teamName, seed, score, isWinner, isBye }: TeamRowProps) {
  const displayName = isBye
    ? 'BYE'
    : teamName
      ? seed
        ? `#${seed} ${teamName}`
        : teamName
      : 'TBD';

  const scoreDisplay = score !== undefined && score !== null ? String(score) : '-';

  return (
    <View style={[styles.teamRow, isWinner && styles.winnerRow]}>
      <Text
        style={[
          styles.teamName,
          isWinner && styles.winnerText,
          isBye && styles.byeText,
          !teamName && !isBye && styles.tbdText,
        ]}
        numberOfLines={1}
      >
        {displayName}
      </Text>
      <Text style={[styles.score, isWinner && styles.winnerText]}>
        {isBye ? '-' : scoreDisplay}
      </Text>
    </View>
  );
}

export function BracketMatchBox({ match, onPress, canEdit = false }: BracketMatchBoxProps) {
  const isCompleted = match.status === 'Completed' || match.status === 'Forfeit';
  const homeIsWinner = isCompleted && match.winnerTeamId === match.homeTeamId;
  const awayIsWinner = isCompleted && match.winnerTeamId === match.awayTeamId;

  // Parse seed from bracketPosition if available (format: "R1M1" or similar)
  // For now, we don't have seeds in the DTO directly, so we won't display them
  // This can be enhanced when seed info is added to TournamentMatchDto

  const content = (
    <View style={styles.container}>
      {/* Match header */}
      <View style={styles.header}>
        <Text style={styles.matchNumber}>Match {match.matchNumber}</Text>
        {match.status === 'Forfeit' && (
          <Text style={styles.forfeitLabel}>FORFEIT</Text>
        )}
        {match.status === 'InProgress' && (
          <View style={styles.liveIndicator}>
            <Text style={styles.liveText}>LIVE</Text>
          </View>
        )}
      </View>

      {/* BYE match */}
      {match.isBye ? (
        <View style={styles.byeContainer}>
          <TeamRow
            teamName={match.homeTeamName}
            isWinner={false}
            isBye={false}
          />
          <View style={styles.divider} />
          <TeamRow
            teamName={undefined}
            isWinner={false}
            isBye={true}
          />
        </View>
      ) : (
        <>
          {/* Home team */}
          <TeamRow
            teamName={match.homeTeamName}
            score={match.homeScore}
            isWinner={homeIsWinner}
          />

          <View style={styles.divider} />

          {/* Away team */}
          <TeamRow
            teamName={match.awayTeamName}
            score={match.awayScore}
            isWinner={awayIsWinner}
          />
        </>
      )}

      {/* Venue and time if scheduled */}
      {match.scheduledTime && (
        <View style={styles.scheduleInfo}>
          <Text style={styles.scheduleText}>
            {new Date(match.scheduledTime).toLocaleString('en-US', {
              month: 'short',
              day: 'numeric',
              hour: 'numeric',
              minute: '2-digit',
            })}
          </Text>
          {match.venue && (
            <Text style={styles.venueText} numberOfLines={1}>
              {match.venue}
            </Text>
          )}
        </View>
      )}
    </View>
  );

  if (onPress && canEdit) {
    return (
      <TouchableOpacity onPress={onPress} activeOpacity={0.7}>
        {content}
      </TouchableOpacity>
    );
  }

  return content;
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    overflow: 'hidden',
    minWidth: 180,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    backgroundColor: colors.bg.elevated,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  matchNumber: {
    fontSize: 10,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  forfeitLabel: {
    fontSize: 9,
    fontWeight: '700',
    color: colors.status.error,
    textTransform: 'uppercase',
  },
  liveIndicator: {
    backgroundColor: colors.status.errorSubtle,
    paddingHorizontal: spacing.xs,
    paddingVertical: 2,
    borderRadius: radius.sm,
  },
  liveText: {
    fontSize: 9,
    fontWeight: '700',
    color: colors.status.error,
  },
  teamRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.sm,
  },
  winnerRow: {
    backgroundColor: colors.subtle.green,
  },
  teamName: {
    fontSize: 13,
    fontWeight: '500',
    color: colors.text.secondary,
    flex: 1,
    marginRight: spacing.sm,
  },
  winnerText: {
    color: colors.primary.green,
    fontWeight: '700',
  },
  byeText: {
    color: colors.text.muted,
    fontStyle: 'italic',
  },
  tbdText: {
    color: colors.text.subtle,
    fontStyle: 'italic',
  },
  score: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.secondary,
    minWidth: 24,
    textAlign: 'right',
  },
  divider: {
    height: 1,
    backgroundColor: colors.border.default,
  },
  byeContainer: {
    // Same as regular container, just semantic wrapper
  },
  scheduleInfo: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    backgroundColor: colors.bg.elevated,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  scheduleText: {
    fontSize: 10,
    color: colors.text.muted,
  },
  venueText: {
    fontSize: 10,
    color: colors.text.subtle,
    marginTop: 2,
  },
});
