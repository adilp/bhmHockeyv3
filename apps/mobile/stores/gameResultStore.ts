import { create } from 'zustand';
import { gameResultService } from '@bhmhockey/api-client';
import type {
  GameResultDto,
  GameResultSummaryDto,
  LeaderboardDto,
  LeaderboardStat,
  PlayerStatsDto,
} from '@bhmhockey/shared';
import {
  sanitizeGameResult,
  sanitizeLeaderboard,
  sanitizePlayerStats,
  sanitizeRecentResults,
} from '../utils/gameResults';

/**
 * Game results (AMP Vision). The one owner of every game-results API call.
 *
 * Built to survive a server that does not have these endpoints yet, answers
 * 404 / 403 / 500, times out, or sends something unexpected: no action ever
 * throws or rejects. Each load ends in one of
 *   'ready'  - data, sanitized;
 *   'none'   - nothing to show (404, 403, an empty or malformed body, or the
 *              feature switched off with GAME_RESULTS_ENABLED / on the server);
 *   'error'  - anything else (network, 5xx, timeout): screens hide the section
 *              or show a quiet retry, never an alert.
 * The new UI renders only on 'ready', so the whole feature is invisible until
 * the server publishes a result - that absence is the remote off switch.
 */

/** The app-side switch for the whole feature (a one-line OTA flips it). */
export const GAME_RESULTS_ENABLED = true;

export type LoadStatus = 'idle' | 'loading' | 'ready' | 'none' | 'error';

export interface Loaded<T> {
  status: LoadStatus;
  data: T | null;
}

const idle = <T,>(): Loaded<T> => ({ status: 'idle', data: null });

/** 'none' for a 404/403/400 (nothing for this viewer), 'error' otherwise. */
export function statusForError(error: unknown): LoadStatus {
  const code = error && typeof error === 'object' && 'statusCode' in error ? (error as { statusCode: unknown }).statusCode : null;
  return code === 404 || code === 403 || code === 400 ? 'none' : 'error';
}

async function load<T>(fetch: () => Promise<unknown>, sanitize: (raw: unknown) => T | null): Promise<Loaded<T>> {
  if (!GAME_RESULTS_ENABLED) return { status: 'none', data: null };
  try {
    const data = sanitize(await fetch());
    return data === null ? { status: 'none', data: null } : { status: 'ready', data };
  } catch (error) {
    return { status: statusForError(error), data: null };
  }
}

interface GameResultState {
  results: Record<string, Loaded<GameResultDto>>;            // by eventId
  playerStats: Record<string, Loaded<PlayerStatsDto>>;       // by `${userId}|${season}`
  leaderboards: Record<string, Loaded<LeaderboardDto>>;      // by `${orgId}|${stat}|${season}`
  recentResults: Record<string, Loaded<GameResultSummaryDto[]>>; // by orgId
  statsHidden: Loaded<boolean>;

  fetchResult: (eventId: string) => Promise<Loaded<GameResultDto>>;
  fetchPlayerStats: (userId: string, season?: string) => Promise<Loaded<PlayerStatsDto>>;
  fetchLeaderboard: (orgId: string, stat: LeaderboardStat, season?: string) => Promise<Loaded<LeaderboardDto>>;
  fetchRecentResults: (orgId: string, limit?: number) => Promise<Loaded<GameResultSummaryDto[]>>;
  fetchStatsHidden: () => Promise<Loaded<boolean>>;
  setStatsHidden: (hidden: boolean) => Promise<boolean>;
  clear: () => void;
}

export const statsKey = (userId: string, season?: string) => `${userId}|${season ?? ''}`;
export const boardKey = (orgId: string, stat: LeaderboardStat, season?: string) => `${orgId}|${stat}|${season ?? ''}`;

export const useGameResultStore = create<GameResultState>((set, get) => ({
  results: {},
  playerStats: {},
  leaderboards: {},
  recentResults: {},
  statsHidden: idle<boolean>(),

  fetchResult: async (eventId) => {
    const prev = get().results[eventId];
    set((s) => ({ results: { ...s.results, [eventId]: { status: 'loading', data: prev?.data ?? null } } }));
    const next = await load(() => gameResultService.getResult(eventId), (raw) => sanitizeGameResult(raw, eventId));
    set((s) => ({ results: { ...s.results, [eventId]: next } }));
    return next;
  },

  fetchPlayerStats: async (userId, season) => {
    const key = statsKey(userId, season);
    const prev = get().playerStats[key];
    set((s) => ({ playerStats: { ...s.playerStats, [key]: { status: 'loading', data: prev?.data ?? null } } }));
    const next = await load(() => gameResultService.getPlayerStats(userId, season), (raw) => sanitizePlayerStats(raw, userId));
    set((s) => ({ playerStats: { ...s.playerStats, [key]: next } }));
    return next;
  },

  fetchLeaderboard: async (orgId, stat, season) => {
    const key = boardKey(orgId, stat, season);
    const prev = get().leaderboards[key];
    set((s) => ({ leaderboards: { ...s.leaderboards, [key]: { status: 'loading', data: prev?.data ?? null } } }));
    const next = await load(() => gameResultService.getLeaderboard(orgId, stat, season), sanitizeLeaderboard);
    set((s) => ({ leaderboards: { ...s.leaderboards, [key]: next } }));
    return next;
  },

  fetchRecentResults: async (orgId, limit = 5) => {
    const prev = get().recentResults[orgId];
    set((s) => ({ recentResults: { ...s.recentResults, [orgId]: { status: 'loading', data: prev?.data ?? null } } }));
    const next = await load(() => gameResultService.getRecentResults(orgId, limit), (raw) => {
      const list = sanitizeRecentResults(raw);
      return list.length ? list : null;
    });
    set((s) => ({ recentResults: { ...s.recentResults, [orgId]: next } }));
    return next;
  },

  fetchStatsHidden: async () => {
    const next = await load(() => gameResultService.getMyStatsVisibility(), (raw) =>
      raw && typeof raw === 'object' && typeof (raw as { hidden?: unknown }).hidden === 'boolean'
        ? (raw as { hidden: boolean }).hidden : null);
    set({ statsHidden: next });
    return next;
  },

  setStatsHidden: async (hidden) => {
    const before = get().statsHidden;
    set({ statsHidden: { status: 'ready', data: hidden } });   // optimistic
    try {
      const raw = await gameResultService.setMyStatsVisibility(hidden);
      const confirmed = raw && typeof raw === 'object' ? (raw as { hidden?: unknown }).hidden : undefined;
      set({ statsHidden: { status: 'ready', data: typeof confirmed === 'boolean' ? confirmed : hidden } });
      // Anyone's view of me may have changed: drop cached stats
      set({ playerStats: {} });
      return true;
    } catch {
      set({ statsHidden: before });                              // roll back
      return false;
    }
  },

  clear: () => set({ results: {}, playerStats: {}, leaderboards: {}, recentResults: {}, statsHidden: idle<boolean>() }),
}));
