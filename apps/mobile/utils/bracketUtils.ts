/**
 * Bracket Utilities for Double Elimination Tournament Visualization
 *
 * These utilities help position and draw tournament brackets for React Native.
 * Designed for use with TournamentMatchDto types from @bhmhockey/shared.
 */

import { TournamentMatchDto } from '@bhmhockey/shared';

// ============================================
// CONSTANTS
// ============================================

/**
 * Width of a single match box in pixels
 * Narrower to fit more rounds (QF, SF, Finals) on screen without horizontal scroll
 */
export const MATCH_BOX_WIDTH = 105;

/**
 * Height of a single match box in pixels
 * Must account for: header (~18px) + 2 team rows (~28px each) + schedule info (~28px) = ~100px
 */
export const MATCH_BOX_HEIGHT = 100;

/**
 * Horizontal spacing between rounds in pixels
 * Reduced to fit 3 rounds (QF, SF, Finals) on screen without horizontal scroll
 */
export const ROUND_SPACING = 16;

/**
 * Base vertical spacing between matches in round 1
 * This doubles with each subsequent round to create bracket tree structure
 */
export const MATCH_SPACING = 12;

// ============================================
// TYPES
// ============================================

/**
 * 2D coordinate position for a match box
 */
export interface MatchPosition {
  x: number;
  y: number;
}

/**
 * Dimensions of a bracket section
 */
export interface BracketDimensions {
  width: number;
  height: number;
}

/**
 * Grouped matches by bracket type
 */
export interface GroupedMatches {
  winners: TournamentMatchDto[];
  losers: TournamentMatchDto[];
  grandFinal: TournamentMatchDto[];
}

// ============================================
// POSITION CALCULATION
// ============================================

/**
 * Calculates the (x, y) position for a match box in the bracket visualization.
 * Creates a proper bracket tree structure where later rounds are centered
 * between their feeder matches.
 *
 * @param round - Round number (1-based)
 * @param matchNumber - Match number within the round (1-based)
 * @param totalMatchesInRound - Total number of matches in this round
 * @param bracketType - Type of bracket ('Winners', 'Losers', or 'GrandFinal')
 * @param maxMatchesFirstRound - Maximum matches in round 1 (for proper centering)
 * @returns {x, y} coordinates in pixels for positioning the match box
 */
export function calculateMatchPosition(
  round: number,
  matchNumber: number,
  totalMatchesInRound: number,
  bracketType: 'Winners' | 'Losers' | 'GrandFinal' = 'Winners',
  maxMatchesFirstRound: number = 4
): MatchPosition {
  // X position: increases with each round
  const x = (round - 1) * (MATCH_BOX_WIDTH + ROUND_SPACING);

  // Slot height is the vertical space each round 1 match occupies
  const slotHeight = MATCH_BOX_HEIGHT + MATCH_SPACING;

  // Match index (0-based)
  const matchIndex = matchNumber - 1;

  // Multiplier doubles with each round to create tree structure
  // Round 1: 1x, Round 2: 2x, Round 3: 4x, etc.
  const multiplier = Math.pow(2, round - 1);

  // Offset centers matches between their feeder matches
  // Round 1: 0, Round 2: slotHeight/2, Round 3: 1.5*slotHeight, etc.
  const offset = (multiplier - 1) * slotHeight / 2;

  // Calculate Y position
  const y = matchIndex * multiplier * slotHeight + offset;

  // Grand Final positioning: add extra spacing
  if (bracketType === 'GrandFinal') {
    return {
      x: x + ROUND_SPACING,
      y: y,
    };
  }

  return { x, y };
}

// ============================================
// CONNECTOR DRAWING
// ============================================

/**
 * Generates an SVG path string to draw a connector line from one match to the next.
 * Creates an "S-curve" or "step" line connecting the winner output to the next match input.
 *
 * @param fromMatch - The source match
 * @param toMatch - The destination match
 * @param matchPositions - Map of match IDs to their {x, y} positions
 * @returns SVG path string (e.g., "M 10 20 L 30 40 L 50 60")
 *
 * @example
 * const path = getConnectorPath(match1, match2, positions);
 * // Use in SVG: <Path d={path} stroke="white" />
 */
