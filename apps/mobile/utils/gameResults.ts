/**
 * Game results: turning whatever the server sends into something every screen
 * can render without a check, and the small formatting rules the comps use.
 *
 * The rule: nothing here throws. A missing, null, mistyped or unknown value
 * becomes "not known" (null, an empty list, or the whole object rejected as
 * null when its identity is missing) - never a crash, never a guessed number.
 * Unknown enum values (a team that is neither Black nor White, a qualifier we
 * do not know) drop the item that carries them rather than mislabel it.
 */
import type {
  GameEstimates,
  GameGoalDto,
  GamePlayerLineDto,
  GamePlayerRefDto,
  GameResultDto,
  GameResultSideDto,
  GameResultSummaryDto,
  GameTeam,
  LeaderboardDto,
  LeaderboardRowDto,
  LeaderboardStat,
  PlayerGameLogEntryDto,
  PlayerGameWithoutStatsDto,
  PlayerSeasonLineDto,
  PlayerStatsDto,
  VideoChapterDto,
} from '@bhmhockey/shared';

type Obj = Record<string, unknown>;

const isObj = (v: unknown): v is Obj => typeof v === 'object' && v !== null && !Array.isArray(v);
const str = (v: unknown): string | null => (typeof v === 'string' && v.length > 0 ? v : null);
const int = (v: unknown): number | null =>
  typeof v === 'number' && Number.isFinite(v) ? Math.trunc(v) : null;
const nonNeg = (v: unknown): number | null => {
  const n = int(v);
  return n !== null && n >= 0 ? n : null;
};
const bool = (v: unknown): boolean => v === true;
const arr = (v: unknown): unknown[] => (Array.isArray(v) ? v : []);
const team = (v: unknown): GameTeam | null => (v === 'Black' || v === 'White' ? v : null);
const star = (v: unknown): 1 | 2 | 3 | null => (v === 1 || v === 2 || v === 3 ? v : null);
const compact = <T,>(xs: (T | null)[]): T[] => xs.filter((x): x is T => x !== null);

export const ESTIMATE_KEYS = ['iceTime', 'distanceSkated'] as const;

function estimates(v: unknown): GameEstimates | null {
  if (!isObj(v)) return null;
  const out: GameEstimates = {};
  for (const key of ESTIMATE_KEYS) {
    const e = v[key];
    if (!isObj(e)) continue;
    const display = str(e.display);
    const q = e.qualifier;
    if (display && (q === 'about' || q === 'atLeast')) out[key] = { display, qualifier: q };
  }
  return Object.keys(out).length ? out : null;
}

function side(v: unknown): GameResultSideDto | null {
  if (!isObj(v)) return null;
  const score = nonNeg(v.score);
  return score === null ? null : { label: str(v.label), score };
}

function ref(v: unknown): GamePlayerRefDto | null {
  if (!isObj(v)) return null;
  const displayName = str(v.displayName);
  if (!displayName) return null;
  return { userId: str(v.userId), displayName, jersey: str(v.jersey), isLoaner: bool(v.isLoaner) };
}

function line(v: unknown): GamePlayerLineDto | null {
  if (!isObj(v)) return null;
  const player = ref(v.player);
  const t = team(v.team);
  if (!player || !t) return null;
  return {
    player,
    team: t,
    position: v.position === 'Goalie' ? 'Goalie' : 'Skater',
    goals: nonNeg(v.goals),
    assists: nonNeg(v.assists),
    points: nonNeg(v.points),
    star: star(v.star),
    goalsAgainst: nonNeg(v.goalsAgainst),
    saves: nonNeg(v.saves),
    estimates: estimates(v.estimates),
  };
}

function goal(v: unknown): GameGoalDto | null {
  if (!isObj(v)) return null;
  const t = team(v.team);
  const sequence = int(v.sequence);
  const b = nonNeg(v.blackScoreAfter);
  const w = nonNeg(v.whiteScoreAfter);
  if (!t || sequence === null || b === null || w === null) return null;
  const assistsRuled = bool(v.assistsRuled);
  return {
    sequence,
    team: t,
    period: nonNeg(v.period),
    periodElapsedSec: nonNeg(v.periodElapsedSec),
    blackScoreAfter: b,
    whiteScoreAfter: w,
    scorer: ref(v.scorer),
    assists: assistsRuled ? compact(arr(v.assists).map(ref)) : [],
    assistsRuled,
    videoOffsetSec: nonNeg(v.videoOffsetSec),
  };
}

