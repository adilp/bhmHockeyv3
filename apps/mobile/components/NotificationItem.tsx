import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { colors, spacing, radius } from '../theme';
import type { Notification, NotificationType } from '@bhmhockey/shared';

interface NotificationItemProps {
  notification: Notification;
  onPress: () => void;
  onDelete?: () => void;
}

// Icon and accent colors per notification type
const typeConfig: Record<NotificationType, { icon: string; color: string }> = {
  new_event: { icon: '📅', color: colors.primary.teal },
  waitlist_promoted: { icon: '🎉', color: colors.primary.green },
  waitlist_joined: { icon: '📝', color: colors.primary.purple },
  waitlist_promotion: { icon: '⬆️', color: colors.primary.purple },
  payment_reminder: { icon: '💸', color: colors.status.warning },
  game_reminder: { icon: '🏒', color: colors.primary.teal },
  organizer_payment_reminder: { icon: '💰', color: colors.status.error },
};

function formatTimeAgo(dateString: string): string {
  // Notification timestamps are stored as UTC but may not have "Z" suffix
  // Append "Z" to ensure correct UTC parsing
  const utcDateString = dateString.endsWith('Z') ? dateString : dateString + 'Z';
  const date = new Date(utcDateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / (1000 * 60));
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;

  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
  });
}

export function NotificationItem({ notification, onPress, onDelete }: NotificationItemProps) {
  const config = typeConfig[notification.type] || { icon: '🔔', color: colors.primary.teal };
  const isUnread = !notification.isRead;

  return (
    <TouchableOpacity
      style={[styles.container, isUnread && styles.unread]}
      onPress={onPress}
      activeOpacity={0.7}
    >
      {/* Unread indicator */}
      {isUnread && <View style={[styles.unreadDot, { backgroundColor: config.color }]} />}

      {/* Icon */}
      <View style={[styles.iconContainer, { backgroundColor: `${config.color}20` }]}>
        <Text style={styles.icon}>{config.icon}</Text>
      </View>

      {/* Content */}
      <View style={styles.content}>
        <Text style={[styles.title, isUnread && styles.titleUnread]} numberOfLines={1}>
          {notification.title}
        </Text>
        <Text style={styles.body} numberOfLines={2}>
          {notification.body}
        </Text>
        <Text style={styles.time}>{formatTimeAgo(notification.createdAt)}</Text>
      </View>

      {/* Delete button */}
      {onDelete && (
        <TouchableOpacity
          style={styles.deleteButton}
          onPress={(e) => {
            e.stopPropagation();
            onDelete();
          }}
          hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
        >
          <Text style={styles.deleteIcon}>×</Text>
        </TouchableOpacity>
      )}
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    backgroundColor: colors.bg.dark,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  unread: {
    backgroundColor: colors.bg.elevated,
  },
  unreadDot: {
    position: 'absolute',
    left: spacing.sm,
    top: spacing.md + 8,
    width: 8,
    height: 8,
    borderRadius: 4,
  },
  iconContainer: {
    width: 40,
    height: 40,
    borderRadius: radius.md,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.md,
  },
  icon: {
    fontSize: 20,
  },
  content: {
    flex: 1,
    paddingRight: spacing.sm,
  },
  title: {
    fontSize: 15,
    fontWeight: '500',
    color: colors.text.secondary,
    marginBottom: 2,
  },
  titleUnread: {
    color: colors.text.primary,
    fontWeight: '600',
  },
  body: {
    fontSize: 14,
    color: colors.text.muted,
    lineHeight: 20,
    marginBottom: 4,
  },
  time: {
    fontSize: 12,
    color: colors.text.subtle,
  },
  deleteButton: {
    padding: spacing.xs,
    marginLeft: spacing.sm,
  },
  deleteIcon: {
    fontSize: 20,
    color: colors.text.muted,
    fontWeight: '300',
  },
});
