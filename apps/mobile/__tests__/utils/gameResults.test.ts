/**
 * Game results: the sanitizers must turn ANY server answer into something the
 * screens can render without a check (or null), and never throw. The
 * formatting rules are the comps' (comp 02-07).
 */
import {
  assistText,
  clockText,
  countText,
  goalsByPeriod,
  isPastEvent,
  myLine,
  playerGameRows,
  rankText,
  resultsByEventId,
  sanitizeGameResult,
  sanitizeLeaderboard,
  sanitizePlayerStats,
  sanitizeRecentResults,
  scorerGoalNumber,
  sideLabel,
  statLine,
  youPlayedText,
} from '../../utils/gameResults';

const EVENT = '00000000-0000-0000-0000-0000000000e1';
const ALICE = '11111111-1111-4111-8111-111111111111';

const ref = (name: string, userId: string | null = null) => ({ userId, displayName: name, jersey: '22', isLoaner: false });
const line = (name: string, team: string, extra: Record<string, unknown> = {}) => ({
  player: ref(name, extra.userId as string ?? null), team, position: 'Skater',
  goals: 1, assists: 0, points: 1, star: null, goalsAgainst: null, saves: null, estimates: null, ...extra,
});

/** A server answer shaped like GameResultDto (abridged). */
function serverResult(): any {
  return {
    eventId: EVENT, status: 'Published',
    black: { label: 'Visitors', score: 1 }, white: { label: 'Home', score: 2 }, winner: 'White',
    periodCount: 3, videoUrl: null, videoChapters: [{ label: 'Period 1', offsetSec: null }],
    goals: [
      { sequence: 2, team: 'White', period: 2, periodElapsedSec: 598, blackScoreAfter: 1, whiteScoreAfter: 1,
        scorer: ref('Alice Able', ALICE), assists: [ref('Nora Nameonly')], assistsRuled: true, videoOffsetSec: null },
      { sequence: 1, team: 'Black', period: 1, periodElapsedSec: 145, blackScoreAfter: 1, whiteScoreAfter: 0,
        scorer: ref('Sam Sample'), assists: [], assistsRuled: true, videoOffsetSec: null },
      { sequence: 3, team: 'White', period: 3, periodElapsedSec: 603, blackScoreAfter: 1, whiteScoreAfter: 2,
        scorer: ref('Alice Able', ALICE), assists: [], assistsRuled: false, videoOffsetSec: 3000 },
    ],
    stars: [line('Alice Able', 'White', { userId: ALICE, star: 1, goals: 2, points: 2 })],
    blackLines: [line('Sam Sample', 'Black')],
    whiteLines: [line('Alice Able', 'White', { userId: ALICE, goals: 2, points: 2,
      estimates: { iceTime: { display: 'about 23 min', qualifier: 'about' } } })],
    notes: ['Saves not measured'], estimateDisclaimer: 'Estimates, not measurements.', updatedAt: '2026-11-28T00:00:00Z',
  };
}

describe('sanitizeGameResult', () => {
  it('keeps a well-formed result, goals in order', () => {
    const r = sanitizeGameResult(serverResult(), EVENT)!;
    expect(r.goals.map((g) => g.sequence)).toEqual([1, 2, 3]);
    expect(r.whiteLines[0].estimates?.iceTime?.display).toBe('about 23 min');
    expect(r.winner).toBe('White');
  });

  it.each([undefined, null, 0, 'x', [], {}, { eventId: EVENT }, { eventId: EVENT, black: {}, white: {} }])(
    'returns null for %p, never throws',
    (v) => expect(sanitizeGameResult(v)).toBeNull()
  );

  it('refuses a result for another event, or one that is not Published', () => {
    expect(sanitizeGameResult(serverResult(), 'another-event')).toBeNull();
    expect(sanitizeGameResult({ ...serverResult(), status: 'Withdrawn' }, EVENT)).toBeNull();
  });

  it('drops items with an unknown team or no name instead of mislabelling them', () => {
    const raw = serverResult();
    raw.goals.push({ ...raw.goals[0], sequence: 4, team: 'Purple' });
    raw.whiteLines.push({ ...raw.whiteLines[0], player: { displayName: '' } });
    raw.blackLines.push(line('Wrong Side', 'White'));
    const r = sanitizeGameResult(raw, EVENT)!;
    expect(r.goals).toHaveLength(3);
    expect(r.whiteLines).toHaveLength(1);
    expect(r.blackLines.map((l) => l.player.displayName)).toEqual(['Sam Sample']);
  });

  it('keeps unknown counts unknown and estimates only as known text', () => {
    const raw = serverResult();
    raw.whiteLines[0].assists = null;
    raw.whiteLines[0].goals = '2';
    raw.whiteLines[0].estimates = { iceTime: { display: 23, qualifier: 'about' }, distanceSkated: { display: 'x', qualifier: 'exactly' }, topSpeed: { display: 'fast', qualifier: 'about' } };
    const l = sanitizeGameResult(raw, EVENT)!.whiteLines[0];
    expect(l.assists).toBeNull();
    expect(l.goals).toBeNull();
    expect(l.estimates).toBeNull();
  });

  it('opens only an https video link', () => {
    expect(sanitizeGameResult({ ...serverResult(), videoUrl: 'javascript:alert(1)' }, EVENT)!.videoUrl).toBeNull();
    expect(sanitizeGameResult({ ...serverResult(), videoUrl: 'https://youtu.be/x' }, EVENT)!.videoUrl).toBe('https://youtu.be/x');
  });

  it('shows no assists for a goal whose assists nobody ruled', () => {
    const raw = serverResult();
    raw.goals[2].assists = [ref('Someone')];
    expect(sanitizeGameResult(raw, EVENT)!.goals[2].assists).toEqual([]);
  });

  it('survives every field being garbage', () => {
    const raw: any = serverResult();
    for (const k of Object.keys(raw)) {
      for (const junk of [null, 7, 'x', [], {}, [null, 1, 'a']]) {
        expect(() => sanitizeGameResult({ ...raw, [k]: junk }, EVENT)).not.toThrow();
      }
    }
  });
});

