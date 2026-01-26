import { View, Text, StyleSheet } from 'react-native';
import type { EventDto } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../theme';

interface DraftModeRosterProps {
  event: EventDto;
}

export function DraftModeRoster({ event }: DraftModeRosterProps) {
  const isRegistered = event.isRegistered;
  const isWaitlisted = event.amIWaitlisted;
  const paymentStatus = event.myPaymentStatus;
  const isPaidEvent = event.cost > 0;

  return (
    <View style={styles.container}>
      {/* Draft State Banner */}
      <View style={styles.banner}>
        <Text style={styles.bannerIcon} allowFontScaling={false}>
          📋
        </Text>
        <Text style={styles.bannerTitle} allowFontScaling={false}>
          Roster Not Yet Published
        </Text>
        <Text style={styles.bannerSubtext} allowFontScaling={false}>
          The organizer is finalizing the roster. You'll be notified when it's
          published.
        </Text>
      </View>

      {/* Registration Confirmation */}
      {isRegistered && !isWaitlisted && (
        <View style={styles.confirmationCard}>
          <View style={styles.confirmationHeader}>
            <Text style={styles.checkmark} allowFontScaling={false}>
              ✓
            </Text>
            <Text style={styles.confirmationTitle} allowFontScaling={false}>
              You're Registered
            </Text>
          </View>
          <Text style={styles.confirmationSubtext} allowFontScaling={false}>
            Your spot is confirmed. Team assignment will be visible when the
            roster is published.
          </Text>
          {isPaidEvent && paymentStatus && (
            <View style={styles.paymentRow}>
              <Text style={styles.paymentLabel} allowFontScaling={false}>
                Payment:
              </Text>
              <PaymentBadge status={paymentStatus} />
            </View>
          )}
        </View>
      )}

      {isWaitlisted && (
        <View style={styles.confirmationCard}>
          <View style={styles.confirmationHeader}>
            <Text style={styles.waitlistIcon} allowFontScaling={false}>
              ⏳
            </Text>
            <Text style={styles.confirmationTitle} allowFontScaling={false}>
              You're on the Waitlist
            </Text>
          </View>
          <Text style={styles.confirmationSubtext} allowFontScaling={false}>
            Your exact position will be visible when the roster is published.
          </Text>
          {isPaidEvent && paymentStatus && (
            <View style={styles.paymentRow}>
              <Text style={styles.paymentLabel} allowFontScaling={false}>
                Payment:
              </Text>
              <PaymentBadge status={paymentStatus} />
            </View>
          )}
        </View>
      )}

      {!isRegistered && !isWaitlisted && (
        <View style={styles.notRegisteredCard}>
          <Text style={styles.notRegisteredText} allowFontScaling={false}>
            You're not registered for this event.
          </Text>
        </View>
      )}
    </View>
  );
}

// Simple payment status badge
function PaymentBadge({ status }: { status: string }) {
  const getStatusStyle = () => {
    switch (status) {
      case 'Verified':
        return { bg: colors.status.success, text: 'Paid' };
      case 'MarkedPaid':
        return { bg: colors.status.warning, text: 'Pending Verification' };
      case 'Pending':
      default:
        return { bg: colors.status.error, text: 'Unpaid' };
    }
  };

  const statusInfo = getStatusStyle();

  return (
    <View style={[styles.badge, { backgroundColor: statusInfo.bg }]}>
      <Text style={styles.badgeText} allowFontScaling={false}>
        {statusInfo.text}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    paddingHorizontal: spacing.md,
    paddingTop: spacing.md,
  },
  banner: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.lg,
    alignItems: 'center',
    marginBottom: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  bannerIcon: {
    fontSize: 32,
    marginBottom: spacing.sm,
  },
  bannerTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.xs,
    textAlign: 'center',
  },
  bannerSubtext: {
    fontSize: 14,
    color: colors.text.secondary,
    textAlign: 'center',
    lineHeight: 20,
  },
  confirmationCard: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.primary.teal,
  },
  confirmationHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  checkmark: {
    fontSize: 20,
    color: colors.status.success,
    marginRight: spacing.sm,
  },
  waitlistIcon: {
    fontSize: 20,
    marginRight: spacing.sm,
  },
  confirmationTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  confirmationSubtext: {
    fontSize: 14,
    color: colors.text.secondary,
    lineHeight: 20,
  },
  paymentRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: spacing.md,
    paddingTop: spacing.md,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  paymentLabel: {
    fontSize: 14,
    color: colors.text.secondary,
    marginRight: spacing.sm,
  },
  badge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: radius.sm,
  },
  badgeText: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.primary,
  },
  notRegisteredCard: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  notRegisteredText: {
    fontSize: 14,
    color: colors.text.secondary,
    textAlign: 'center',
  },
});
