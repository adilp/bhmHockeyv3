import { create } from 'zustand';
import { notificationService } from '@bhmhockey/api-client';
import type { Notification } from '@bhmhockey/shared';

const PAGE_SIZE = 20;

interface NotificationState {
  // State
  notifications: Notification[];
  unreadCount: number;
  totalCount: number;
  hasMore: boolean;
  isLoading: boolean;
  isLoadingMore: boolean;
  error: string | null;

  // Actions
  fetchNotifications: (reset?: boolean) => Promise<void>;
  fetchUnreadCount: () => Promise<void>;
  markAsRead: (id: string) => Promise<void>;
  markAllAsRead: () => Promise<void>;
  deleteNotification: (id: string) => Promise<void>;
  addNotification: (notification: Notification) => void;
  clearError: () => void;
  reset: () => void;
}

export const useNotificationStore = create<NotificationState>((set, get) => ({
  // Initial state
  notifications: [],
  unreadCount: 0,
  totalCount: 0,
  hasMore: false,
  isLoading: false,
  isLoadingMore: false,
  error: null,

  // Fetch notifications with pagination
  fetchNotifications: async (reset = false) => {
    const { notifications, isLoading, isLoadingMore } = get();

    // Prevent duplicate requests
    if (isLoading || isLoadingMore) return;

    const offset = reset ? 0 : notifications.length;
    set(reset ? { isLoading: true, error: null } : { isLoadingMore: true, error: null });

    try {
      const response = await notificationService.getNotifications({
        offset,
        limit: PAGE_SIZE,
      });

      set({
        notifications: reset
          ? response.notifications
          : [...notifications, ...response.notifications],
        unreadCount: response.unreadCount,
        totalCount: response.totalCount,
        hasMore: response.hasMore,
        isLoading: false,
        isLoadingMore: false,
      });
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to load notifications',
        isLoading: false,
        isLoadingMore: false,
      });
    }
  },

  // Fetch just the unread count (lightweight, for badge)
  fetchUnreadCount: async () => {
    try {
      const count = await notificationService.getUnreadCount();
      set({ unreadCount: count });
    } catch (error) {
      // Silently fail - don't show error for badge count
      console.error('Failed to fetch unread count:', error);
    }
  },

  // Mark a single notification as read
  markAsRead: async (id: string) => {
    try {
      await notificationService.markAsRead(id);

      set((state) => {
        const notification = state.notifications.find((n) => n.id === id);
        const wasUnread = notification && !notification.isRead;

        return {
          notifications: state.notifications.map((n) =>
            n.id === id ? { ...n, isRead: true, readAt: new Date().toISOString() } : n
          ),
          unreadCount: wasUnread ? Math.max(0, state.unreadCount - 1) : state.unreadCount,
        };
      });
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to mark notification as read',
      });
    }
  },

  // Mark all notifications as read
  markAllAsRead: async () => {
    try {
      await notificationService.markAllAsRead();

      set((state) => ({
        notifications: state.notifications.map((n) => ({
          ...n,
          isRead: true,
          readAt: n.readAt || new Date().toISOString(),
        })),
        unreadCount: 0,
      }));
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to mark all as read',
      });
    }
  },

  // Delete a notification
  deleteNotification: async (id: string) => {
    try {
      await notificationService.deleteNotification(id);

      set((state) => {
        const notification = state.notifications.find((n) => n.id === id);
        const wasUnread = notification && !notification.isRead;

        return {
          notifications: state.notifications.filter((n) => n.id !== id),
          totalCount: state.totalCount - 1,
          unreadCount: wasUnread ? Math.max(0, state.unreadCount - 1) : state.unreadCount,
        };
      });
    } catch (error) {
      set({
        error: error instanceof Error ? error.message : 'Failed to delete notification',
      });
    }
  },

  // Add a notification (for real-time updates when push notification received)
  addNotification: (notification: Notification) => {
    set((state) => ({
      notifications: [notification, ...state.notifications],
      unreadCount: state.unreadCount + 1,
      totalCount: state.totalCount + 1,
    }));
  },

  // Clear error
  clearError: () => set({ error: null }),

  // Reset store (on logout)
  reset: () =>
    set({
      notifications: [],
      unreadCount: 0,
      totalCount: 0,
      hasMore: false,
      isLoading: false,
      isLoadingMore: false,
      error: null,
    }),
}));
