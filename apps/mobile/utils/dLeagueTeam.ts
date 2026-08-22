import { DLEAGUE_TEAMS } from '@bhmhockey/shared';

/**
 * Jersey colors rendered for the dark theme. Pure black and pure white are
 * nudged so both stay visible against the card background.
 */
const JERSEY_HEX: Record<string, string> = {
  Red: '#E5484D',
  Blue: '#3E9BFF',
  Gold: '#E0B341',
  Orange: '#F0761A',
  White: '#E6EDF3',
  Black: '#6E7681',
};

/** Hex color for a D-League team name, or null when the team is unknown/unset */
export function dLeagueTeamColor(teamName?: string | null): string | null {
  if (!teamName) return null;
  const team = DLEAGUE_TEAMS.find((t) => t.name === teamName);
  return team ? JERSEY_HEX[team.color] ?? null : null;
}
