import type { User, UpdateUserProfileRequest } from '@bhmhockey/shared';
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
};
