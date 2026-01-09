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
import type { EventRegistrationDto, TeamAssignment, UserBadgeDto } from '@bhmhockey/shared';
import { userService } from '@bhmhockey/api-client';
import { colors, spacing, radius } from '../theme';
import { TrophyCase } from './badges/TrophyCase';

interface PlayerDetailModalProps {
  visible: boolean;
  registration: EventRegistrationDto | null;
  /** Show payment status section (default: true) */
  showPayment?: boolean;
  /** Show admin actions like swap team, remove, payment controls (default: true for backwards compat) */
  isAdmin?: boolean;
  onClose: () => void;
  onSwapTeam?: (registration: EventRegistrationDto) => void;
  onRemove?: (registration: EventRegistrationDto) => void;
  onMarkPaid?: (registration: EventRegistrationDto) => void;
  onVerifyPayment?: (registration: EventRegistrationDto) => void;
  onResetPayment?: (registration: EventRegistrationDto) => void;
}

const getPaymentStatusDisplay = (status?: string): { label: string; color: string } => {
  switch (status) {
    case 'Verified':
      return { label: 'Paid (Verified)', color: colors.primary.green };
    case 'MarkedPaid':
      return { label: 'Awaiting Verification', color: colors.status.warning };
    case 'Pending':
    default:
      return { label: 'Unpaid', color: colors.status.error };
  }
};

