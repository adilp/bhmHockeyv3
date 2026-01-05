import React from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TouchableWithoutFeedback,
} from 'react-native';
import type { EventRegistrationDto, TeamAssignment } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../theme';

interface PlayerDetailModalProps {
  visible: boolean;
  registration: EventRegistrationDto | null;
  showPayment: boolean;
  onClose: () => void;
  onSwapTeam: (registration: EventRegistrationDto) => void;
  onRemove: (registration: EventRegistrationDto) => void;
  onMarkPaid: (registration: EventRegistrationDto) => void;
  onVerifyPayment: (registration: EventRegistrationDto) => void;
  onResetPayment: (registration: EventRegistrationDto) => void;
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
  showPayment,
  onClose,
  onSwapTeam,
  onRemove,
  onMarkPaid,
  onVerifyPayment,
  onResetPayment,
}: PlayerDetailModalProps) {
  if (!registration) return null;

  const { user, teamAssignment, paymentStatus, registeredPosition } = registration;
  const fullName = `${user.firstName} ${user.lastName}`;
  const paymentInfo = getPaymentStatusDisplay(paymentStatus);
  const otherTeam: TeamAssignment = teamAssignment === 'Black' ? 'White' : 'Black';

  const handleSwapTeam = () => {
    onSwapTeam(registration);
    onClose();
  };

  const handleRemove = () => {
    onRemove(registration);
    onClose();
  };

  const handleMarkPaid = () => {
    onMarkPaid(registration);
    onClose();
  };

  const handleVerifyPayment = () => {
    onVerifyPayment(registration);
    onClose();
  };

  const handleResetPayment = () => {
    onResetPayment(registration);
    onClose();
  };

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
              {/* Header */}
              <View style={styles.header}>
                <View style={styles.headerInfo}>
                  <Text style={styles.playerName}>{fullName}</Text>
                  <Text style={styles.playerMeta}>
                    {registeredPosition || 'Skater'} • Team {teamAssignment || 'TBD'}
                  </Text>
                </View>
              </View>

              {/* Payment Status */}
              {showPayment && (
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

              {/* Actions */}
              <View style={styles.actions}>
                {/* Swap Team */}
                <TouchableOpacity style={styles.actionButton} onPress={handleSwapTeam}>
                  <Text style={styles.actionButtonText}>Move to Team {otherTeam}</Text>
                </TouchableOpacity>

                {/* Payment Actions - Show all relevant options */}
                {showPayment && (
                  <>
                    {/* Mark as Paid - only show if Pending */}
                    {paymentStatus === 'Pending' && (
                      <TouchableOpacity
                        style={[styles.actionButton, styles.successButton]}
                        onPress={handleMarkPaid}
                      >
                        <Text style={styles.successButtonText}>Mark as Paid</Text>
                      </TouchableOpacity>
                    )}

                    {/* Verify Payment - only show if MarkedPaid */}
                    {paymentStatus === 'MarkedPaid' && (
                      <TouchableOpacity
                        style={[styles.actionButton, styles.successButton]}
                        onPress={handleVerifyPayment}
                      >
                        <Text style={styles.successButtonText}>Verify Payment</Text>
                      </TouchableOpacity>
                    )}

                    {/* Reset Payment - show if MarkedPaid or Verified */}
                    {(paymentStatus === 'MarkedPaid' || paymentStatus === 'Verified') && (
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
                <TouchableOpacity
                  style={[styles.actionButton, styles.dangerButton]}
                  onPress={handleRemove}
                >
                  <Text style={styles.dangerButtonText}>Remove from Roster</Text>
                </TouchableOpacity>
              </View>

              {/* Cancel */}
              <TouchableOpacity style={styles.cancelButton} onPress={onClose}>
                <Text style={styles.cancelButtonText}>Cancel</Text>
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
    padding: spacing.lg,
    paddingBottom: spacing.xl + 20, // Extra padding for home indicator
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
    alignItems: 'center',
    marginTop: spacing.md,
  },
  cancelButtonText: {
    color: colors.text.muted,
    fontSize: 16,
    fontWeight: '600',
  },
});
