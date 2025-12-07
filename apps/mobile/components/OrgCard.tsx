import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { colors, spacing, radius } from '../theme';
import { Badge } from './Badge';
import type { Organization } from '@bhmhockey/shared';

interface OrgCardProps {
  organization: Organization;
  isAdmin?: boolean;
  onPress: () => void;
  onJoinPress?: () => void;
  showJoinButton?: boolean;
}

// Skill level badge colors
const skillLevelColors: Record<string, string> = {
  Gold: '#FFD700',
  Silver: '#C0C0C0',
  Bronze: '#CD7F32',
  'D-League': colors.primary.blue,
};

export function OrgCard({
  organization,
  isAdmin = false,
  onPress,
  onJoinPress,
  showJoinButton = false,
}: OrgCardProps) {
  const { name, description, skillLevel, subscriberCount, isSubscribed } = organization;

  return (
    <TouchableOpacity style={styles.card} onPress={onPress} activeOpacity={0.7}>
      {/* Logo placeholder */}
      <View style={styles.logo}>
        <Text style={styles.logoText}>{name.charAt(0).toUpperCase()}</Text>
      </View>

      <View style={styles.content}>
        {/* Header row with name and badges */}
        <View style={styles.headerRow}>
          <View style={styles.nameRow}>
            <Text style={styles.name} numberOfLines={1}>{name}</Text>
            {isAdmin && <Badge variant="purple">Admin</Badge>}
          </View>
          {skillLevel && (
            <View style={[styles.skillBadge, { backgroundColor: skillLevelColors[skillLevel] || colors.text.muted }]}>
              <Text style={styles.skillBadgeText}>{skillLevel}</Text>
            </View>
          )}
        </View>

        {/* Description */}
        {description && (
          <Text style={styles.description} numberOfLines={2}>{description}</Text>
        )}

        {/* Footer with stats and join button */}
        <View style={styles.footer}>
          <Text style={styles.memberCount}>
            <Text style={styles.memberCountValue}>{subscriberCount}</Text>
            {' '}{subscriberCount === 1 ? 'member' : 'members'}
          </Text>

          {showJoinButton && onJoinPress && (
            <TouchableOpacity
              style={[styles.joinButton, isSubscribed && styles.joinedButton]}
              onPress={(e) => {
                e.stopPropagation();
                onJoinPress();
              }}
            >
              <Text style={[styles.joinButtonText, isSubscribed && styles.joinedButtonText]}>
                {isSubscribed ? 'Joined' : 'Join'}
              </Text>
            </TouchableOpacity>
          )}
        </View>
      </View>
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginBottom: spacing.sm,
    flexDirection: 'row',
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  logo: {
    width: 56,
    height: 56,
    borderRadius: radius.lg,
    backgroundColor: colors.bg.active,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: spacing.md,
  },
  logoText: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text.muted,
  },
  content: {
    flex: 1,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: spacing.xs,
  },
  nameRow: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
    gap: spacing.sm,
    marginRight: spacing.sm,
  },
  name: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    flexShrink: 1,
  },
  skillBadge: {
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: radius.round,
  },
  skillBadgeText: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.bg.darkest,
  },
  description: {
    fontSize: 13,
    color: colors.text.muted,
    lineHeight: 18,
    marginBottom: spacing.sm,
  },
  footer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginTop: spacing.xs,
  },
  memberCount: {
    fontSize: 12,
    color: colors.text.muted,
  },
  memberCountValue: {
    fontWeight: '600',
    color: colors.text.secondary,
  },
  joinButton: {
    backgroundColor: colors.primary.teal,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
  },
  joinedButton: {
    backgroundColor: colors.subtle.teal,
    borderWidth: 1,
    borderColor: colors.primary.teal,
  },
  joinButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.bg.darkest,
  },
  joinedButtonText: {
    color: colors.primary.teal,
  },
});