export function getConnectorPath(
  fromMatch: TournamentMatchDto,
  toMatch: TournamentMatchDto,
  matchPositions: Map<string, MatchPosition>
): string {
  const fromPos = matchPositions.get(fromMatch.id);
  const toPos = matchPositions.get(toMatch.id);

  if (!fromPos || !toPos) {
    return '';
  }

  // Starting point: right edge, vertical center of source match
  const startX = fromPos.x + MATCH_BOX_WIDTH;
  const startY = fromPos.y + MATCH_BOX_HEIGHT / 2;

  // Ending point: left edge, vertical center of destination match
  const endX = toPos.x;
  const endY = toPos.y + MATCH_BOX_HEIGHT / 2;

  // Midpoint for horizontal step
  const midX = startX + (endX - startX) / 2;

  // Create an S-curve using quadratic bezier curves
  // Path: start → horizontal step → vertical step → horizontal to end
  return `M ${startX} ${startY} L ${midX} ${startY} L ${midX} ${endY} L ${endX} ${endY}`;
}

// ============================================
// GROUPING & FILTERING
// ============================================

/**
 * Groups an array of matches by their bracket type.
 *
 * @param matches - Array of tournament matches
 * @returns Object with arrays for winners, losers, and grandFinal matches
 *
 * @example
 * const grouped = groupMatchesByBracketType(allMatches);
 * console.log(grouped.winners); // All Winners bracket matches
 * console.log(grouped.losers);  // All Losers bracket matches
 * console.log(grouped.grandFinal); // Grand Final match(es)
 */
export function groupMatchesByBracketType(matches: TournamentMatchDto[]): GroupedMatches {
  return {
    winners: matches.filter(m => m.bracketType === 'Winners'),
    losers: matches.filter(m => m.bracketType === 'Losers'),
    grandFinal: matches.filter(m => m.bracketType === 'GrandFinal'),
  };
}

// ============================================
// DIMENSION CALCULATION
// ============================================

/**
 * Calculates the total width and height needed to display a bracket section.
 *
 * @param matches - Array of matches in this bracket section
 * @param bracketType - Type of bracket ('Winners', 'Losers', or 'GrandFinal')
 * @returns {width, height} dimensions in pixels
 *
 * @example
 * const { width, height } = calculateBracketDimensions(winnersMatches, 'Winners');
 * // Use for ScrollView contentSize: <ScrollView contentContainerStyle={{ width, height }}>
 */
export function calculateBracketDimensions(
  matches: TournamentMatchDto[],
  bracketType: 'Winners' | 'Losers' | 'GrandFinal' = 'Winners'
): BracketDimensions {
  if (matches.length === 0) {
    return { width: 0, height: 0 };
  }

  // Find the maximum and minimum round numbers
  const maxRound = Math.max(...matches.map(m => m.round));
  const minRound = Math.min(...matches.map(m => m.round));

  // Count matches in the first round (determines bracket height)
  const firstRoundMatches = matches.filter(m => m.round === minRound).length;

  // Slot height is the space each round 1 match occupies
  const slotHeight = MATCH_BOX_HEIGHT + MATCH_SPACING;

  // Calculate dimensions
  // Width: number of rounds * (box width + spacing)
  const numRounds = maxRound - minRound + 1;
  const width = numRounds * MATCH_BOX_WIDTH + (numRounds - 1) * ROUND_SPACING;

  // Height: based on first round matches (they determine the full bracket height)
  const height = firstRoundMatches * slotHeight - MATCH_SPACING;

  // Add extra spacing for Grand Final
  if (bracketType === 'GrandFinal') {
    return {
      width: width + ROUND_SPACING,
      height: height,
    };
  }

  return { width, height };
}

// ============================================
// TEAM STATISTICS
// ============================================

/**
 * Counts how many losses a team has accumulated in the tournament.
 * Used to determine elimination status in double elimination format.
 *
 * @param teamId - The team's unique identifier
 * @param matches - All completed matches in the tournament
 * @returns Number of losses (0, 1, or 2+)
 *
 * @example
 * const losses = getTeamLossCount(teamId, allMatches);
 * if (losses >= 2) {
 *   console.log('Team is eliminated');
 * }
 */