function chapter(v: unknown): VideoChapterDto | null {
  if (!isObj(v)) return null;
  const label = str(v.label);
  return label ? { label, offsetSec: nonNeg(v.offsetSec) } : null;
}

/** Only an https link opens; anything else is dropped. */
function httpsUrl(v: unknown): string | null {
  const s = str(v);
  return s && /^https:\/\/[^\s]+$/i.test(s) ? s : null;
}

/** A Results-tab body, or null when it is not one (the tab then says "No Results Yet"). */
export function sanitizeGameResult(v: unknown, expectedEventId?: string): GameResultDto | null {
  if (!isObj(v)) return null;
  const eventId = str(v.eventId);
  const black = side(v.black);
  const white = side(v.white);
  if (!eventId || !black || !white) return null;
  if (expectedEventId && eventId.toLowerCase() !== expectedEventId.toLowerCase()) return null;
  if (v.status !== undefined && v.status !== 'Published') return null;
  return {
    eventId,
    status: 'Published',
    black,
    white,
    winner: team(v.winner),
    periodCount: nonNeg(v.periodCount),
    videoUrl: httpsUrl(v.videoUrl),
    videoChapters: compact(arr(v.videoChapters).map(chapter)),
    goals: compact(arr(v.goals).map(goal)).sort((a, b) => a.sequence - b.sequence),
    stars: compact(arr(v.stars).map(line)).filter((l) => l.star !== null).sort((a, b) => (a.star ?? 9) - (b.star ?? 9)),
    blackLines: compact(arr(v.blackLines).map(line)).filter((l) => l.team === 'Black'),
    whiteLines: compact(arr(v.whiteLines).map(line)).filter((l) => l.team === 'White'),
    notes: compact(arr(v.notes).map(str)),
    estimateDisclaimer: str(v.estimateDisclaimer),
    updatedAt: str(v.updatedAt) ?? '',
  };
}

function seasonLine(v: unknown): PlayerSeasonLineDto | null {
  if (!isObj(v)) return null;
  const games = nonNeg(v.gamesWithStats);
  if (!games) return null;
  return {
    gamesWithStats: games,
    goals: nonNeg(v.goals) ?? 0,
    assists: nonNeg(v.assists) ?? 0,
    points: nonNeg(v.points) ?? 0,
    stars: nonNeg(v.stars) ?? 0,
    goalsAgainst: nonNeg(v.goalsAgainst),
    gamesWithUnknownScorers: nonNeg(v.gamesWithUnknownScorers) ?? 0,
  };
}

function logEntry(v: unknown): PlayerGameLogEntryDto | null {
  if (!isObj(v)) return null;
  const eventId = str(v.eventId);
  const eventDate = str(v.eventDate);
  const t = team(v.team);
  const teamScore = nonNeg(v.teamScore);
  const opponentScore = nonNeg(v.opponentScore);
  if (!eventId || !eventDate || !t || teamScore === null || opponentScore === null) return null;
  if (Number.isNaN(new Date(eventDate).getTime())) return null;
  const result = teamScore > opponentScore ? 'W' : teamScore < opponentScore ? 'L' : 'T';
  return {
    eventId, eventDate, team: t, teamScore, opponentScore, result,
    opponentLabel: str(v.opponentLabel),
    goals: nonNeg(v.goals), assists: nonNeg(v.assists), points: nonNeg(v.points),
    star: star(v.star), estimates: estimates(v.estimates),
  };
}

function noStatsEntry(v: unknown): PlayerGameWithoutStatsDto | null {
  if (!isObj(v)) return null;
  const eventId = str(v.eventId);
  const eventDate = str(v.eventDate);
  if (!eventId || !eventDate || Number.isNaN(new Date(eventDate).getTime())) return null;
  // An unknown team is shown as no team rather than dropping a game the player was in
  return { eventId, eventDate, eventName: str(v.eventName), team: team(v.team) };
}

