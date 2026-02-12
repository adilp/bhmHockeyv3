import { View, Text, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import type { EventDto } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../theme';
import { Badge } from '../Badge';
import { getPaymentBadgeInfo } from '../../utils/payment';

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
        <Ionicons name="clipboard-outline" size={32} color={colors.text.secondary} style={styles.bannerIcon} />
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
            <Ionicons name="checkmark-circle" size={20} color={colors.status.success} style={styles.iconMargin} />
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
              <Badge variant={getPaymentBadgeInfo(paymentStatus).variant}>
                {getPaymentBadgeInfo(paymentStatus).text}
              </Badge>
            </View>
          )}
        </View>
      )}

      {isWaitlisted && (
        <View style={styles.confirmationCard}>
          <View style={styles.confirmationHeader}>
            <Ionicons name="time-outline" size={20} color={colors.status.warning} style={styles.iconMargin} />
            <Text style={styles.confirmationTitle} allowFontScaling={false}>
              You're on the Waitlist
            </Text>
          </View>
          <Text style={styles.confirmationSubtext} allowFontScaling={false}>
            Your exact position will be visible when the roster is published.
          </Text>
          {isPaidEvent && paymentStatus && (
            <View style={styles.paymentRow}>
              <Badge variant={getPaymentBadgeInfo(paymentStatus).variant}>
                {getPaymentBadgeInfo(paymentStatus).text}
              </Badge>
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
  iconMargin: {
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
    marginTop: spacing.md,
    paddingTop: spacing.md,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
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
