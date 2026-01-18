import React, { useMemo } from 'react';
import { View, Text, StyleSheet, ScrollView } from 'react-native';
import { TournamentMatchDto } from '@bhmhockey/shared';
import { BracketMatchBox } from '../BracketMatchBox';
import { BracketConnector } from './BracketConnector';
import { colors, spacing, typography } from '../../theme';
import {
  generateMatchPositionsMap,
  calculateBracketDimensions,
  MATCH_BOX_WIDTH,
  MATCH_BOX_HEIGHT,
} from '../../utils/bracketUtils';

interface WinnersBracketProps {
  matches: TournamentMatchDto[]; // Filtered to only Winners bracket matches
  selectedTeamId?: string; // For highlighting team's path
  onMatchPress?: (match: TournamentMatchDto) => void;
  onTeamPress?: (teamId: string) => void; // For team selection
  canEdit?: boolean;
}

export const WinnersBracket: React.FC<WinnersBracketProps> = ({
  matches,
  selectedTeamId,
  onMatchPress,
  onTeamPress,
  canEdit = false,
}) => {
  // Calculate positions for all matches
  const matchPositions = useMemo(
    () => generateMatchPositionsMap(matches, 'Winners'),
    [matches]
  );

  // Calculate bracket dimensions
  const { width, height } = useMemo(
    () => calculateBracketDimensions(matches, 'Winners'),
    [matches]
  );

  // Group matches by round for optional round labels
  const matchesByRound = useMemo(() => {
    const grouped = new Map<number, TournamentMatchDto[]>();
    matches.forEach((match) => {
      const roundMatches = grouped.get(match.round) || [];
      roundMatches.push(match);
      grouped.set(match.round, roundMatches);
    });
    return grouped;
  }, [matches]);

  // Determine round labels
  const maxRound = matches.length > 0 ? Math.max(...matches.map((m) => m.round)) : 0;
  const getRoundLabel = (round: number): string => {
    if (round === maxRound) return 'Finals';
    if (round === maxRound - 1) return 'Semifinals';
    return `Round ${round}`;
  };

  // Check if a match should be highlighted (contains selected team)
  const isMatchHighlighted = (match: TournamentMatchDto): boolean => {
    if (!selectedTeamId) return false;
    return match.homeTeamId === selectedTeamId || match.awayTeamId === selectedTeamId;
  };

  // Check if a connector should be highlighted (connects two highlighted matches)
  const isConnectorHighlighted = (fromMatch: TournamentMatchDto, toMatch: TournamentMatchDto): boolean => {
    if (!selectedTeamId) return false;
    return isMatchHighlighted(fromMatch) && isMatchHighlighted(toMatch);
  };

  if (matches.length === 0) {
    return (
      <View style={styles.emptyContainer}>
        <Text style={styles.emptyText} allowFontScaling={false}>No matches in Winners Bracket</Text>
      </View>
    );
  }

  // Add padding to dimensions for header and spacing
  const HEADER_HEIGHT = 40;
  const PADDING = 20;
  const containerWidth = width + PADDING * 2;
  const containerHeight = height + HEADER_HEIGHT + PADDING * 2;

  return (
    <View style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.headerText} allowFontScaling={false}>WINNERS BRACKET</Text>
      </View>

      {/* Scrollable bracket area */}
      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={true}
        contentContainerStyle={[
          styles.scrollContent,
          {
            width: containerWidth,
            height: containerHeight,
          },
        ]}
      >
        <View
          style={[
            styles.bracketContainer,
            {
              width: containerWidth,
              height: containerHeight,
            },
          ]}
        >
          {/* Round labels */}
          {Array.from(matchesByRound.keys())
            .sort((a, b) => a - b)
            .map((round) => {
              const roundMatches = matchesByRound.get(round) || [];
              if (roundMatches.length === 0) return null;

              // Get first match position to determine X coordinate
              const firstMatch = roundMatches[0];
              const position = matchPositions.get(firstMatch.id);
              if (!position) return null;

              return (
                <View
                  key={`round-label-${round}`}
                  style={[
                    styles.roundLabel,
                    {
                      left: PADDING + position.x,
                      top: HEADER_HEIGHT,
                    },
                  ]}
                >
                  <Text style={styles.roundLabelText} allowFontScaling={false}>{getRoundLabel(round)}</Text>
                </View>
              );
            })}

          {/* Match boxes */}
          {matches.map((match) => {
            const position = matchPositions.get(match.id);
            if (!position) return null;

            return (
              <View
                key={match.id}
                style={[
                  styles.matchBox,
                  {
                    left: PADDING + position.x,
                    top: HEADER_HEIGHT + PADDING + position.y,
                    width: MATCH_BOX_WIDTH,
                    height: MATCH_BOX_HEIGHT,
                  },
                ]}
              >
                <BracketMatchBox
                  match={match}
                  onPress={onMatchPress ? () => onMatchPress(match) : undefined}
                  onTeamPress={onTeamPress}
                  canEdit={canEdit}
                  isHighlighted={isMatchHighlighted(match)}
                />
              </View>
            );
          })}

          {/* Connector lines */}
          {matches.map((match) => {
            if (!match.nextMatchId) return null;

            const toMatch = matches.find((m) => m.id === match.nextMatchId);
            if (!toMatch) return null;

            const fromPos = matchPositions.get(match.id);
            const toPos = matchPositions.get(toMatch.id);
            if (!fromPos || !toPos) return null;

            // Calculate connector endpoints
            const fromX = PADDING + fromPos.x + MATCH_BOX_WIDTH;
            const fromY = HEADER_HEIGHT + PADDING + fromPos.y + MATCH_BOX_HEIGHT / 2;
            const toX = PADDING + toPos.x;
            const toY = HEADER_HEIGHT + PADDING + toPos.y + MATCH_BOX_HEIGHT / 2;

            return (
              <BracketConnector
                key={`connector-${match.id}-${toMatch.id}`}
                fromX={fromX}
                fromY={fromY}
                toX={toX}
                toY={toY}
                isHighlighted={isConnectorHighlighted(match, toMatch)}
                isLoserPath={false}
              />
            );
          })}
        </View>
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  header: {
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    backgroundColor: colors.bg.elevated,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  headerText: {
    ...typography.sectionTitle,
    fontSize: 12,
    letterSpacing: 1,
  },
  scrollContent: {
    minWidth: '100%',
  },
  bracketContainer: {
    position: 'relative',
  },
  matchBox: {
    position: 'absolute',
  },
  roundLabel: {
    position: 'absolute',
    paddingVertical: spacing.xs,
  },
  roundLabelText: {
    fontSize: 11,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.xl,
  },
  emptyText: {
    fontSize: 14,
    color: colors.text.muted,
  },
});

export default WinnersBracket;
