import { DLEAGUE_ELIGIBLE_SKILL_LEVELS, DLEAGUE_TEAMS } from '@bhmhockey/shared';
import type { SkillLevel, UserPositions } from '@bhmhockey/shared';

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

/** True when a skill level is allowed a D-League team (D-League or Bronze) */
export function isDLeagueEligibleSkill(level?: SkillLevel | null): boolean {
  return !!level && (DLEAGUE_ELIGIBLE_SKILL_LEVELS as readonly string[]).includes(level);
}

/**
 * True when any of the player's positions is at a level allowed a D-League
 * team. Bronze players often skate in both leagues, so they get the team
 * option too; Silver and Gold cannot play D-League and never do.
 *
 * Mirrors the server rule in UserService, which clears the stored team for
 * anyone this returns false for.
 */
export function canHaveDLeagueTeam(positions?: UserPositions | null): boolean {
  if (!positions) return false;
  return isDLeagueEligibleSkill(positions.goalie) || isDLeagueEligibleSkill(positions.skater);
}

/** Hex color for a D-League team name, or null when the team is unknown/unset */
export function dLeagueTeamColor(teamName?: string | null): string | null {
  if (!teamName) return null;
  const team = DLEAGUE_TEAMS.find((t) => t.name === teamName);
  return team ? JERSEY_HEX[team.color] ?? null : null;
}
