import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TouchableWithoutFeedback,
  ScrollView,
  ActivityIndicator,
} from 'react-native';
import type { OrganizationMember, UserBadgeDto } from '@bhmhockey/shared';
import { userService } from '@bhmhockey/api-client';
import { colors, spacing, radius } from '../theme';
import { TrophyCase } from './badges/TrophyCase';

interface MemberDetailModalProps {
  visible: boolean;
  member: OrganizationMember | null;
  /** Show admin actions (promote/demote/remove) */
  isAdmin?: boolean;
  /** Whether this member is the current user */
  isCurrentUser?: boolean;
  onClose: () => void;
  onPromoteToAdmin?: (member: OrganizationMember) => void;
  onRemoveAdmin?: (member: OrganizationMember) => void;
  onRemoveMember?: (member: OrganizationMember) => void;
}

export function MemberDetailModal({
  visible,
  member,
  isAdmin = false,
  isCurrentUser = false,
  onClose,
  onPromoteToAdmin,
  onRemoveAdmin,
  onRemoveMember,
}: MemberDetailModalProps) {
  const [badges, setBadges] = useState<UserBadgeDto[]>([]);
  const [isLoadingBadges, setIsLoadingBadges] = useState(false);

  // Reset badge state when modal closes or member changes
  useEffect(() => {
    if (!visible || !member) {
      setBadges([]);
      setIsLoadingBadges(false);
      return;
    }

    // Smart fetch: use cached badges if totalBadgeCount <= 3, otherwise fetch full list
    const cachedBadges = member.badges ?? [];
    const totalCount = member.totalBadgeCount ?? 0;

    if (totalCount <= 3) {
      // Use cached badges from member data
      setBadges(cachedBadges);
    } else {
      // Need to fetch full badge list
      setIsLoadingBadges(true);
      userService.getUserBadges(member.id)
        .then((fullBadges) => {
          setBadges(fullBadges);
        })
        .catch(() => {
          // On error, fall back to cached badges
          setBadges(cachedBadges);
        })
        .finally(() => {
          setIsLoadingBadges(false);
        });
    }
  }, [visible, member?.id]);

  if (!member) return null;

  const fullName = `${member.firstName} ${member.lastName}`;

  // Get position info for display
  const getPositionText = () => {
    if (!member.positions) return null;
    const positions: string[] = [];
    if (member.positions.goalie) positions.push(`Goalie (${member.positions.goalie})`);
    if (member.positions.skater) positions.push(`Skater (${member.positions.skater})`);
    return positions.length > 0 ? positions.join(' • ') : null;
  };

  const positionText = getPositionText();

  const handlePromoteToAdmin = () => {
    onPromoteToAdmin?.(member);
    onClose();
  };

  const handleRemoveAdmin = () => {
    onRemoveAdmin?.(member);
    onClose();
  };

  const handleRemoveMember = () => {
    onRemoveMember?.(member);
  };

  // Check if we have any admin actions to show
  const hasAdminActions = isAdmin && !isCurrentUser && (onPromoteToAdmin || onRemoveAdmin || onRemoveMember);

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={onClose}
    >
      <TouchableWithoutFeedback onPress={onClose}>
        <View style={styles.overlay}>
          <TouchableWithoutFeedback>
            <View style={styles.modal}>
              <ScrollView
                style={styles.scrollView}
                contentContainerStyle={styles.scrollContent}
                showsVerticalScrollIndicator={false}
                bounces={false}
              >
                {/* Header */}
                <View style={styles.header}>
                  <View style={styles.headerInfo}>
                    <Text style={styles.memberName} allowFontScaling={false}>{fullName}</Text>
                    {positionText && (
                      <Text style={styles.memberMeta} allowFontScaling={false}>
                        {positionText}
                      </Text>
                    )}
                    {member.isAdmin && (
                      <Text style={styles.adminBadge} allowFontScaling={false}>Admin</Text>
                    )}
                  </View>
                </View>

                {/* Email - Admin only */}
                {isAdmin && member.email && (
                  <View style={styles.section}>
                    <Text style={styles.sectionLabel} allowFontScaling={false}>Email</Text>
                    <Text style={styles.emailText} allowFontScaling={false}>{member.email}</Text>
                  </View>
                )}

                {/* Trophy Case */}
                <View style={styles.section}>
                  <Text style={styles.sectionLabel} allowFontScaling={false}>Trophy Case</Text>
                  {isLoadingBadges ? (
                    <View style={styles.loadingContainer}>
                      <ActivityIndicator size="small" color={colors.primary.teal} />
                    </View>
                  ) : (
                    <TrophyCase badges={badges} />
                  )}
                </View>

                {/* Admin Actions */}
                {hasAdminActions && (
                  <View style={styles.actions}>
                    {/* Promote to Admin */}
                    {!member.isAdmin && onPromoteToAdmin && (
                      <TouchableOpacity style={styles.actionButton} onPress={handlePromoteToAdmin}>
                        <Text style={styles.actionButtonText} allowFontScaling={false}>Promote to Admin</Text>
                      </TouchableOpacity>
                    )}

                    {/* Remove Admin Role */}
                    {member.isAdmin && onRemoveAdmin && (
                      <TouchableOpacity style={styles.actionButton} onPress={handleRemoveAdmin}>
                        <Text style={styles.actionButtonText} allowFontScaling={false}>Remove Admin Role</Text>
                      </TouchableOpacity>
                    )}

                    {/* Remove from Organization */}
                    {onRemoveMember && (
                      <TouchableOpacity
                        style={[styles.actionButton, styles.dangerButton]}
                        onPress={handleRemoveMember}
                      >
                        <Text style={styles.dangerButtonText} allowFontScaling={false}>
                          Remove from Organization
                        </Text>
                      </TouchableOpacity>
                    )}
                  </View>
                )}
              </ScrollView>

              {/* Cancel - Fixed at bottom */}
              <TouchableOpacity style={styles.cancelButton} onPress={onClose}>
                <Text style={styles.cancelButtonText} allowFontScaling={false}>
                  {hasAdminActions ? 'Cancel' : 'Close'}
                </Text>
              </TouchableOpacity>
            </View>
          </TouchableWithoutFeedback>
        </View>
      </TouchableWithoutFeedback>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
    justifyContent: 'flex-end',
  },
  modal: {
    backgroundColor: colors.bg.dark,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    maxHeight: '80%',
    paddingBottom: spacing.xl + 20,
  },
  scrollView: {
    flexGrow: 0,
  },
  scrollContent: {
    padding: spacing.lg,
    paddingBottom: 0,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.lg,
  },
  headerInfo: {
    flex: 1,
  },
  memberName: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text.primary,
  },
  memberMeta: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: 2,
  },
  adminBadge: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.primary.purple,
    marginTop: 4,
  },
  section: {
    marginBottom: spacing.lg,
  },
  sectionLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.subtle,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.sm,
  },
  emailText: {
    fontSize: 16,
    color: colors.text.secondary,
  },
  loadingContainer: {
    padding: spacing.lg,
    alignItems: 'center',
  },
  actions: {
    gap: spacing.sm,
  },
  actionButton: {
    backgroundColor: colors.bg.elevated,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  actionButtonText: {
    color: colors.primary.teal,
    fontSize: 16,
    fontWeight: '600',
  },
  dangerButton: {
    backgroundColor: colors.status.errorSubtle,
    borderWidth: 1,
    borderColor: colors.status.error,
  },
  dangerButtonText: {
    color: colors.status.error,
    fontSize: 16,
    fontWeight: '600',
  },
  cancelButton: {
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    alignItems: 'center',
  },
  cancelButtonText: {
    color: colors.text.muted,
    fontSize: 16,
    fontWeight: '600',
  },
});
