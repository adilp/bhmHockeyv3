import { Modal, View, Text, TouchableOpacity, StyleSheet, ScrollView } from 'react-native';
import type { UserBadgeDto } from '@bhmhockey/shared';
import { BadgeIcon } from './BadgeIcon';
import { colors, spacing, radius } from '../../theme';

interface BadgeDetailModalProps {
  /** The badge to describe; null closes the modal */
  badge: UserBadgeDto | null;
  /**
   * Whose badge this is, when it is not the viewer's own. Anyone can tap a
   * badge on a roster or member list, so the copy stays factual rather than
   * congratulatory - the celebration modal is only for the person who just
   * earned one.
   */
  ownerName?: string;
  onClose: () => void;
}

/** "Earned Aug 26, 2026" */
function formatEarned(earnedAt: string): string {
  return new Date(earnedAt).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  });
}

/** Awarding context, when the badge carries a description of the occasion */
function contextText(badge: UserBadgeDto): string | null {
  const description = badge.context?.description;
  return typeof description === 'string' && description.length > 0 ? description : null;
}

export function BadgeDetailModal({ badge, ownerName, onClose }: BadgeDetailModalProps) {
  if (!badge) return null;

  const isOwn = !ownerName;
  const occasion = contextText(badge);

  return (
    <Modal visible transparent animationType="fade" onRequestClose={onClose}>
      <TouchableOpacity style={styles.backdrop} activeOpacity={1} onPress={onClose}>
        <TouchableOpacity style={styles.card} activeOpacity={1} onPress={() => {}}>
          <ScrollView contentContainerStyle={styles.content}>
            <BadgeIcon iconName={badge.badgeType.iconName} size={140} />

            <Text style={styles.name} allowFontScaling={false}>{badge.badgeType.name}</Text>

            {!isOwn && (
              <Text style={styles.owner} allowFontScaling={false}>
                Earned by {ownerName}
              </Text>
            )}

            <Text style={styles.description} allowFontScaling={false}>
              {badge.badgeType.description}
            </Text>

            {occasion && occasion !== badge.badgeType.description && (
              <Text style={styles.occasion} allowFontScaling={false}>{occasion}</Text>
            )}

            {/* Only your own trophy case shows when you earned it */}
            {isOwn && (
              <Text style={styles.earned} allowFontScaling={false}>
                Earned {formatEarned(badge.earnedAt)}
              </Text>
            )}
          </ScrollView>

          <TouchableOpacity style={styles.closeButton} onPress={onClose}>
            <Text style={styles.closeButtonText} allowFontScaling={false}>Close</Text>
          </TouchableOpacity>
        </TouchableOpacity>
      </TouchableOpacity>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.75)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.lg,
  },
  card: {
    width: '100%',
    maxWidth: 380,
    maxHeight: '80%',
    backgroundColor: colors.bg.dark,
    borderRadius: radius.xl,
    borderWidth: 1,
    borderColor: colors.border.default,
    padding: spacing.lg,
  },
  content: {
    alignItems: 'center',
  },
  name: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text.primary,
    textAlign: 'center',
    marginTop: spacing.md,
  },
  owner: {
    fontSize: 14,
    color: colors.primary.teal,
    textAlign: 'center',
    marginTop: spacing.xs,
  },
  description: {
    fontSize: 15,
    color: colors.text.secondary,
    textAlign: 'center',
    lineHeight: 22,
    marginTop: spacing.md,
  },
  occasion: {
    fontSize: 14,
    color: colors.text.muted,
    textAlign: 'center',
    lineHeight: 20,
    marginTop: spacing.sm,
  },
  earned: {
    fontSize: 13,
    color: colors.text.muted,
    textAlign: 'center',
    marginTop: spacing.md,
  },
  closeButton: {
    marginTop: spacing.lg,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    alignItems: 'center',
  },
  closeButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
});
