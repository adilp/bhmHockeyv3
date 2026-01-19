import React, { useMemo } from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { colors, spacing } from '../../theme';
import { LosersBracketMatchBox } from './LosersBracketMatchBox';
import { BracketConnector } from './BracketConnector';
import {
  generateMatchPositionsMap,
  calculateBracketDimensions,
  getFromWinnersRoundText,
  getTeamLossCount,
  MATCH_BOX_WIDTH,
  MATCH_BOX_HEIGHT,
} from '../../utils/bracketUtils';
import type { TournamentMatchDto } from '@bhmhockey/shared';

interface LosersBracketProps {
  matches: TournamentMatchDto[]; // Filtered to Losers bracket matches
  allMatches: TournamentMatchDto[]; // All matches (needed for loss count and fromWinners text)
  selectedTeamId?: string;
  onMatchPress?: (match: TournamentMatchDto) => void;
  onTeamPress?: (teamId: string) => void;
  canEdit?: boolean;
}

export function LosersBracket({
  matches,
  allMatches,
  selectedTeamId,
  onMatchPress,
  onTeamPress,
  canEdit = false,
}: LosersBracketProps) {
  // Calculate positions for all matches
  const positions = useMemo(
    () => generateMatchPositionsMap(matches, 'Losers'),
    [matches]
  );

  // Calculate bracket dimensions
  const dimensions = useMemo(
    () => calculateBracketDimensions(matches, 'Losers'),
    [matches]
  );

  // Build connector data
  const connectors = useMemo(() => {
    const result: Array<{
      fromMatch: TournamentMatchDto;
      toMatch: TournamentMatchDto;
      isHighlighted: boolean;
    }> = [];

    matches.forEach((match) => {
      // Find the match that this match's winner advances to
      if (match.nextMatchId) {
        const nextMatch = matches.find((m) => m.id === match.nextMatchId);
        if (nextMatch) {
          const isHighlighted =
            selectedTeamId &&
            (match.homeTeamId === selectedTeamId || match.awayTeamId === selectedTeamId) &&
            match.status === 'Completed' &&
            match.winnerTeamId === selectedTeamId;

          result.push({
            fromMatch: match,
            toMatch: nextMatch,
            isHighlighted: !!isHighlighted,
          });
        }
      }
    });

    return result;
  }, [matches, selectedTeamId]);

  // Empty state
  if (matches.length === 0) {
    return null;
  }

  return (
    <View style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.headerText} allowFontScaling={false}>LOSERS BRACKET</Text>
      </View>

      {/* Bracket visualization */}
      <View
        style={[
          styles.bracketContainer,
          {
            width: dimensions.width,
            height: dimensions.height,
          },
        ]}
      >
        {/* Draw connectors first (behind match boxes) */}
        {connectors.map((conn, index) => {
          const fromPos = positions.get(conn.fromMatch.id);
          const toPos = positions.get(conn.toMatch.id);

          if (!fromPos || !toPos) return null;

          return (
            <BracketConnector
              key={`connector-${index}`}
              fromX={fromPos.x + MATCH_BOX_WIDTH}
              fromY={fromPos.y + MATCH_BOX_HEIGHT / 2}
              toX={toPos.x}
              toY={toPos.y + MATCH_BOX_HEIGHT / 2}
              isHighlighted={conn.isHighlighted}
            />
          );
        })}

        {/* Draw match boxes */}
        {matches.map((match) => {
          const pos = positions.get(match.id);
          if (!pos) return null;

          // Calculate loss counts for both teams
          const lossCount = {
            homeTeam: match.homeTeamId
              ? getTeamLossCount(match.homeTeamId, allMatches)
              : 0,
            awayTeam: match.awayTeamId
              ? getTeamLossCount(match.awayTeamId, allMatches)
              : 0,
          };

          // Get "from winners" context text
          const fromWinnersText = getFromWinnersRoundText(match, allMatches);

          // Check if this match involves the selected team
          const isHighlighted =
            !!selectedTeamId &&
            (match.homeTeamId === selectedTeamId ||
              match.awayTeamId === selectedTeamId);

          return (
            <View
              key={match.id}
              style={[
                styles.matchBox,
                {
                  left: pos.x,
                  top: pos.y,
                  width: MATCH_BOX_WIDTH,
                },
              ]}
            >
              <LosersBracketMatchBox
                match={match}
                onPress={onMatchPress ? () => onMatchPress(match) : undefined}
                onTeamPress={onTeamPress}
                canEdit={canEdit}
                fromWinnersText={fromWinnersText || undefined}
                lossCount={lossCount}
                isHighlighted={isHighlighted}
              />
            </View>
          );
        })}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    // Container for the entire losers bracket section
  },
  header: {
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    backgroundColor: colors.bg.elevated,
    borderBottomWidth: 2,
    borderBottomColor: colors.status.warning,
  },
  headerText: {
    fontSize: 16,
    fontWeight: '700',
    color: colors.status.warning,
    textTransform: 'uppercase',
    letterSpacing: 1,
  },
  bracketContainer: {
    position: 'relative',
    paddingTop: spacing.lg,
    paddingBottom: spacing.lg,
  },
  matchBox: {
    position: 'absolute',
  },
});

export default LosersBracket;
