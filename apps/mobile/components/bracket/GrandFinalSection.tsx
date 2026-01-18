import { View, Text, StyleSheet } from 'react-native';
import { colors, spacing, radius } from '../../theme';
import { GrandFinalMatchBox } from './GrandFinalMatchBox';
import { BracketResetIndicator } from './BracketResetIndicator';
import type { TournamentMatchDto } from '@bhmhockey/shared';

interface GrandFinalSectionProps {
  matches: TournamentMatchDto[];
  selectedTeamId?: string;
  onMatchPress?: (match: TournamentMatchDto) => void;
  onTeamPress?: (teamId: string) => void;
  canEdit?: boolean;
}

export function GrandFinalSection({
  matches,
  selectedTeamId,
  onMatchPress,
  onTeamPress,
  canEdit = false,
}: GrandFinalSectionProps) {
  if (!matches || matches.length === 0) {
    return null;
  }

  // GF1 is always the first grand final match
  const gf1 = matches.find(m => m.matchNumber === 1);
  const gf2 = matches.find(m => m.matchNumber === 2);

  if (!gf1) {
    return null;
  }

  // Determine if we should show GF2
  const shouldShowGF2 = () => {
    // Always show if GF2 has a status other than 'Scheduled' (it's been triggered)
    if (gf2 && gf2.status !== 'Scheduled') {
      return true;
    }

    // Show if GF1 is completed AND the away team (losers bracket champion) won
    if (gf1.status === 'Completed' || gf1.status === 'Forfeit') {
      return gf1.winnerTeamId === gf1.awayTeamId;
    }

    return false;
  };

  const showGF2 = shouldShowGF2();

  // Show bracket reset indicator if:
  // - GF1 is not yet completed
  // - We know the losers bracket champion's name (away team)
  const showResetIndicator =
    gf1.status === 'Scheduled' &&
    gf1.awayTeamName !== undefined &&
    gf1.awayTeamName !== '';

  return (
    <View style={styles.container}>
      {/* Gold divider */}
      <View style={styles.topDivider} />

      {/* Section header */}
      <View style={styles.headerContainer}>
        <Text style={styles.headerText} allowFontScaling={false}>GRAND FINALS</Text>
      </View>

      {/* Matches container */}
      <View style={styles.matchesContainer}>
        {/* GF1 */}
        <View style={styles.matchWrapper}>
          <GrandFinalMatchBox
            match={gf1}
            onPress={onMatchPress ? () => onMatchPress(gf1) : undefined}
            onTeamPress={onTeamPress}
            canEdit={canEdit}
            isGF1={true}
            isHighlighted={
              selectedTeamId
                ? gf1.homeTeamId === selectedTeamId || gf1.awayTeamId === selectedTeamId
                : false
            }
          />

          {/* Bracket reset indicator below GF1 */}
          {showResetIndicator && (
            <BracketResetIndicator
              losersTeamName={gf1.awayTeamName || ''}
              isVisible={true}
            />
          )}
        </View>

        {/* GF2 (bracket reset) */}
        {showGF2 && gf2 && (
          <View style={styles.matchWrapper}>
            <GrandFinalMatchBox
              match={gf2}
              onPress={onMatchPress ? () => onMatchPress(gf2) : undefined}
              onTeamPress={onTeamPress}
              canEdit={canEdit}
              isGF2={true}
              isHighlighted={
                selectedTeamId
                  ? gf2.homeTeamId === selectedTeamId || gf2.awayTeamId === selectedTeamId
                  : false
              }
            />
          </View>
        )}
      </View>

      {/* Bottom gold divider */}
      <View style={styles.bottomDivider} />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    marginVertical: spacing.lg,
  },
  topDivider: {
    height: 3,
    backgroundColor: '#FFD700', // Gold
    marginBottom: spacing.md,
    borderRadius: radius.sm,
  },
  bottomDivider: {
    height: 3,
    backgroundColor: '#FFD700', // Gold
    marginTop: spacing.md,
    borderRadius: radius.sm,
  },
  headerContainer: {
    alignItems: 'center',
    marginBottom: spacing.md,
  },
  headerText: {
    fontSize: 18,
    fontWeight: '700',
    color: '#FFD700', // Gold
    textTransform: 'uppercase',
    letterSpacing: 1.5,
    textShadowColor: 'rgba(255, 215, 0, 0.3)',
    textShadowOffset: { width: 0, height: 2 },
    textShadowRadius: 4,
  },
  matchesContainer: {
    alignItems: 'center',
    gap: spacing.md,
  },
  matchWrapper: {
    width: '100%',
    maxWidth: 400,
    alignItems: 'center',
  },
});
