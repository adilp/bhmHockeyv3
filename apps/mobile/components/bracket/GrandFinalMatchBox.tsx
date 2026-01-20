import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing, radius } from '../../theme';
import type { TournamentMatchDto } from '@bhmhockey/shared';

interface GrandFinalMatchBoxProps {
  match: TournamentMatchDto;
  onPress?: () => void;
  onTeamPress?: (teamId: string) => void;
  canEdit?: boolean;
  isGF1?: boolean;
  isGF2?: boolean;
  isHighlighted?: boolean;
}

interface TeamRowProps {
  teamName?: string;
  teamId?: string;
  seed?: number;
  score?: number;
  isWinner: boolean;
  isBye?: boolean;
  onPress?: () => void;
}

function TeamRow({ teamName, teamId, seed, score, isWinner, isBye, onPress }: TeamRowProps) {
  const displayName = isBye
    ? 'BYE'
    : teamName
      ? seed
        ? `#${seed} ${teamName}`
        : teamName
      : 'TBD';

  const scoreDisplay = score !== undefined && score !== null ? String(score) : '-';

  const content = (
    <View style={[styles.teamRow, isWinner && styles.winnerRow]}>
      <Text
        style={[
          styles.teamName,
          isWinner && styles.winnerText,
          isBye && styles.byeText,
          !teamName && !isBye && styles.tbdText,
        ]}
        numberOfLines={1}
        allowFontScaling={false}
      >
        {displayName}
      </Text>
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

export function GrandFinalMatchBox({
  match,
  onPress,
  onTeamPress,
  canEdit = false,
  isGF1 = false,
  isGF2 = false,
  isHighlighted = false,
}: GrandFinalMatchBoxProps) {
  const isCompleted = match.status === 'Completed' || match.status === 'Forfeit';
  const homeIsWinner = isCompleted && match.winnerTeamId === match.homeTeamId;
  const awayIsWinner = isCompleted && match.winnerTeamId === match.awayTeamId;

  const headerText = isGF2 ? 'GRAND FINAL 2 (Reset)' : 'GRAND FINAL';

  const content = (
    <View style={[styles.container, isHighlighted && styles.highlightedContainer]}>
      {/* Grand Final Header */}
      <View style={styles.header}>
        <View style={styles.headerLeft}>
          <Ionicons name="trophy" size={12} color="#FFD700" style={styles.trophyIcon} />
          <Text style={styles.matchNumber} allowFontScaling={false}>{headerText}</Text>
        </View>
        {match.status === 'Forfeit' && (
          <Text style={styles.forfeitLabel} allowFontScaling={false}>FORFEIT</Text>
        )}
        {match.status === 'InProgress' && (
          <View style={styles.liveIndicator}>
            <Text style={styles.liveText} allowFontScaling={false}>LIVE</Text>
          </View>
        )}
      </View>

      {/* BYE match */}
      {match.isBye ? (
        <View style={styles.byeContainer}>
          <TeamRow
            teamName={match.homeTeamName}
            teamId={match.homeTeamId}
            isWinner={false}
            isBye={false}
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
            onPress={match.homeTeamId && onTeamPress ? () => onTeamPress(match.homeTeamId!) : undefined}
          />

          <View style={styles.divider} />

          {/* Away team */}
          <TeamRow
            teamName={match.awayTeamName}
            teamId={match.awayTeamId}
            score={match.awayScore}
            isWinner={awayIsWinner}
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
    backgroundColor: 'rgba(255, 215, 0, 0.1)', // Gold tint
    borderRadius: radius.md,
    borderWidth: 2,
    borderColor: '#FFD700', // Gold border
    overflow: 'hidden',
    // No minWidth - let parent control width
  },
  highlightedContainer: {
    borderColor: '#FFD700',
    borderWidth: 3,
    shadowColor: '#FFD700',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.5,
    shadowRadius: 6,
    elevation: 6,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.xs,
    paddingVertical: 3,
    backgroundColor: colors.bg.elevated,
    borderBottomWidth: 1,
    borderBottomColor: '#FFD700',
  },
  headerLeft: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  trophyIcon: {
    marginRight: 3,
  },
  matchNumber: {
    fontSize: 9,
    fontWeight: '700',
    color: '#FFD700',
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
  teamRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.xs,
    paddingVertical: spacing.xs,
  },
  winnerRow: {
    backgroundColor: 'rgba(255, 215, 0, 0.2)', // Stronger gold tint for winner
  },
  teamName: {
    fontSize: 12,
    fontWeight: '500',
    color: colors.text.secondary,
    flex: 1,
    marginRight: spacing.xs,
  },
  winnerText: {
    color: '#FFD700', // Gold for winner
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
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
    minWidth: 20,
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
