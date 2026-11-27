/**
 * gameResultStore must survive every server it can meet - one without the
 * endpoints (404), one that refuses (403), one that fails (500), a timeout or
 * network error, a malformed body - without a rejected promise, and show
 * nothing new unless it got real data.
 */
const mockGetResult = jest.fn();
const mockGetPlayerStats = jest.fn();
const mockGetLeaderboard = jest.fn();
const mockGetRecentResults = jest.fn();
const mockGetVisibility = jest.fn();
const mockSetVisibility = jest.fn();

jest.mock('@bhmhockey/api-client', () => ({
  gameResultService: {
    getResult: mockGetResult,
    getPlayerStats: mockGetPlayerStats,
    getLeaderboard: mockGetLeaderboard,
    getRecentResults: mockGetRecentResults,
    getMyStatsVisibility: mockGetVisibility,
    setMyStatsVisibility: mockSetVisibility,
  },
}));

import { useGameResultStore, statusForError, statsKey, boardKey } from '../../stores/gameResultStore';

const EVENT = '00000000-0000-0000-0000-0000000000e1';
const apiError = (statusCode: number) => ({ message: 'x', statusCode });
const goodResult = {
  eventId: EVENT, status: 'Published', black: { label: 'Visitors', score: 4 }, white: { label: 'Home', score: 5 },
  goals: [], stars: [], blackLines: [], whiteLines: [], notes: [],
};

beforeEach(() => {
  jest.clearAllMocks();
  useGameResultStore.getState().clear();
});

describe('fetchResult', () => {
  it('ready with sanitized data', async () => {
    mockGetResult.mockResolvedValue(goodResult);
    const r = await useGameResultStore.getState().fetchResult(EVENT);
    expect(r.status).toBe('ready');
    expect(useGameResultStore.getState().results[EVENT].data?.white.score).toBe(5);
  });

  it.each([
    ['a server without the endpoint (404)', apiError(404), 'none'],
    ['a refusal (403)', apiError(403), 'none'],
    ['a server error (500)', apiError(500), 'error'],
    ['a timeout / network error (axios gives statusCode 500 or none)', { message: 'timeout of 30000ms exceeded' }, 'error'],
    ['a thrown Error', new Error('boom'), 'error'],
  ])('%s never rejects and shows nothing new', async (_label, err, status) => {
    mockGetResult.mockRejectedValue(err);
    await expect(useGameResultStore.getState().fetchResult(EVENT)).resolves.toEqual({ status, data: null });
  });

  it.each([null, '', 'html page', [], { eventId: EVENT }, { ...goodResult, eventId: 'other' }])(
    'a malformed body %p is "none"',
    async (body) => {
      mockGetResult.mockResolvedValue(body);
      expect((await useGameResultStore.getState().fetchResult(EVENT)).status).toBe('none');
    }
  );

  it('a result that goes away (withdrawn -> 404) drops the old data', async () => {
    mockGetResult.mockResolvedValueOnce(goodResult).mockRejectedValueOnce(apiError(404));
    await useGameResultStore.getState().fetchResult(EVENT);
    await useGameResultStore.getState().fetchResult(EVENT);
    expect(useGameResultStore.getState().results[EVENT]).toEqual({ status: 'none', data: null });
  });
});

describe('the other reads', () => {
  it('player stats, leaderboard and recent results all degrade to none/error', async () => {
    mockGetPlayerStats.mockRejectedValue(apiError(404));
    mockGetLeaderboard.mockRejectedValue(apiError(403));
    mockGetRecentResults.mockResolvedValue([]);
    const s = useGameResultStore.getState();
    expect((await s.fetchPlayerStats('u', '2026')).status).toBe('none');
    expect((await s.fetchLeaderboard('o', 'points', '2026')).status).toBe('none');
    expect((await s.fetchRecentResults('o')).status).toBe('none');
    const st = useGameResultStore.getState();
    expect(st.playerStats[statsKey('u', '2026')].status).toBe('none');
    expect(st.leaderboards[boardKey('o', 'points', '2026')].status).toBe('none');
  });

  it('the hide switch appears only when the server answers it', async () => {
    mockGetVisibility.mockRejectedValue(apiError(404));
    expect((await useGameResultStore.getState().fetchStatsHidden()).status).toBe('none');
    mockGetVisibility.mockResolvedValue({ hidden: false });
    expect(await useGameResultStore.getState().fetchStatsHidden()).toEqual({ status: 'ready', data: false });
  });

  it('a failed hide-switch save rolls back and reports false', async () => {
    mockGetVisibility.mockResolvedValue({ hidden: false });
    await useGameResultStore.getState().fetchStatsHidden();
    mockSetVisibility.mockRejectedValue(apiError(500));
    expect(await useGameResultStore.getState().setStatsHidden(true)).toBe(false);
    expect(useGameResultStore.getState().statsHidden).toEqual({ status: 'ready', data: false });
    mockSetVisibility.mockResolvedValue({ hidden: true });
    expect(await useGameResultStore.getState().setStatsHidden(true)).toBe(true);
    expect(useGameResultStore.getState().statsHidden.data).toBe(true);
  });
});

it('statusForError: only 404/403/400 mean "nothing here"', () => {
  expect(statusForError(apiError(404))).toBe('none');
  expect(statusForError(apiError(401))).toBe('error');
  expect(statusForError(undefined)).toBe('error');
});
