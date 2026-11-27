import type {
  GameResultSummaryDto,
  LeaderboardStat,
  StatsVisibilityDto,
} from '@bhmhockey/shared';
import { apiClient } from '../client';

/**
 * Game results (published by AMP Vision). Read-only from the phone apart from
 * the player's own hide switch: the result write belongs to AMP Vision and has
 * no client method on purpose.
 *
 * Every method returns the raw body as `unknown`: the caller (gameResultStore)
 * sanitizes it, so a server that sends something unexpected can never crash a
 * screen. A 404 rejects like any error; the store reads it as "nothing yet".
 */
export const gameResultService = {
  /** The Results tab of a past event. */
  async getResult(eventId: string): Promise<unknown> {
    const response = await apiClient.instance.get(`/events/${eventId}/result`);
    return response.data;
  },

  /** A player's season line, career and game log. */
  async getPlayerStats(userId: string, season?: string): Promise<unknown> {
    const url = season ? `/users/${userId}/stats?season=${encodeURIComponent(season)}` : `/users/${userId}/stats`;
    const response = await apiClient.instance.get(url);
    return response.data;
  },

  /** Whether my stats are hidden from other players. */
  async getMyStatsVisibility(): Promise<unknown> {
    const response = await apiClient.instance.get('/users/me/stats-visibility');
    return response.data;
  },

  /** Hide or show my stats to other players. */
  async setMyStatsVisibility(hidden: boolean): Promise<unknown> {
    const body: StatsVisibilityDto = { hidden };
    const response = await apiClient.instance.put('/users/me/stats-visibility', body);
    return response.data;
  },

  /** An organization's leaders for a season. */
  async getLeaderboard(orgId: string, stat: LeaderboardStat = 'points', season?: string): Promise<unknown> {
    const params = new URLSearchParams({ stat });
    if (season) params.append('season', season);
    const response = await apiClient.instance.get(`/organizations/${orgId}/leaderboard?${params.toString()}`);
    return response.data;
  },

  /** An organization's most recent published results. */
  async getRecentResults(orgId: string, limit = 5): Promise<unknown> {
    const response = await apiClient.instance.get(`/organizations/${orgId}/results?limit=${limit}`);
    return response.data;
  },
};

export type { GameResultSummaryDto };