describe('sanitizePlayerStats / leaderboard / recent results', () => {
  it('a hidden player carries nothing else', () => {
    const s = sanitizePlayerStats({ userId: ALICE, firstName: 'Alice', statsHidden: true,
      seasonLine: { gamesWithStats: 1, goals: 9 }, gameLog: [{}] }, ALICE)!;
    expect(s.statsHidden).toBe(true);
    expect(s.seasonLine).toBeNull();
    expect(s.gameLog).toEqual([]);
  });

  it('a player with no games has no line, not zeros', () => {
    const s = sanitizePlayerStats({ userId: ALICE, statsHidden: false, seasonLine: null, career: { gamesWithStats: 0 }, gameLog: null })!;
    expect(s.seasonLine).toBeNull();
    expect(s.career).toBeNull();
    expect(s.gameLog).toEqual([]);
  });

  it('drops game-log rows with a bad date or an unknown team, and recomputes W/L/T from the score', () => {
    const s = sanitizePlayerStats({ userId: ALICE, gameLog: [
      { eventId: EVENT, eventDate: 'not a date', team: 'White', teamScore: 5, opponentScore: 4 },
      { eventId: EVENT, eventDate: '2026-11-23T02:15:00Z', team: 'Green', teamScore: 5, opponentScore: 4 },
      { eventId: EVENT, eventDate: '2026-11-23T02:15:00Z', team: 'White', teamScore: 5, opponentScore: 4, result: 'L' },
    ] })!;
    expect(s.gameLog).toHaveLength(1);
    expect(s.gameLog[0].result).toBe('W');
  });

  it('games without stats: an older server sends none, bad rows dropped, an unknown team shown as none', () => {
    expect(sanitizePlayerStats({ userId: ALICE }, ALICE)!.gamesWithoutStats).toEqual([]);
    expect(sanitizePlayerStats({ userId: ALICE, gamesWithoutStats: 'x' })!.gamesWithoutStats).toEqual([]);
    const s = sanitizePlayerStats({ userId: ALICE, gamesWithoutStats: [
      { eventId: 'nov7', eventDate: '2026-11-08T00:00:00Z', eventName: 'Home', team: 'White' },
      { eventId: 'sep26', eventDate: '2026-09-27T00:00:00Z', eventName: null, team: 'Purple' },
      { eventId: 'bad', eventDate: 'yesterday' }, { eventDate: '2026-09-01T00:00:00Z' }, null, 7,
    ] })!;
    expect(s.gamesWithoutStats).toEqual([
      { eventId: 'nov7', eventDate: '2026-11-08T00:00:00Z', eventName: 'Home', team: 'White' },
      { eventId: 'sep26', eventDate: '2026-09-27T00:00:00Z', eventName: null, team: null },
    ]);
    const hidden = sanitizePlayerStats({ userId: ALICE, statsHidden: true,
      gamesWithoutStats: [{ eventId: 'nov7', eventDate: '2026-11-08T00:00:00Z' }] })!;
    expect(hidden.gamesWithoutStats).toEqual([]);
  });

  it('the game log merges both lists newest first, each game once (comp 04)', () => {
    const s = sanitizePlayerStats({ userId: ALICE,
      gameLog: [{ eventId: EVENT, eventDate: '2026-11-23T02:15:00Z', team: 'White', teamScore: 5, opponentScore: 4 }],
      gamesWithoutStats: [
        { eventId: 'aug22', eventDate: '2026-08-23T00:00:00Z' },
        { eventId: EVENT, eventDate: '2026-11-23T02:15:00Z' },          // also in the log: the stats row wins
        { eventId: 'nov7', eventDate: '2026-11-08T00:00:00Z', team: 'Black' },
      ] })!;
    const rows = playerGameRows(s);
    expect(rows.map((r) => [r.kind, r.eventId])).toEqual([
      ['stats', EVENT], ['noStats', 'nov7'], ['noStats', 'aug22'],
    ]);
    expect(playerGameRows({ gameLog: [], gamesWithoutStats: [] })).toEqual([]);
    // A stats object from before this field (an old cache) still works
    expect(playerGameRows({ gameLog: [], gamesWithoutStats: undefined as unknown as [] })).toEqual([]);
  });

  it('refuses a stats answer for another player', () => {
    expect(sanitizePlayerStats({ userId: ALICE }, 'someone-else')).toBeNull();
  });

  it('leaderboard: unknown stat refused, rows without a user dropped, sorted by rank', () => {
    expect(sanitizeLeaderboard({ organizationId: 'o', stat: 'iceTime', rows: [] })).toBeNull();
    const b = sanitizeLeaderboard({ organizationId: 'o', stat: 'goals', rows: [
      { rank: 2, userId: 'b', firstName: 'B' }, { rank: 1, userId: 'a' }, { rank: 1 }, null] })!;
    expect(b.rows.map((r) => r.userId)).toEqual(['a', 'b']);
  });

  it('recent results: malformed rows dropped', () => {
    expect(sanitizeRecentResults(null)).toEqual([]);
    expect(sanitizeRecentResults([{ eventId: EVENT, eventDate: '2026-11-23T02:15:00Z',
      black: { label: 'Visitors', score: 4 }, white: { label: 'Home', score: 5 } }, { eventId: 'x' }, 3])).toHaveLength(1);
  });
});