export function sanitizePlayerStats(v: unknown, expectedUserId?: string): PlayerStatsDto | null {
  if (!isObj(v)) return null;
  const userId = str(v.userId);
  if (!userId) return null;
  if (expectedUserId && userId.toLowerCase() !== expectedUserId.toLowerCase()) return null;
  const hidden = bool(v.statsHidden);
  const gameLog = hidden ? [] : compact(arr(v.gameLog).map(logEntry));
  const inLog = new Set(gameLog.map((g) => g.eventId));
  return {
    userId,
    firstName: str(v.firstName) ?? '',
    lastName: str(v.lastName) ?? '',
    statsHidden: hidden,
    season: str(v.season) ?? '',
    seasonLine: hidden ? null : seasonLine(v.seasonLine),
    career: hidden ? null : seasonLine(v.career),
    gameLog,
    // An older server sends no such field: an empty list, the screen as it was
    gamesWithoutStats: hidden ? [] : compact(arr(v.gamesWithoutStats).map(noStatsEntry)).filter((g) => !inLog.has(g.eventId)),
  };
}

export type PlayerGameRow =
  | { kind: 'stats'; eventId: string; eventDate: string; entry: PlayerGameLogEntryDto }
  | { kind: 'noStats'; eventId: string; eventDate: string; entry: PlayerGameWithoutStatsDto };

/** The player screen's game log (comp 04): games with stats and without, newest first, each game once. */
export function playerGameRows(stats: Pick<PlayerStatsDto, 'gameLog' | 'gamesWithoutStats'>): PlayerGameRow[] {
  const seen = new Set<string>();
  const rows: PlayerGameRow[] = [];
  for (const entry of stats.gameLog ?? []) {
    if (seen.has(entry.eventId)) continue;
    seen.add(entry.eventId);
    rows.push({ kind: 'stats', eventId: entry.eventId, eventDate: entry.eventDate, entry });
  }
  for (const entry of stats.gamesWithoutStats ?? []) {
    if (seen.has(entry.eventId)) continue;
    seen.add(entry.eventId);
    rows.push({ kind: 'noStats', eventId: entry.eventId, eventDate: entry.eventDate, entry });
  }
  const t = (iso: string) => {
    const n = new Date(iso).getTime();
    return Number.isFinite(n) ? n : 0;
  };
  return rows.sort((a, b) => t(b.eventDate) - t(a.eventDate));
}

export const LEADERBOARD_STATS: LeaderboardStat[] = ['points', 'goals', 'assists', 'stars'];

function boardRow(v: unknown): LeaderboardRowDto | null {
  if (!isObj(v)) return null;
  const userId = str(v.userId);
  const rank = nonNeg(v.rank);
  if (!userId || !rank) return null;
  return {
    rank, userId,
    firstName: str(v.firstName) ?? '',
    lastName: str(v.lastName) ?? '',
    gamesWithStats: nonNeg(v.gamesWithStats) ?? 0,
    goals: nonNeg(v.goals) ?? 0,
    assists: nonNeg(v.assists) ?? 0,
    points: nonNeg(v.points) ?? 0,
    stars: nonNeg(v.stars) ?? 0,
  };
}

export function sanitizeLeaderboard(v: unknown): LeaderboardDto | null {
  if (!isObj(v)) return null;
  const stat = LEADERBOARD_STATS.includes(v.stat as LeaderboardStat) ? (v.stat as LeaderboardStat) : null;
  const organizationId = str(v.organizationId);
  if (!stat || !organizationId) return null;
  return {
    organizationId, stat,
    season: str(v.season) ?? '',
    gamesWithStats: nonNeg(v.gamesWithStats) ?? 0,
    rows: compact(arr(v.rows).map(boardRow)).sort((a, b) => a.rank - b.rank),
  };
}

export function sanitizeRecentResults(v: unknown): GameResultSummaryDto[] {
  return compact(arr(v).map((x) => {
    if (!isObj(x)) return null;
    const eventId = str(x.eventId);
    const eventDate = str(x.eventDate);
    const black = side(x.black);
    const white = side(x.white);
    if (!eventId || !eventDate || !black || !white || Number.isNaN(new Date(eventDate).getTime())) return null;
    return { eventId, eventDate, eventName: str(x.eventName), black, white };
  }));
}

// ---------------------------------------------------------------- formatting

