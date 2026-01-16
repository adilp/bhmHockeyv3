import React from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TouchableWithoutFeedback,
  ScrollView,
  Alert,
} from 'react-native';
import type { TournamentRegistrationDto, TournamentDto } from '@bhmhockey/shared';
import { colors, spacing, radius, typography } from '../theme';
import { Badge } from './Badge';
import type { BadgeVariant } from './Badge';

interface RegistrationStatusSheetProps {
  visible: boolean;
  onClose: () => void;
  registration: TournamentRegistrationDto | null;
  tournament: TournamentDto;
  onEdit: () => void;
  onWithdraw: () => void;
  onMarkPayment: () => void;
}

// Map registration status to badge variant and display text
const getRegistrationStatusConfig = (status: string, isWaitlisted: boolean): { variant: BadgeVariant; label: string } => {
  if (isWaitlisted) {
    return { variant: 'warning', label: 'Waitlisted' };
  }
  switch (status) {
    case 'Registered':
      return { variant: 'green', label: 'Registered' };
    case 'Cancelled':
      return { variant: 'error', label: 'Cancelled' };
    default:
      return { variant: 'default', label: status };
  }
};

// Map payment status to display info
const getPaymentStatusDisplay = (status?: string): { label: string; color: string; variant: BadgeVariant } => {
  switch (status) {
    case 'Verified':
      return {
        label: 'Payment Verified',
        color: colors.primary.green,
        variant: 'green',
      };
    case 'MarkedPaid':
      return {
        label: 'Pending Verification',
        color: colors.status.warning,
        variant: 'warning',
      };
    case 'Pending':
    default:
      return {
        label: 'Payment Required',
        color: colors.status.error,
        variant: 'error',
      };
  }
};

// Format ISO date string to readable format
const formatDate = (isoDate: string): string => {
  const date = new Date(isoDate);
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
};

// Check if registration deadline has passed
const isBeforeDeadline = (deadlineStr?: string): boolean => {
  if (!deadlineStr) return true;
  return new Date() < new Date(deadlineStr);
};

