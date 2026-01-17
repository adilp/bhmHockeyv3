import type { AdminPasswordResetResponse, AdminUserSearchResult } from '@bhmhockey/shared';
import { apiClient } from '../client';

/**
 * Admin service for administrative operations
 */
export const adminService = {
  /**
   * Reset a user's password (admin only)
   * Generates a temporary password that must be shared with the user
   */
  async resetUserPassword(userId: string): Promise<AdminPasswordResetResponse> {
    const response = await apiClient.instance.post<AdminPasswordResetResponse>(
      `/auth/admin/reset-password/${userId}`
    );
    return response.data;
  },

  /**
   * Search users by email (for admin to find users)
   */
  async searchUsers(email: string): Promise<AdminUserSearchResult[]> {
    const response = await apiClient.instance.get<AdminUserSearchResult[]>(
      '/auth/admin/users/search',
      { params: { email } }
    );
    return response.data;
  },
};
