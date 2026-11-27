/**
 * The screens render ONLY what the sanitizers return, so "no server answer can
 * crash a screen" reduces to two properties checked here over every field of
 * every answer the app reads:
 *   1. a sanitizer never throws, whatever replaces a field (null, a wrong type,
 *      NaN, an unknown enum, a huge or negative number, a nested junk object);
 *   2. what it returns is well-typed, and every formatting helper the screens
 *      call on it never throws either.
 * The answers are the server's own shapes (abridged). Rendering itself
 * is not exercised here: this repository has no React renderer installed
 * (react-test-renderer is not in node_modules), see GAME_RESULTS_APP_RELEASE.md.
 */
import {
  assistText,
  clockText,
  countText,
  goalsByPeriod,
  leaderValue,
  myLine,
  ordinalPeriod,
  playerGameRows,
  rankText,
  resultsByEventId,
  sanitizeGameResult,
  sanitizeLeaderboard,
  sanitizePlayerStats,
  sanitizeRecentResults,
  scorerGoalNumber,
  shortDate,
  sideLabel,
  statLine,
  youPlayedText,
  LEADERBOARD_STATS,
} from '../../utils/gameResults';

const EVENT = '00000000-0000-0000-0000-0000000000e1';
const ALICE = '11111111-1111-4111-8111-111111111111';
const ref = (name: string, userId: string | null = null) => ({ userId, displayName: name, jersey: '22', isLoaner: false });
const line = (name: string, team: string, extra: Record<string, unknown> = {}) => ({
  player: ref(name, (extra.userId as string) ?? null), team, position: 'Skater',
  goals: 1, assists: 0, points: 1, star: null, goalsAgainst: null, saves: null,
  estimates: { iceTime: { display: 'about 23 min', qualifier: 'about' } }, ...extra,
});

const RESULT = {
  eventId: EVENT, status: 'Published',
  black: { label: 'Visitors', score: 4 }, white: { label: 'Home', score: 5 }, winner: 'White',
  periodCount: 3, videoUrl: 'https://example.test/v', videoChapters: [{ label: 'Period 1', offsetSec: 0 }],
  goals: [
    { sequence: 1, team: 'Black', period: 1, periodElapsedSec: 145, blackScoreAfter: 1, whiteScoreAfter: 0,
      scorer: ref('Sam Sample'), assists: [], assistsRuled: true, videoOffsetSec: null },
    { sequence: 2, team: 'White', period: 2, periodElapsedSec: 598, blackScoreAfter: 1, whiteScoreAfter: 1,
      scorer: ref('Alice Able', ALICE), assists: [ref('Nora Nameonly')], assistsRuled: true, videoOffsetSec: 10 },
  ],
  stars: [line('Alice Able', 'White', { userId: ALICE, star: 1 })],
  blackLines: [line('Sam Sample', 'Black'), line('Gary Goal', 'Black', { position: 'Goalie', goalsAgainst: 5 })],
  whiteLines: [line('Alice Able', 'White', { userId: ALICE })],
  notes: ['Saves not measured'], estimateDisclaimer: 'Estimates, not measurements.', updatedAt: '2026-11-28T00:00:00Z',
};

const STATS = {
  userId: ALICE, firstName: 'Alice', lastName: 'Able', statsHidden: false, season: '2026',
  seasonLine: { gamesWithStats: 1, goals: 1, assists: 1, points: 2, stars: 1, goalsAgainst: null, gamesWithUnknownScorers: 0 },
  career: { gamesWithStats: 1, goals: 1, assists: 1, points: 2, stars: 1, goalsAgainst: 3, gamesWithUnknownScorers: 1 },
  gameLog: [{ eventId: EVENT, eventDate: '2026-11-23T02:15:00Z', opponentLabel: 'Visitors', team: 'White', result: 'W',
    teamScore: 5, opponentScore: 4, goals: 1, assists: 1, points: 2, star: 3,
    estimates: { iceTime: { display: 'about 13 min', qualifier: 'about' }, distanceSkated: { display: 'at least 0.7 mi', qualifier: 'atLeast' } } }],
  gamesWithoutStats: [{ eventId: 'nov7', eventDate: '2026-11-08T00:00:00Z', eventName: 'Home', team: 'Black' }],
};

const BOARD = {
  organizationId: 'org', season: '2026', stat: 'points', gamesCounted: 1,
  rows: [{ rank: 1, userId: ALICE, firstName: 'Alice', lastName: 'Able', gamesWithStats: 1, goals: 1, assists: 1, points: 2, stars: 1 }],
};