export function RegistrationStatusSheet({
  visible,
  onClose,
  registration,
  tournament,
  onEdit,
  onWithdraw,
  onMarkPayment,
}: RegistrationStatusSheetProps) {
  if (!registration) return null;

  const statusConfig = getRegistrationStatusConfig(registration.status, registration.isWaitlisted);
  const paymentInfo = getPaymentStatusDisplay(registration.paymentStatus);
  const canEdit = isBeforeDeadline(tournament.registrationDeadline);
  const hasFee = tournament.entryFee > 0;

  const handleWithdraw = () => {
    Alert.alert(
      'Withdraw from Tournament',
      'Are you sure you want to withdraw from this tournament? This action cannot be undone.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Withdraw',
          style: 'destructive',
          onPress: () => {
            onWithdraw();
            onClose();
          },
        },
      ]
    );
  };

  const handleEdit = () => {
    onEdit();
    onClose();
  };

  const handleMarkPayment = () => {
    onMarkPayment();
    onClose();
  };

  return (
    <Modal
      visible={visible}
      transparent
      animationType="slide"
      onRequestClose={onClose}
    >
      <TouchableWithoutFeedback onPress={onClose}>
        <View style={styles.overlay}>
          <TouchableWithoutFeedback>
            <View style={styles.sheet}>
              {/* Handle bar */}
              <View style={styles.handleBar} />

              <ScrollView
                style={styles.scrollView}
                contentContainerStyle={styles.scrollContent}
                showsVerticalScrollIndicator={false}
                bounces={false}
              >
                {/* Header */}
                <View style={styles.header}>
                  <Text style={styles.title} allowFontScaling={false}>
                    Your Registration
                  </Text>
                </View>

                {/* Status Badge */}
                <View style={styles.section}>
                  <Text style={styles.sectionLabel} allowFontScaling={false}>
                    Status
                  </Text>
                  <Badge variant={statusConfig.variant}>{statusConfig.label}</Badge>
                </View>

                {/* Position */}
                {registration.position && (
                  <View style={styles.section}>
                    <Text style={styles.sectionLabel} allowFontScaling={false}>
                      Position
                    </Text>
                    <Text style={styles.infoText} allowFontScaling={false}>
                      {registration.position}
                    </Text>
                  </View>
                )}

                {/* Team Assignment */}
                <View style={styles.section}>
                  <Text style={styles.sectionLabel} allowFontScaling={false}>
                    Team Assignment
                  </Text>
                  <Text style={styles.infoText} allowFontScaling={false}>
                    {registration.assignedTeamName || 'Pending assignment'}
                  </Text>
                </View>

                {/* Waitlist Position */}
                {registration.isWaitlisted && registration.waitlistPosition && (
                  <View style={styles.section}>
                    <Text style={styles.sectionLabel} allowFontScaling={false}>
                      Waitlist
                    </Text>
                    <Text style={styles.infoText} allowFontScaling={false}>
                      You're #{registration.waitlistPosition} on the waitlist
                    </Text>
                  </View>
                )}

                {/* Payment Status */}
                {hasFee && (
                  <View style={styles.section}>
                    <Text style={styles.sectionLabel} allowFontScaling={false}>
                      Payment
                    </Text>
                    <View style={styles.paymentRow}>
                      <Badge variant={paymentInfo.variant}>{paymentInfo.label}</Badge>
                      {registration.paymentStatus === 'Verified' && (
                        <Text style={styles.checkmark} allowFontScaling={false}>✓</Text>
                      )}
                    </View>
                    {registration.paymentDeadlineAt && registration.paymentStatus === 'Pending' && (
                      <Text style={styles.deadlineText} allowFontScaling={false}>
                        Payment due by {formatDate(registration.paymentDeadlineAt)}
                      </Text>
                    )}
                  </View>
                )}

                {/* Registered At */}
                <View style={styles.section}>
                  <Text style={styles.sectionLabel} allowFontScaling={false}>
                    Registered
                  </Text>
                  <Text style={styles.infoText} allowFontScaling={false}>
                    {formatDate(registration.registeredAt)}
                  </Text>
                </View>

                {/* Action Buttons */}
                <View style={styles.actions}>
                  {/* Edit Registration - only if before deadline */}
                  {canEdit && registration.status !== 'Cancelled' && (
                    <TouchableOpacity style={styles.actionButton} onPress={handleEdit}>
                      <Text style={styles.actionButtonText} allowFontScaling={false}>
                        Edit Registration
                      </Text>
                    </TouchableOpacity>
                  )}

                  {/* Mark as Paid - only if payment pending and has fee */}
                  {hasFee && registration.paymentStatus === 'Pending' && registration.status !== 'Cancelled' && (
                    <TouchableOpacity
                      style={[styles.actionButton, styles.successButton]}
                      onPress={handleMarkPayment}
                    >
                      <Text style={styles.successButtonText} allowFontScaling={false}>
                        Mark as Paid
                      </Text>
                    </TouchableOpacity>
                  )}

                  {/* Withdraw - always available for non-cancelled registrations */}
                  {registration.status !== 'Cancelled' && (
                    <TouchableOpacity
                      style={[styles.actionButton, styles.dangerButton]}
                      onPress={handleWithdraw}
                    >
                      <Text style={styles.dangerButtonText} allowFontScaling={false}>
                        Withdraw from Tournament
                      </Text>
                    </TouchableOpacity>
                  )}
                </View>
              </ScrollView>

              {/* Close Button - Fixed at bottom */}
              <TouchableOpacity style={styles.closeButton} onPress={onClose}>
                <Text style={styles.closeButtonText} allowFontScaling={false}>
                  Close
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
  sheet: {
    backgroundColor: colors.bg.dark,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    maxHeight: '80%',
    paddingBottom: spacing.xl + 20, // Extra padding for home indicator
  },
  handleBar: {
    width: 40,
    height: 4,
    backgroundColor: colors.border.muted,
    borderRadius: 2,
    alignSelf: 'center',
    marginTop: spacing.sm,
    marginBottom: spacing.md,
  },
  scrollView: {
    flexGrow: 0,
  },
  scrollContent: {
    padding: spacing.lg,
    paddingBottom: 0,
  },
  header: {
    marginBottom: spacing.lg,
  },
  title: {
    ...typography.screenTitle,
    fontSize: 22,
  },
  section: {
    marginBottom: spacing.lg,
  },
  sectionLabel: {
    ...typography.sectionTitle,
    marginBottom: spacing.sm,
  },
  infoText: {
    fontSize: 16,
    color: colors.text.primary,
    fontWeight: '500',
  },
  paymentRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  checkmark: {
    fontSize: 20,
    color: colors.primary.green,
    fontWeight: '700',
  },
  deadlineText: {
    fontSize: 13,
    color: colors.text.muted,
    marginTop: spacing.xs,
  },
  actions: {
    gap: spacing.sm,
    marginTop: spacing.md,
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
  closeButton: {
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    alignItems: 'center',
  },
  closeButtonText: {
    color: colors.text.muted,
    fontSize: 16,
    fontWeight: '600',
  },
});