export function getTeamLossCount(teamId: string, matches: TournamentMatchDto[]): number {
  return matches.filter(match => {
    // Only count completed matches
    if (match.status !== 'Completed') {
      return false;
    }

    // Check if this team lost
    const isHomeTeam = match.homeTeamId === teamId;
    const isAwayTeam = match.awayTeamId === teamId;

    if (!isHomeTeam && !isAwayTeam) {
      return false;
    }

    // Team lost if opponent is the winner
    const teamLost = match.winnerTeamId && match.winnerTeamId !== teamId;

    return teamLost;
  }).length;
}

// ============================================
// LOSERS BRACKET CONTEXT
// ============================================

/**
 * For a losers bracket match, determines which winners bracket round the team lost in.
 * This provides context like "Lost in Winners R1" or "Lost in Winners Semifinal".
 *
 * @param match - A losers bracket match
 * @param allMatches - All matches in the tournament
 * @returns Description text (e.g., "Lost in Winners R1") or null if not determinable
 *
 * @example
 * const context = getFromWinnersRoundText(losersMatch, allMatches);
 * // Returns: "Lost in Winners R2"
 */
export function getFromWinnersRoundText(
  match: TournamentMatchDto,
  allMatches: TournamentMatchDto[]
): string | null {
  if (match.bracketType !== 'Losers') {
    return null;
  }

  // Find the winners bracket matches that feed into this losers match
  // In double elimination, losers bracket matches receive teams that lost in winners bracket

  // Strategy: Look for completed winners matches where the loser's next match is this losers match
  const feedingWinnersMatches = allMatches.filter(m =>
    m.bracketType === 'Winners' &&
    m.loserNextMatchId === match.id &&
    m.status === 'Completed'
  );

  if (feedingWinnersMatches.length === 0) {
    return null;
  }

  // Get the round number from the feeding winners match
  const winnersRound = feedingWinnersMatches[0].round;

  // Format the text
  if (winnersRound === 1) {
    return 'Lost in Winners R1';
  }

  // Check if it's a semifinal or final based on match count
  const winnersMatches = allMatches.filter(m => m.bracketType === 'Winners');
  const maxWinnersRound = Math.max(...winnersMatches.map(m => m.round));

  if (winnersRound === maxWinnersRound) {
    return 'Lost in Winners Final';
  } else if (winnersRound === maxWinnersRound - 1) {
    return 'Lost in Winners Semifinal';
  }

  return `Lost in Winners R${winnersRound}`;
}

// ============================================
// HELPER: Generate Match Positions Map
// ============================================

/**
 * Generates a map of match IDs to their calculated positions.
 * Useful for batch calculating all positions before rendering.
 *
 * @param matches - Array of matches to position
 * @param bracketType - Type of bracket ('Winners', 'Losers', or 'GrandFinal')
 * @returns Map of match ID to {x, y} position
 *
 * @example
 * const positions = generateMatchPositionsMap(winnersMatches, 'Winners');
 * const match1Pos = positions.get(match1.id);
 */
export function generateMatchPositionsMap(
  matches: TournamentMatchDto[],
  bracketType: 'Winners' | 'Losers' | 'GrandFinal' = 'Winners'
): Map<string, MatchPosition> {
  const positions = new Map<string, MatchPosition>();

  // Group matches by round to count matches per round
  const matchesByRound = new Map<number, TournamentMatchDto[]>();
  matches.forEach(match => {
    const roundMatches = matchesByRound.get(match.round) || [];
    roundMatches.push(match);
    matchesByRound.set(match.round, roundMatches);
  });

  // Calculate position for each match
  matches.forEach(match => {
    const roundMatches = matchesByRound.get(match.round) || [];
    const totalMatchesInRound = roundMatches.length;

    const position = calculateMatchPosition(
      match.round,
      match.matchNumber,
      totalMatchesInRound,
      bracketType
    );

    positions.set(match.id, position);
  });

  return positions;
}
