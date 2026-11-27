// Game results (published by AMP Vision). Mirrors
// apps/api/BHMHockey.Api/Models/DTOs/GameResultDTOs.cs by hand.
//
// These describe what the server SHOULD send. The app never trusts them: every
// response goes through apps/mobile/utils/gameResults.ts (sanitize*), which
// turns a missing, null, mistyped or unknown value into "not known" instead of
// a crash.

export type GameTeam = 'Black' | 'White';

/** An estimate is TEXT ONLY ("about 23 min", "at least 1.9 mi"). There is no number to format. */
export type EstimateQualifier = 'about' | 'atLeast';
export interface GameEstimateDto {
  display: string;
  qualifier: EstimateQualifier;
}
export type EstimateKey = 'iceTime' | 'distanceSkated';
export type GameEstimates = Partial<Record<EstimateKey, GameEstimateDto>>;

export interface GameResultSideDto {
  label: string | null; // "Home", "Visitors"; null = show "Black" / "White"
  score: number;
}

export interface VideoChapterDto {
  label: string;
  offsetSec: number | null;
}

/** A name anywhere in the stats. userId null = not in the app: plain text, not tappable. */
export interface GamePlayerRefDto {
  userId: string | null;
  displayName: string;
  jersey: string | null;
  isLoaner: boolean;
}

export interface GameGoalDto {
  sequence: number;
  team: GameTeam;
  period: number | null;
  periodElapsedSec: number | null;
  blackScoreAfter: number;
  whiteScoreAfter: number;
  scorer: GamePlayerRefDto | null; // null = no identified scorer
  assists: GamePlayerRefDto[];
  assistsRuled: boolean; // true + [] = unassisted; false = not known
  videoOffsetSec: number | null;
}

export interface GamePlayerLineDto {
  player: GamePlayerRefDto;
  team: GameTeam;
  position: 'Skater' | 'Goalie';
  goals: number | null; // null = not known: render "-", never 0
  assists: number | null;
  points: number | null;
  star: 1 | 2 | 3 | null;
  goalsAgainst: number | null;
  saves: number | null;
  estimates: GameEstimates | null;
}

export interface GameResultDto {
  eventId: string;
  status: 'Published';
  black: GameResultSideDto;
  white: GameResultSideDto;
  winner: GameTeam | null;
  periodCount: number | null;
  videoUrl: string | null;
  videoChapters: VideoChapterDto[];
  goals: GameGoalDto[];
  stars: GamePlayerLineDto[];
  blackLines: GamePlayerLineDto[];
  whiteLines: GamePlayerLineDto[];
  notes: string[];
  estimateDisclaimer: string | null;
  updatedAt: string;
}

export interface PlayerSeasonLineDto {
  gamesWithStats: number;
  goals: number;
  assists: number;
  points: number;
  stars: number;
  goalsAgainst: number | null;
  gamesWithUnknownScorers: number; // > 0: totals are a floor; say so
}

export interface PlayerGameLogEntryDto {
  eventId: string;
  eventDate: string;
  opponentLabel: string | null;
  team: GameTeam;
  result: 'W' | 'L' | 'T';
  teamScore: number;
  opponentScore: number;
  goals: number | null;
  assists: number | null;
  points: number | null;
  star: 1 | 2 | 3 | null;
  estimates: GameEstimates | null;
}

export interface PlayerStatsDto {
  userId: string;
  firstName: string;
  lastName: string;
  statsHidden: boolean;
  season: string;
  seasonLine: PlayerSeasonLineDto | null;
  career: PlayerSeasonLineDto | null;
  gameLog: PlayerGameLogEntryDto[];
  /** Past games the player was registered for with no published stats ("No stats"); absent from an older server. */
  gamesWithoutStats: PlayerGameWithoutStatsDto[];
}

export interface PlayerGameWithoutStatsDto {
  eventId: string;
  eventDate: string;
  eventName: string | null;
  /** Only once the roster was published. */
  team: GameTeam | null;
}

export type LeaderboardStat = 'points' | 'goals' | 'assists' | 'stars';

export interface LeaderboardRowDto {
  rank: number;
  userId: string;
  firstName: string;
  lastName: string;
  gamesWithStats: number;
  goals: number;
  assists: number;
  points: number;
  stars: number;
}

export interface LeaderboardDto {
  organizationId: string;
  season: string;
  stat: LeaderboardStat;
  gamesWithStats: number;
  rows: LeaderboardRowDto[];
}

export interface GameResultSummaryDto {
  eventId: string;
  eventDate: string;
  eventName: string | null;
  black: GameResultSideDto;
  white: GameResultSideDto;
}

export interface StatsVisibilityDto {
  hidden: boolean;
}
