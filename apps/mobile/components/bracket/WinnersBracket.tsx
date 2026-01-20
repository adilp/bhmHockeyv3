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
  userTeamId?: string; // Current user's team - always highlighted in teal
  currentRound?: number | null; // Round with active matches - label shown in white
  onMatchPress?: (match: TournamentMatchDto) => void;
  onTeamPress?: (teamId: string) => void; // For team selection
  canEdit?: boolean;
  showHeader?: boolean; // Whether to show "WINNERS BRACKET" header
}

export const WinnersBracket: React.FC<WinnersBracketProps> = ({
  matches,
  selectedTeamId,
  userTeamId,
  currentRound,
  onMatchPress,
  onTeamPress,
  canEdit = false,
  showHeader = true,
}) => {
  // Use userTeamId as the effective highlighted team (user's team is always highlighted)
  const effectiveSelectedTeamId = userTeamId || selectedTeamId;
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

  // Check if a match should be highlighted (contains user's team)
  const isMatchHighlighted = (match: TournamentMatchDto): boolean => {
    if (!effectiveSelectedTeamId) return false;
    return match.homeTeamId === effectiveSelectedTeamId || match.awayTeamId === effectiveSelectedTeamId;
  };

  // Check if a connector should be highlighted (connects two highlighted matches)
  const isConnectorHighlighted = (fromMatch: TournamentMatchDto, toMatch: TournamentMatchDto): boolean => {
    if (!effectiveSelectedTeamId) return false;
    return isMatchHighlighted(fromMatch) && isMatchHighlighted(toMatch);
  };

  // Check if a round is the current active round
  const isCurrentRound = (round: number): boolean => {
    return currentRound !== null && currentRound !== undefined && round === currentRound;
  };

  if (matches.length === 0) {
    return (
      <View style={styles.emptyContainer}>
        <Text style={styles.emptyText} allowFontScaling={false}>No matches in Winners Bracket</Text>
      </View>
    );
  }

  // Add padding to dimensions for header and spacing
  // Reduced padding to fit more rounds horizontally (QF, SF, Finals)
  const HEADER_HEIGHT = showHeader ? 36 : 0;
  const PADDING_H = 8;  // Horizontal padding - minimal to fit more rounds
  const PADDING_V = 28; // Vertical padding - extra room for round labels at top
  const ROUND_LABEL_HEIGHT = 20; // Space for round labels above matches
  const containerWidth = width + PADDING_H * 2;
  const containerHeight = height + HEADER_HEIGHT + PADDING_V * 2 + ROUND_LABEL_HEIGHT;

  return (
    <View style={styles.container}>
      {/* Header */}
      {showHeader && (
        <View style={styles.header}>
          <Text style={styles.headerText} allowFontScaling={false}>WINNERS BRACKET</Text>
        </View>
      )}

      {/* Scrollable bracket area - vertical scroll, content fits horizontally */}
      <ScrollView
        showsVerticalScrollIndicator={true}
        contentContainerStyle={[
          styles.scrollContent,
          {
            minHeight: containerHeight,
          },
        ]}
      >
        <View
          style={[
            styles.bracketContainer,
            {
              minHeight: containerHeight,
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

              const isCurrent = isCurrentRound(round);
              return (
                <View
                  key={`round-label-${round}`}
                  style={[
                    styles.roundLabel,
                    {
                      left: PADDING_H + position.x,
                      top: HEADER_HEIGHT + PADDING_V,
                    },
                  ]}
                >
                  <Text
                    style={[
                      styles.roundLabelText,
                      isCurrent && styles.roundLabelTextCurrent,
                    ]}
                    allowFontScaling={false}
                  >
                    {getRoundLabel(round)}
                  </Text>
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
                    left: PADDING_H + position.x,
                    top: HEADER_HEIGHT + PADDING_V + ROUND_LABEL_HEIGHT + position.y,
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
                  userTeamId={userTeamId}
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
            const fromX = PADDING_H + fromPos.x + MATCH_BOX_WIDTH;
            const fromY = HEADER_HEIGHT + PADDING_V + ROUND_LABEL_HEIGHT + fromPos.y + MATCH_BOX_HEIGHT / 2;
            const toX = PADDING_H + toPos.x;
            const toY = HEADER_HEIGHT + PADDING_V + ROUND_LABEL_HEIGHT + toPos.y + MATCH_BOX_HEIGHT / 2;

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
    paddingVertical: spacing.xs,
    paddingHorizontal: spacing.sm,
    backgroundColor: colors.bg.elevated,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  headerText: {
    ...typography.sectionTitle,
    fontSize: 11,
    letterSpacing: 0.5,
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
    paddingVertical: 2,
  },
  roundLabelText: {
    fontSize: 10,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.3,
  },
  roundLabelTextCurrent: {
    color: colors.text.primary,
    fontWeight: '700',
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
