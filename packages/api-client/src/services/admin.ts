import type { AdminPasswordResetResponse, AdminUserSearchResult, AdminStatsResponse, AdminUpdateRoleResponse, UserRole } from '@bhmhockey/shared';
import { apiClient } from '../client';

/**
 * Admin service for administrative operations
 */
export const adminService = {
  /**
   * Get admin stats (total users, etc.)
   */
  async getStats(): Promise<AdminStatsResponse> {
    const response = await apiClient.instance.get<AdminStatsResponse>('/auth/admin/stats');
    return response.data;
  },

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
   * Search users by email or name (for admin to find users)
   */
  async searchUsers(query: string): Promise<AdminUserSearchResult[]> {
    const response = await apiClient.instance.get<AdminUserSearchResult[]>(
      '/auth/admin/users/search',
      { params: { query } }
    );
    return response.data;
  },

  /**
   * Update a user's role (admin only)
   */
  async updateUserRole(userId: string, role: UserRole): Promise<AdminUpdateRoleResponse> {
    const response = await apiClient.instance.put<AdminUpdateRoleResponse>(
      `/auth/admin/users/${userId}/role`,
      { role }
    );
    return response.data;
  },
};
