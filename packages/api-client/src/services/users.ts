import type { User, UpdateUserProfileRequest, UserBadgeDto, UpdateBadgeOrderRequest, UncelebratedBadgeDto, MyTournamentsResponseDto, UserTournamentsFilter, UpcomingTournamentMatchDto } from '@bhmhockey/shared';
import { apiClient } from '../client';

/**
 * User service for profile management
 */
export const userService = {
  /**
   * Get current user profile
   */
  async getCurrentUser(): Promise<User> {
    const response = await apiClient.instance.get<User>('/users/me');
    return response.data;
  },

  /**
   * Update user profile
   */
  async updateProfile(data: UpdateUserProfileRequest): Promise<User> {
    const response = await apiClient.instance.put<User>('/users/me', data);
    return response.data;
  },

  /**
   * Update push notification token
   */
  async updatePushToken(token: string): Promise<void> {
    await apiClient.instance.put('/users/me/push-token', { pushToken: token });
  },

  /**
   * Delete user account
   * This permanently deletes the user and all associated data
   */
  async deleteAccount(): Promise<void> {
    await apiClient.instance.delete('/users/me');
  },

  /**
   * Get all badges for a user
   * Returns badges sorted by display order (for profile/modal view)
   */
  async getUserBadges(userId: string): Promise<UserBadgeDto[]> {
    const response = await apiClient.instance.get<UserBadgeDto[]>(`/users/${userId}/badges`);
    return response.data;
  },

  /**
   * Update the display order of the current user's badges
   * Used for profile badge reordering
   */
  async updateBadgeOrder(badgeIds: string[]): Promise<void> {
    const request: UpdateBadgeOrderRequest = { badgeIds };
    await apiClient.instance.patch('/users/me/badges/order', request);
  },

  /**
   * Get uncelebrated badges for the current user
   * Returns badges that haven't been celebrated yet with rarity information
   */
  async getUncelebratedBadges(): Promise<UncelebratedBadgeDto[]> {
    const response = await apiClient.instance.get<UncelebratedBadgeDto[]>('/users/me/badges/uncelebrated');
    return response.data;
  },

  /**
   * Mark a badge as celebrated
   * Sets the celebratedAt timestamp for the badge
   */
  async celebrateBadge(id: string): Promise<void> {
    await apiClient.instance.patch(`/users/me/badges/${id}/celebrate`);
  },

  /**
   * Get current user's tournaments (active, past, organizing)
   * Supports filtering by year and won tournaments
   */
  async getMyTournaments(filter?: UserTournamentsFilter): Promise<MyTournamentsResponseDto> {
    const params = new URLSearchParams();
    if (filter?.filter) params.append('filter', filter.filter);
    if (filter?.year) params.append('year', filter.year.toString());

    const queryString = params.toString();
    const url = queryString ? `/users/me/tournaments?${queryString}` : '/users/me/tournaments';

    const response = await apiClient.instance.get<MyTournamentsResponseDto>(url);
    return response.data;
  },

  /**
   * Get another user's tournament history (public - past tournaments only)
   * Supports filtering by year and won tournaments
   */
  async getUserTournaments(userId: string, filter?: UserTournamentsFilter): Promise<MyTournamentsResponseDto> {
    const params = new URLSearchParams();
    if (filter?.filter) params.append('filter', filter.filter);
    if (filter?.year) params.append('year', filter.year.toString());

    const queryString = params.toString();
    const url = queryString ? `/users/${userId}/tournaments?${queryString}` : `/users/${userId}/tournaments`;

    const response = await apiClient.instance.get<MyTournamentsResponseDto>(url);
    return response.data;
  },

  /**
   * Get current user's upcoming tournament matches
   */
  async getMyUpcomingTournamentMatches(): Promise<UpcomingTournamentMatchDto[]> {
    const response = await apiClient.instance.get<UpcomingTournamentMatchDto[]>(
      '/users/me/upcoming-tournament-matches'
    );
    return response.data;
  },
};
