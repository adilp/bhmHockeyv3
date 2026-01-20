import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { colors, spacing, radius } from '../../theme';
import type { TournamentMatchDto } from '@bhmhockey/shared';

interface LosersBracketMatchBoxProps {
  match: TournamentMatchDto;
  onPress?: () => void;
  onTeamPress?: (teamId: string) => void;
  canEdit?: boolean;
  fromWinnersText?: string;
  lossCount?: { homeTeam: number; awayTeam: number };
  isHighlighted?: boolean;
}

interface TeamRowProps {
  teamName?: string;
  teamId?: string;
  seed?: number;
  score?: number;
  isWinner: boolean;
  isBye?: boolean;
  lossCount?: number;
  onPress?: () => void;
}

function TeamRow({ teamName, teamId, seed, score, isWinner, isBye, lossCount, onPress }: TeamRowProps) {
  const displayName = isBye
    ? 'BYE'
    : teamName
      ? seed
        ? `#${seed} ${teamName}`
        : teamName
      : 'TBD';

  const scoreDisplay = score !== undefined && score !== null ? String(score) : '-';
  const isEliminated = lossCount !== undefined && lossCount >= 2;

  const content = (
    <View style={[styles.teamRow, isWinner && styles.winnerRow]}>
      <View style={styles.teamNameContainer}>
        <Text
          style={[
            styles.teamName,
            isWinner && styles.winnerText,
            isBye && styles.byeText,
            !teamName && !isBye && styles.tbdText,
            isEliminated && styles.eliminatedText,
          ]}
          numberOfLines={1}
          allowFontScaling={false}
        >
          {displayName}
        </Text>
        {lossCount !== undefined && (
          <View style={[styles.lossChip, isEliminated && styles.lossChipEliminated]}>
            <Text style={[styles.lossChipText, isEliminated && styles.lossChipTextEliminated]} allowFontScaling={false}>
              L:{lossCount}
            </Text>
          </View>
        )}
      </View>
      <Text style={[styles.score, isWinner && styles.winnerText]} allowFontScaling={false}>
        {isBye ? '-' : scoreDisplay}
      </Text>
    </View>
  );

  // Make team row tappable if onPress is provided and team exists
  if (onPress && teamId && teamName) {
    return (
      <TouchableOpacity onPress={onPress} activeOpacity={0.7}>
        {content}
      </TouchableOpacity>
    );
  }

  return content;
}

export function LosersBracketMatchBox({
  match,
  onPress,
  onTeamPress,
  canEdit = false,
  fromWinnersText,
  lossCount,
  isHighlighted = false,
}: LosersBracketMatchBoxProps) {
  const isCompleted = match.status === 'Completed' || match.status === 'Forfeit';
  const homeIsWinner = isCompleted && match.winnerTeamId === match.homeTeamId;
  const awayIsWinner = isCompleted && match.winnerTeamId === match.awayTeamId;

  const content = (
    <View style={[styles.container, isHighlighted && styles.highlightedContainer]}>
      {/* Match header */}
      <View style={styles.header}>
        <Text style={styles.matchNumber} allowFontScaling={false}>Match {match.matchNumber}</Text>
        {match.status === 'Forfeit' && (
          <Text style={styles.forfeitLabel} allowFontScaling={false}>FORFEIT</Text>
        )}
        {match.status === 'InProgress' && (
          <View style={styles.liveIndicator}>
            <Text style={styles.liveText} allowFontScaling={false}>LIVE</Text>
          </View>
        )}
      </View>

      {/* From Winners Text */}
      {fromWinnersText && (
        <View style={styles.fromWinnersContainer}>
          <Text style={styles.fromWinnersText} allowFontScaling={false}>{fromWinnersText}</Text>
        </View>
      )}

      {/* BYE match */}
      {match.isBye ? (
        <View style={styles.byeContainer}>
          <TeamRow
            teamName={match.homeTeamName}
            teamId={match.homeTeamId}
            isWinner={false}
            isBye={false}
            lossCount={lossCount?.homeTeam}
            onPress={match.homeTeamId && onTeamPress ? () => onTeamPress(match.homeTeamId!) : undefined}
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
            teamId={match.homeTeamId}
            score={match.homeScore}
            isWinner={homeIsWinner}
            lossCount={lossCount?.homeTeam}
            onPress={match.homeTeamId && onTeamPress ? () => onTeamPress(match.homeTeamId!) : undefined}
          />

          <View style={styles.divider} />

          {/* Away team */}
          <TeamRow
            teamName={match.awayTeamName}
            teamId={match.awayTeamId}
            score={match.awayScore}
            isWinner={awayIsWinner}
            lossCount={lossCount?.awayTeam}
            onPress={match.awayTeamId && onTeamPress ? () => onTeamPress(match.awayTeamId!) : undefined}
          />
        </>
      )}

      {/* Venue and time if scheduled */}
      {match.scheduledTime && (
        <View style={styles.scheduleInfo}>
          <Text style={styles.scheduleText} allowFontScaling={false}>
            {new Date(match.scheduledTime).toLocaleString('en-US', {
              month: 'short',
              day: 'numeric',
              hour: 'numeric',
              minute: '2-digit',
            })}
          </Text>
          {match.venue && (
            <Text style={styles.venueText} numberOfLines={1} allowFontScaling={false}>
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
    // No minWidth - let parent control width for compact bracket layout
  },
  highlightedContainer: {
    borderColor: colors.primary.teal,
    borderWidth: 2,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.xs,
    paddingVertical: 3,
    backgroundColor: colors.bg.elevated,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  matchNumber: {
    fontSize: 9,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.3,
  },
  forfeitLabel: {
    fontSize: 8,
    fontWeight: '700',
    color: colors.status.error,
    textTransform: 'uppercase',
  },
  liveIndicator: {
    backgroundColor: colors.status.errorSubtle,
    paddingHorizontal: 3,
    paddingVertical: 1,
    borderRadius: radius.sm,
  },
  liveText: {
    fontSize: 8,
    fontWeight: '700',
    color: colors.status.error,
  },
  fromWinnersContainer: {
    paddingHorizontal: spacing.xs,
    paddingTop: 2,
    paddingBottom: 1,
    backgroundColor: colors.bg.elevated,
  },
  fromWinnersText: {
    fontSize: 8,
    color: colors.text.subtle,
    fontStyle: 'italic',
  },
  teamRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.xs,
    paddingVertical: 4,
  },
  winnerRow: {
    backgroundColor: colors.subtle.green,
  },
  teamNameContainer: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    marginRight: 2,
  },
  teamName: {
    fontSize: 11,
    fontWeight: '500',
    color: colors.text.secondary,
    marginRight: 2,
    flex: 1,
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
  eliminatedText: {
    textDecorationLine: 'line-through',
    color: colors.text.subtle,
  },
  lossChip: {
    backgroundColor: colors.bg.elevated,
    paddingHorizontal: 3,
    paddingVertical: 1,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  lossChipEliminated: {
    backgroundColor: colors.status.errorSubtle,
    borderColor: colors.status.error,
  },
  lossChipText: {
    fontSize: 8,
    fontWeight: '600',
    color: colors.text.muted,
  },
  lossChipTextEliminated: {
    color: colors.status.error,
  },
  score: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.secondary,
    minWidth: 16,
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
    paddingHorizontal: spacing.xs,
    paddingVertical: 3,
    backgroundColor: colors.bg.elevated,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  scheduleText: {
    fontSize: 9,
    color: colors.text.muted,
  },
  venueText: {
    fontSize: 9,
    color: colors.text.subtle,
    marginTop: 1,
  },
});
