import { View, Text, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import type { EventDto, EventRegistrationDto } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../theme';
import { Badge } from '../Badge';
import { getSelfPaymentBadgeInfo } from '../../utils/payment';

interface DraftModeRosterProps {
  event: EventDto;
  /** Pre-publish waitlist (server returns it only when showWaitlistBeforePublish allows the viewer) */
  waitlist?: EventRegistrationDto[];
}

export function DraftModeRoster({ event, waitlist = [] }: DraftModeRosterProps) {
  const isRegistered = event.isRegistered;
  const isWaitlisted = event.amIWaitlisted;
  const paymentStatus = event.myPaymentStatus;
  const isPaidEvent = event.cost > 0;

  // Server only returns the pre-publish waitlist to registered/waitlisted viewers when the
  // event setting is on - this client check just mirrors that gate
  const showWaitlist =
    event.showWaitlistBeforePublish && (isRegistered || isWaitlisted) && waitlist.length > 0;

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
            {paymentStatus === 'Pending'
              ? 'Your spot is confirmed - please send your payment. Team assignment will be visible when the roster is published.'
              : 'Your spot is confirmed. Team assignment will be visible when the roster is published.'}
          </Text>
          {isPaidEvent && paymentStatus && (
            <View style={styles.paymentRow}>
              <Badge variant={getSelfPaymentBadgeInfo(paymentStatus).variant}>
                {getSelfPaymentBadgeInfo(paymentStatus).text}
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
            {(() => {
              const position = event.myWaitlistPosition
                ? `You're #${event.myWaitlistPosition} of ${event.waitlistCount} on the waitlist.`
                : "You're on the waitlist.";
              // Mirror the registration popup: pay now when a spot is
              // claimable, hold off when genuinely queued
              if (event.myWaitlistPaymentEligible === true) {
                return `${position} Send your payment to secure your spot - the event is not yet full.`;
              }
              if (event.myWaitlistPaymentEligible === false) {
                return `${position} Don't pay yet - the organizer will reach out if a spot opens.`;
              }
              return position;
            })()}
          </Text>
          {isPaidEvent && paymentStatus && event.myWaitlistPaymentEligible !== false && (
            <View style={styles.paymentRow}>
              <Badge variant={getSelfPaymentBadgeInfo(paymentStatus).variant}>
                {getSelfPaymentBadgeInfo(paymentStatus).text}
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

      {/* Pre-publish Waitlist (names + positions only - no payment info) */}
      {showWaitlist && (
        <View style={styles.waitlistCard}>
          <Text style={styles.waitlistTitle} allowFontScaling={false}>
            Waitlist ({waitlist.length})
          </Text>
          {waitlist.map((registration, index) => (
            <View key={registration.id} style={styles.waitlistRow}>
              <View style={styles.waitlistPositionBadge}>
                <Text style={styles.waitlistPositionText} allowFontScaling={false}>
                  #{registration.waitlistPosition ?? index + 1}
                </Text>
              </View>
              <View style={styles.waitlistUserInfo}>
                <Text style={styles.waitlistUserName} allowFontScaling={false}>
                  {registration.user.firstName} {registration.user.lastName}
                </Text>
                <Text style={styles.waitlistUserMeta} allowFontScaling={false}>
                  {registration.registeredPosition || 'Skater'}
                </Text>
              </View>
            </View>
          ))}
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
  waitlistCard: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    marginTop: spacing.lg,
  },
  waitlistTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.sm,
  },
  waitlistRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  waitlistPositionBadge: {
    width: 32,
    height: 32,
    borderRadius: radius.round,
    backgroundColor: colors.status.warningSubtle,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.sm,
  },
  waitlistPositionText: {
    color: colors.status.warning,
    fontWeight: '700',
    fontSize: 12,
  },
  waitlistUserInfo: {
    flex: 1,
  },
  waitlistUserName: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  waitlistUserMeta: {
    fontSize: 13,
    color: colors.text.muted,
    marginTop: spacing.xxs,
  },
});