/** "Home" / "Visitors", or the app's own "Black" / "White" when a side has no label. */
export function sideLabel(result: { black: GameResultSideDto; white: GameResultSideDto }, t: GameTeam): string {
  return (t === 'Black' ? result.black.label : result.white.label) ?? t;
}

/** A count that may be unknown: "-" for null, never "0". */
export function countText(n: number | null | undefined): string {
  return typeof n === 'number' ? String(n) : '-';
}

/** 145 -> "2:25". */
export function clockText(sec: number | null): string {
  if (sec === null || sec < 0) return '';
  const m = Math.floor(sec / 60);
  const s = sec % 60;
  return `${m}:${s < 10 ? '0' : ''}${s}`;
}

export function ordinalPeriod(p: number | null): string {
  if (p === null) return 'Period unknown';
  const suffix = p === 1 ? 'st' : p === 2 ? 'nd' : p === 3 ? 'rd' : 'th';
  return `${p}${suffix} Period`;
}

/** Goals grouped by period, in order; goals with no period last. */
export function goalsByPeriod(goals: GameGoalDto[]): { period: number | null; goals: GameGoalDto[] }[] {
  const groups = new Map<number | null, GameGoalDto[]>();
  for (const g of goals) {
    const list = groups.get(g.period) ?? [];
    list.push(g);
    groups.set(g.period, list);
  }
  return [...groups.entries()]
    .sort(([a], [b]) => (a === null ? 1 : b === null ? -1 : a - b))
    .map(([period, gs]) => ({ period, goals: gs }));
}

/** "1 G, 1 A" - only what is known and non-zero; '' when nothing. */
export function statLine(l: { goals: number | null; assists: number | null }): string {
  const parts: string[] = [];
  if (l.goals) parts.push(`${l.goals} G`);
  if (l.assists) parts.push(`${l.assists} A`);
  return parts.join(', ');
}

/** "Assist: X, Y" / "Unassisted" / "" (assists nobody ruled say nothing). */
export function assistText(g: GameGoalDto): string {
  if (!g.assistsRuled) return '';
  if (g.assists.length === 0) return 'Unassisted';
  return `Assist${g.assists.length > 1 ? 's' : ''}: ${g.assists.map((a) => a.displayName).join(', ')}`;
}

/** How many goals a scorer had in this game up to and including goal `g`. */
export function scorerGoalNumber(goals: GameGoalDto[], g: GameGoalDto): number | null {
  if (!g.scorer) return null;
  const key = g.scorer.userId ?? g.scorer.displayName;
  return goals.filter((x) => x.sequence <= g.sequence && x.scorer && (x.scorer.userId ?? x.scorer.displayName) === key).length;
}

/** The viewer's own line in a game, if they have one. */
export function myLine(result: GameResultDto, userId: string | null | undefined): GamePlayerLineDto | null {
  if (!userId) return null;
  const id = userId.toLowerCase();
  return [...result.blackLines, ...result.whiteLines, ...result.stars]
    .find((l) => l.player.userId?.toLowerCase() === id) ?? null;
}

/** "You played · Team White · Win", or null. */
export function youPlayedText(result: GameResultDto, userId: string | null | undefined): string | null {
  const mine = myLine(result, userId);
  if (!mine) return null;
  const outcome = result.winner === null ? 'Tie' : result.winner === mine.team ? 'Win' : 'Loss';
  return `You played · Team ${mine.team} · ${outcome}`;
}

/** Competition ranks shown with a "T" when tied: 1, T2, T2, T4. */
export function rankText(rows: { rank: number }[], i: number): string {
  const r = rows[i].rank;
  const tied = rows.some((x, j) => j !== i && x.rank === r);
  return tied ? `T${r}` : String(r);
}

export function leaderValue(row: LeaderboardRowDto, stat: LeaderboardStat): number {
  return stat === 'goals' ? row.goals : stat === 'assists' ? row.assists : stat === 'stars' ? row.stars : row.points;
}

/** {eventId: summary} over several organizations' recent results. */
export function resultsByEventId(lists: GameResultSummaryDto[][]): Record<string, GameResultSummaryDto> {
  const out: Record<string, GameResultSummaryDto> = {};
  for (const list of lists) for (const r of list) out[r.eventId] = r;
  return out;
}

/** "Sun, Nov 22" in the phone's time zone; '' for a bad date. */
export function shortDate(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return d.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' });
}