describe('formatting', () => {
  const r = sanitizeGameResult(serverResult(), EVENT)!;

  it('never prints 0 for an unknown count', () => {
    expect(countText(null)).toBe('-');
    expect(countText(0)).toBe('0');
  });

  it('clock, periods, sides', () => {
    expect(clockText(145)).toBe('2:25');
    expect(clockText(null)).toBe('');
    expect(goalsByPeriod(r.goals).map((g) => g.period)).toEqual([1, 2, 3]);
    expect(goalsByPeriod([{ ...r.goals[0], period: null }, r.goals[1]]).map((g) => g.period)).toEqual([2, null]);
    expect(sideLabel(r, 'Black')).toBe('Visitors');
    expect(sideLabel({ black: { label: null, score: 0 }, white: { label: null, score: 0 } }, 'White')).toBe('White');
  });

  it('assists: ruled and empty is Unassisted, not ruled says nothing', () => {
    expect(assistText(r.goals[0])).toBe('Unassisted');
    expect(assistText(r.goals[1])).toBe('Assist: Nora Nameonly');
    expect(assistText(r.goals[2])).toBe('');
  });

  it('a scorer\'s running goal count, and the viewer\'s own line', () => {
    expect(scorerGoalNumber(r.goals, r.goals[2])).toBe(2);
    expect(myLine(r, ALICE.toUpperCase())?.player.displayName).toBe('Alice Able');
    expect(youPlayedText(r, ALICE)).toBe('You played · Team White · Win');
    expect(youPlayedText(r, 'nobody')).toBeNull();
  });

  it('stat line only states what is known and non-zero', () => {
    expect(statLine({ goals: 1, assists: 1 })).toBe('1 G, 1 A');
    expect(statLine({ goals: 2, assists: null })).toBe('2 G');
    expect(statLine({ goals: 0, assists: 0 })).toBe('');
  });

  it('ties show T', () => {
    const rows = [{ rank: 1 }, { rank: 2 }, { rank: 2 }, { rank: 4 }];
    expect(rows.map((_, i) => rankText(rows, i))).toEqual(['1', 'T2', 'T2', '4']);
  });

  it('past-event check and the per-event map', () => {
    expect(isPastEvent('2026-11-23T02:15:00Z', new Date('2026-11-27T00:00:00Z'))).toBe(true);
    expect(isPastEvent('garbage')).toBe(false);
    expect(isPastEvent(undefined)).toBe(false);
    const a = { eventId: 'a', eventDate: 'x', eventName: null, black: { label: null, score: 1 }, white: { label: null, score: 2 } };
    expect(Object.keys(resultsByEventId([[a], []]))).toEqual(['a']);
  });
});
