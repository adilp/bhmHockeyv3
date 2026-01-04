import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { colors, spacing, radius } from '../theme';
import { Badge } from './Badge';
import { SkillLevelBadges } from './SkillLevelBadges';
import type { Organization } from '@bhmhockey/shared';

interface OrgCardProps {
  organization: Organization;
  isAdmin?: boolean;
  onPress: () => void;
  onJoinPress?: () => void;
  showJoinButton?: boolean;
}

export function OrgCard({
  organization,
  isAdmin = false,
  onPress,
  onJoinPress,
  showJoinButton = false,
}: OrgCardProps) {
  const { name, description, skillLevels, subscriberCount, isSubscribed } = organization;

  return (
    <TouchableOpacity style={styles.card} onPress={onPress} activeOpacity={0.7}>
      {/* Logo placeholder */}
      <View style={styles.logo}>
        <Text style={styles.logoText}>{name.charAt(0).toUpperCase()}</Text>
      </View>

      <View style={styles.content}>
        {/* Name row */}
        <Text style={styles.name} numberOfLines={1}>{name}</Text>

        {/* Badges row - Admin badge and skill levels */}
        {(isAdmin || (skillLevels && skillLevels.length > 0)) && (
          <View style={styles.badgesRow}>
            {isAdmin && <Badge variant="purple">Admin</Badge>}
            <SkillLevelBadges levels={skillLevels} size="small" />
          </View>
        )}

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
  name: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  badgesRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: spacing.xs,
    marginBottom: spacing.xs,
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