const RECENT = [{ eventId: EVENT, eventDate: '2026-11-23T02:15:00Z', name: 'Home vs Visitors',
  black: { label: 'Visitors', score: 4 }, white: { label: 'Home', score: 5 } }];

const JUNK: unknown[] = [
  undefined, null, 0, -1, 1.5, NaN, Infinity, -Infinity, Number.MAX_SAFE_INTEGER, '', 'x', 'Green',
  '2026-13-45', true, false, [], [null], {}, { x: { y: [1, 'z'] } }, () => 1,
];

/** Every path to every value in a JSON-like object, the object itself included. */
function paths(v: unknown, at: (string | number)[] = []): (string | number)[][] {
  const out = [at];
  if (Array.isArray(v)) v.forEach((x, i) => out.push(...paths(x, [...at, i])));
  else if (v && typeof v === 'object') for (const [k, x] of Object.entries(v)) out.push(...paths(x, [...at, k]));
  return out;
}

function replaced(base: unknown, at: (string | number)[], value: unknown): unknown {
  if (at.length === 0) return value;
  const copy: any = JSON.parse(JSON.stringify(base));
  let o = copy;
  for (const k of at.slice(0, -1)) o = o[k];
  o[at[at.length - 1]] = value;
  return copy;
}

function* variants(base: unknown): Generator<unknown> {
  for (const at of paths(base)) for (const j of JUNK) yield replaced(base, at, j);
}

describe('no server answer can make a game-results screen throw', () => {
  it('the recap (Results tab, player sheet, Past cards)', () => {
    let n = 0;
    for (const raw of variants(RESULT)) {
      n++;
      const r = sanitizeGameResult(raw, EVENT);
      if (!r) continue;
      expect(['Black', 'White', null]).toContain(r.winner);
      for (const g of r.goals) {
        expect(['Black', 'White']).toContain(g.team);
        assistText(g);
        scorerGoalNumber(r.goals, g);
        clockText(g.periodElapsedSec);
        ordinalPeriod(g.period);
      }
      goalsByPeriod(r.goals);
      sideLabel(r, 'Black'); sideLabel(r, 'White');
      for (const l of [...r.blackLines, ...r.whiteLines, ...r.stars]) {
        statLine(l); countText(l.goals);
        expect(typeof l.player.displayName).toBe('string');
      }
      myLine(r, ALICE); youPlayedText(r, ALICE); youPlayedText(r, null);
    }
    expect(n).toBeGreaterThan(1000);
  });

  it('player stats (player screen, player sheet)', () => {
    for (const raw of variants(STATS)) {
      const s = sanitizePlayerStats(raw, ALICE);
      if (!s) continue;
      expect(typeof s.firstName).toBe('string');
      expect(typeof s.lastName).toBe('string');
      expect(Array.isArray(s.gameLog)).toBe(true);
      expect(Array.isArray(s.gamesWithoutStats)).toBe(true);
      for (const l of [s.seasonLine, s.career]) {
        if (!l) continue;
        for (const k of ['gamesWithStats', 'goals', 'assists', 'points', 'stars', 'gamesWithUnknownScorers'] as const) {
          expect(Number.isFinite(l[k])).toBe(true);
        }
      }
      for (const row of playerGameRows(s)) {
        shortDate(row.eventDate);
        if (row.kind === 'stats') { statLine(row.entry); expect(['W', 'L', 'T']).toContain(row.entry.result); }
        else expect(['Black', 'White', null]).toContain(row.entry.team);
      }
    }
  });

  it('the leaderboard and recent results (organization page, leaderboard screen, Past cards)', () => {
    for (const raw of variants(BOARD)) {
      const b = sanitizeLeaderboard(raw);
      if (!b) continue;
      expect(LEADERBOARD_STATS).toContain(b.stat);
      b.rows.forEach((row, i) => { rankText(b.rows, i); expect(Number.isFinite(leaderValue(row, b.stat))).toBe(true); });
    }
    for (const raw of variants(RECENT)) {
      const list = sanitizeRecentResults(raw);
      expect(Array.isArray(list)).toBe(true);
      for (const r of list) {
        // EventCard prints these outside any boundary: they must be real numbers and strings-or-null
        for (const side of [r.black, r.white]) {
          expect(Number.isFinite(side.score)).toBe(true);
          expect(side.label === null || typeof side.label === 'string').toBe(true);
        }
        shortDate(r.eventDate); sideLabel(r, 'Black');
      }
      resultsByEventId([list]);
    }
  });
});