export function PlayerDetailModal({
  visible,
  registration,
  showPayment = true,
  isAdmin = true,
  onClose,
  onSwapTeam,
  onRemove,
  onMarkPaid,
  onVerifyPayment,
  onResetPayment,
}: PlayerDetailModalProps) {
  const [badges, setBadges] = useState<UserBadgeDto[]>([]);
  const [isLoadingBadges, setIsLoadingBadges] = useState(false);

  // Reset badge state when modal closes or registration changes
  useEffect(() => {
    if (!visible || !registration) {
      setBadges([]);
      setIsLoadingBadges(false);
      return;
    }

    // Smart fetch: use cached badges if totalBadgeCount <= 3, otherwise fetch full list
    const cachedBadges = registration.user.badges ?? [];
    const totalCount = registration.user.totalBadgeCount ?? 0;

    if (totalCount <= 3) {
      // Use cached badges from registration data
      setBadges(cachedBadges);
    } else {
      // Need to fetch full badge list
      setIsLoadingBadges(true);
      userService.getUserBadges(registration.user.id)
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
  }, [visible, registration?.user.id]);

  if (!registration) return null;

  const { user, teamAssignment, paymentStatus, registeredPosition } = registration;
  const fullName = `${user.firstName} ${user.lastName}`;
  const paymentInfo = getPaymentStatusDisplay(paymentStatus);
  const otherTeam: TeamAssignment = teamAssignment === 'Black' ? 'White' : 'Black';

  const handleSwapTeam = () => {
    onSwapTeam?.(registration);
    onClose();
  };

  const handleRemove = () => {
    onRemove?.(registration);
    onClose();
  };

  const handleMarkPaid = () => {
    onMarkPaid?.(registration);
    onClose();
  };

  const handleVerifyPayment = () => {
    onVerifyPayment?.(registration);
    onClose();
  };

  const handleResetPayment = () => {
    onResetPayment?.(registration);
    onClose();
  };

  // Check if we have any admin actions to show
  const hasAdminActions = isAdmin && (onSwapTeam || onRemove || onMarkPaid || onVerifyPayment || onResetPayment);

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
                    <Text style={styles.playerName}>{fullName}</Text>
                    <Text style={styles.playerMeta}>
                      {registeredPosition || 'Skater'} • Team {teamAssignment || 'TBD'}
                    </Text>
                  </View>
                </View>

                {/* Payment Status - Admin only */}
                {isAdmin && showPayment && (
                  <View style={styles.section}>
                    <Text style={styles.sectionLabel}>Payment Status</Text>
                    <View style={styles.statusRow}>
                      <View style={[styles.statusDot, { backgroundColor: paymentInfo.color }]} />
                      <Text style={[styles.statusText, { color: paymentInfo.color }]}>
                        {paymentInfo.label}
                      </Text>
                    </View>
                  </View>
                )}

                {/* Trophy Case - Visible to everyone */}
                <View style={styles.section}>
                  <Text style={styles.sectionLabel}>Trophy Case</Text>
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
                    {/* Swap Team */}
                    {onSwapTeam && (
                      <TouchableOpacity style={styles.actionButton} onPress={handleSwapTeam}>
                        <Text style={styles.actionButtonText}>Move to Team {otherTeam}</Text>
                      </TouchableOpacity>
                    )}

                    {/* Payment Actions - Show all relevant options */}
                    {showPayment && (
                      <>
                        {/* Mark as Paid - only show if Pending */}
                        {paymentStatus === 'Pending' && onMarkPaid && (
                          <TouchableOpacity
                            style={[styles.actionButton, styles.successButton]}
                            onPress={handleMarkPaid}
                          >
                            <Text style={styles.successButtonText}>Mark as Paid</Text>
                          </TouchableOpacity>
                        )}

                        {/* Verify Payment - only show if MarkedPaid */}
                        {paymentStatus === 'MarkedPaid' && onVerifyPayment && (
                          <TouchableOpacity
                            style={[styles.actionButton, styles.successButton]}
                            onPress={handleVerifyPayment}
                          >
                            <Text style={styles.successButtonText}>Verify Payment</Text>
                          </TouchableOpacity>
                        )}

                        {/* Reset Payment - show if MarkedPaid or Verified */}
                        {(paymentStatus === 'MarkedPaid' || paymentStatus === 'Verified') && onResetPayment && (
                          <TouchableOpacity
                            style={[styles.actionButton, styles.warningButton]}
                            onPress={handleResetPayment}
                          >
                            <Text style={styles.warningButtonText}>Reset Payment to Unpaid</Text>
                          </TouchableOpacity>
                        )}
                      </>
                    )}

                    {/* Remove */}
                    {onRemove && (
                      <TouchableOpacity
                        style={[styles.actionButton, styles.dangerButton]}
                        onPress={handleRemove}
                      >
                        <Text style={styles.dangerButtonText}>Remove from Roster</Text>
                      </TouchableOpacity>
                    )}
                  </View>
                )}
              </ScrollView>

              {/* Cancel - Fixed at bottom */}
              <TouchableOpacity style={styles.cancelButton} onPress={onClose}>
                <Text style={styles.cancelButtonText}>
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
    maxHeight: '80%', // Limit modal height for scroll
    paddingBottom: spacing.xl + 20, // Extra padding for home indicator
  },
  scrollView: {
    flexGrow: 0,
  },
  scrollContent: {
    padding: spacing.lg,
    paddingBottom: 0, // Cancel button has its own padding
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.lg,
  },
  headerInfo: {
    flex: 1,
  },
  playerName: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text.primary,
  },
  playerMeta: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: 2,
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
  statusRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  statusDot: {
    width: 10,
    height: 10,
    borderRadius: 5,
    marginRight: spacing.sm,
  },
  statusText: {
    fontSize: 16,
    fontWeight: '600',
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
    color: colors.text.primary,
    fontSize: 16,
    fontWeight: '600',
  },
  successButton: {
    backgroundColor: colors.subtle.green,
    borderWidth: 1,
    borderColor: colors.primary.green,
  },
  successButtonText: {
    color: colors.primary.green,
    fontSize: 16,
    fontWeight: '600',
  },
  warningButton: {
    backgroundColor: colors.status.warningSubtle,
    borderWidth: 1,
    borderColor: colors.status.warning,
  },
  warningButtonText: {
    color: colors.status.warning,
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
