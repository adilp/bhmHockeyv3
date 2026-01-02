import type { NotificationListResponse, UnreadCountResponse } from '@bhmhockey/shared';
import { apiClient } from '../client';

export interface GetNotificationsParams {
  offset?: number;
  limit?: number;
  unreadOnly?: boolean;
}

/**
 * Notification service for in-app notification center
 */
export const notificationService = {
  /**
   * Get user's notifications with pagination
   */
  async getNotifications(params?: GetNotificationsParams): Promise<NotificationListResponse> {
    const response = await apiClient.instance.get<NotificationListResponse>('/notifications', { params });
    return response.data;
  },

  /**
   * Get unread notification count
   */
  async getUnreadCount(): Promise<number> {
    const response = await apiClient.instance.get<UnreadCountResponse>('/notifications/unread-count');
    return response.data.unreadCount;
  },

  /**
   * Mark a single notification as read
   */
  async markAsRead(id: string): Promise<void> {
    await apiClient.instance.put(`/notifications/${id}/read`);
  },

  /**
   * Mark all notifications as read
   */
  async markAllAsRead(): Promise<void> {
    await apiClient.instance.put('/notifications/read-all');
  },

  /**
   * Delete a notification
   */
  async deleteNotification(id: string): Promise<void> {
    await apiClient.instance.delete(`/notifications/${id}`);
  },
};
