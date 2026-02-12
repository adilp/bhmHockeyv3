import { View, Text, TouchableOpacity, StyleSheet, ScrollView, RefreshControl } from 'react-native';
import type { EventDto, SkillLevel } from '@bhmhockey/shared';
import { SkillLevelBadges } from '../SkillLevelBadges';
import { Badge } from '../Badge';
import { colors, spacing, radius } from '../../theme';
import { getPaymentBadgeInfo } from '../../utils/payment';

interface EventInfoTabProps {
  event: EventDto;
  canManage: boolean;
  onPayWithVenmo: () => void;
  onMarkAsPaid: () => void;
  onCancelRegistration: () => void;
  onRefresh?: () => Promise<void>;
  isRefreshing?: boolean;
}

export function EventInfoTab({
  event,
  canManage,
  onPayWithVenmo,
  onMarkAsPaid,
  onCancelRegistration,
  onRefresh,
  isRefreshing = false,
}: EventInfoTabProps) {
  // Waitlist takes priority - if you're waitlisted, show waitlist payment section
  const showWaitlistPaymentCard = event.amIWaitlisted && event.cost > 0;
  const showPaymentCard = event.isRegistered && !event.amIWaitlisted && event.cost > 0;
  const showCostPreview = !event.isRegistered && !event.amIWaitlisted && event.cost > 0;
  const hasMoreDetails = event.description || event.registrationDeadline;
  const isRosterFull = event.registeredCount >= event.maxPlayers;

  // Format: "SAT"
  const getDayName = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', { weekday: 'short' }).toUpperCase();
  };

  // Format: "Jan 11"
  const getMonthDay = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
  };

  // Format: "7:30 PM"
  const getTime = (dateString: string) => {
    return new Date(dateString).toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
  };

  // Format duration: "90 min"
  const formatDuration = (minutes: number) => {
    if (minutes >= 60) {
      const hrs = Math.floor(minutes / 60);
      const mins = minutes % 60;
      return mins > 0 ? `${hrs}h ${mins}m` : `${hrs}h`;
    }
    return `${minutes}m`;
  };

  const paymentBadge = getPaymentBadgeInfo(event.myPaymentStatus);

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={
        onRefresh ? (
          <RefreshControl
            refreshing={isRefreshing}
            onRefresh={onRefresh}
            tintColor={colors.primary.teal}
            colors={[colors.primary.teal]}
          />
        ) : undefined
      }
    >
      {/* ═══════════════════════════════════════════════════════════════════
          TICKET CARD
          ═══════════════════════════════════════════════════════════════════ */}
      <View style={styles.ticketCard}>
        {/* Header - org name or event title for pickup games */}
        <Text style={styles.orgName} numberOfLines={1}>
          {(event.organizationName || event.name || 'Pickup Game').toUpperCase()}
        </Text>

        {/* Event Title - only show if org exists (otherwise title is already the header) */}
        {event.organizationName && event.name && (
          <Text style={styles.eventTitle} numberOfLines={2}>
            {event.name}
          </Text>
        )}

        <View style={styles.ticketDivider} />

        {/* Date / Time / Duration Row */}
        <View style={styles.dateTimeRow}>
          <View style={styles.dateTimeItem}>
            <Text style={styles.dateTimeValue}>{getDayName(event.eventDate)}</Text>
            <Text style={styles.dateTimeLabel}>{getMonthDay(event.eventDate)}</Text>
          </View>
          <View style={styles.dateTimeItem}>
            <Text style={styles.dateTimeValue}>{getTime(event.eventDate)}</Text>
            <Text style={styles.dateTimeLabel}>Start</Text>
          </View>
          <View style={styles.dateTimeItem}>
            <Text style={styles.dateTimeValue}>{formatDuration(event.duration)}</Text>
            <Text style={styles.dateTimeLabel}>Duration</Text>
          </View>
        </View>

        {/* Venue */}
        {event.venue && (
          <Text style={styles.venue} numberOfLines={2}>
            {event.venue}
          </Text>
        )}

        {/* Status Badges Row */}
        {(canManage || event.isRegistered || event.amIWaitlisted ||
          event.visibility === 'InviteOnly') && (
          <View style={styles.statusBadgeRow}>
            {canManage && <Badge variant="purple">Organizer</Badge>}
            {event.isRegistered && <Badge variant="green">Registered</Badge>}
            {event.amIWaitlisted && (
              <Badge variant="warning">
                {event.isRosterPublished ? `#${event.myWaitlistPosition} Waitlist` : 'Waitlist'}
              </Badge>
            )}
            {/* Payment status badge for registered/waitlisted users on paid events */}
            {(event.isRegistered || event.amIWaitlisted) && event.cost > 0 && (
              <Badge variant={paymentBadge.variant}>{paymentBadge.text}</Badge>
            )}
            {event.visibility === 'InviteOnly' && <Badge variant="warning">Invite Only</Badge>}
          </View>
        )}

        {/* Skill Levels */}
        {event.skillLevels && event.skillLevels.length > 0 && (
          <View style={styles.skillLevelsRow}>
            <Text style={styles.skillLevelsLabel}>Skill Levels:</Text>
            <SkillLevelBadges levels={event.skillLevels as SkillLevel[]} size="small" />
          </View>
        )}

        {/* ═══════════════════════════════════════════════════════════════════
            WAITLIST PAYMENT SECTION (for waitlisted users on paid events)
            ═══════════════════════════════════════════════════════════════════ */}
        {showWaitlistPaymentCard && (
          <>
            <View style={styles.paymentDivider} />

            <View style={styles.waitlistPaymentSection}>
              {/* Waitlist Position Header - only show when roster is full AND published */}
              {isRosterFull && event.isRosterPublished && (
                <Text style={styles.waitlistPositionText}>
                  You're #{event.myWaitlistPosition} on the waitlist
                </Text>
              )}

              {/* Pending: Show payment prompt and buttons */}
              {event.myPaymentStatus === 'Pending' && (
                <>
                  <Text style={styles.waitlistPaymentPrompt}>
                    {!event.isRosterPublished
                      ? 'Pay to secure your registration'
                      : isRosterFull
                        ? 'Pay to be ready when a spot opens'
                        : 'Pay to secure your spot on the roster'}
                  </Text>

                  <View style={styles.paymentActions}>
                    {event.creatorVenmoHandle && (
                      <TouchableOpacity style={styles.venmoButton} onPress={onPayWithVenmo}>
                        <Text style={styles.venmoButtonText}>Pay with Venmo</Text>
                      </TouchableOpacity>
                    )}

                    <TouchableOpacity style={styles.markPaidButton} onPress={onMarkAsPaid}>
                      <Text style={styles.markPaidButtonText}>I've Already Paid</Text>
                    </TouchableOpacity>

                    {!event.creatorVenmoHandle && (
                      <Text style={styles.noVenmoText}>
                        Contact organizer directly for payment details.
                      </Text>
                    )}
                  </View>

                  {event.creatorVenmoHandle && (
                    <Text style={styles.disclaimer}>
                      Payment goes directly to the organizer. BHM Hockey does not process payments.
                    </Text>
                  )}
                </>
              )}

              {/* MarkedPaid: Awaiting verification */}
              {event.myPaymentStatus === 'MarkedPaid' && (
                <Text style={styles.statusMessage}>
                  Awaiting organizer verification
                </Text>
              )}

              {/* Verified: Payment confirmed */}
              {event.myPaymentStatus === 'Verified' && (
                <Text style={[styles.statusMessage, { color: colors.primary.green }]}>
                  {!event.isRosterPublished
                    ? "Payment verified - you're all set!"
                    : isRosterFull
                      ? "Payment verified! You'll be automatically added when a spot opens."
                      : "Payment verified! You'll be added to the roster shortly."}
                </Text>
              )}
            </View>
          </>
        )}

        {/* ═══════════════════════════════════════════════════════════════════
            PAYMENT SECTION (for registered users with cost)
            ═══════════════════════════════════════════════════════════════════ */}
        {showPaymentCard && (
          <>
            <View style={styles.paymentDivider} />

            <View style={styles.paymentSection}>
              {/* Amount + Status + Team Row */}
              <View style={styles.paymentRow}>
                <View style={styles.paymentAmount}>
                  <Text style={styles.amountValue}>${event.cost.toFixed(2)}</Text>
                  <Badge variant={paymentBadge.variant}>{paymentBadge.text}</Badge>
                </View>

                {event.myTeamAssignment && event.isRosterPublished && (
                  <View style={styles.teamSection}>
                    <View
                      style={[
                        styles.teamBadge,
                        event.myTeamAssignment === 'Black' ? styles.teamBlack : styles.teamWhite,
                      ]}
                    >
                      <Text
                        style={[
                          styles.teamBadgeText,
                          event.myTeamAssignment === 'Black' ? styles.teamBlackText : styles.teamWhiteText,
                        ]}
                      >
                        {event.myTeamAssignment.toUpperCase()}
                      </Text>
                    </View>
                    <Text style={styles.teamLabel}>TEAM</Text>
                  </View>
                )}
              </View>

              {/* Payment Actions */}
              {event.myPaymentStatus === 'Pending' && (
                <View style={styles.paymentActions}>
                  {event.creatorVenmoHandle && (
                    <TouchableOpacity style={styles.venmoButton} onPress={onPayWithVenmo}>
                      <Text style={styles.venmoButtonText}>Pay with Venmo</Text>
                    </TouchableOpacity>
                  )}

                  <TouchableOpacity style={styles.markPaidButton} onPress={onMarkAsPaid}>
                    <Text style={styles.markPaidButtonText}>I've Already Paid</Text>
                  </TouchableOpacity>

                  {!event.creatorVenmoHandle && (
                    <Text style={styles.noVenmoText}>
                      Contact organizer directly for payment details.
                    </Text>
                  )}
                </View>
              )}

              {event.myPaymentStatus === 'MarkedPaid' && (
                <Text style={styles.statusMessage}>
                  Awaiting verification from organizer
                </Text>
              )}

              {event.myPaymentStatus === 'Verified' && (
                <Text style={[styles.statusMessage, { color: colors.primary.green }]}>
                  Payment verified - you're all set!
                </Text>
              )}
            </View>

            {/* Disclaimer */}
            {event.myPaymentStatus === 'Pending' && event.creatorVenmoHandle && (
              <Text style={styles.disclaimer}>
                Payment goes directly to the organizer. BHM Hockey does not process payments.
              </Text>
            )}
          </>
        )}

        {/* Cost Preview for non-registered users */}
        {showCostPreview && (
          <>
            <View style={styles.paymentDivider} />
            <View style={styles.costPreview}>
              <Text style={styles.costPreviewLabel}>Cost to register</Text>
              <Text style={styles.costPreviewValue}>${event.cost.toFixed(2)}</Text>
            </View>
          </>
        )}
      </View>

      {/* ═══════════════════════════════════════════════════════════════════
          DETAILS SECTION
          ═══════════════════════════════════════════════════════════════════ */}
      {hasMoreDetails && (
        <View style={styles.detailsSection}>
          {/* Description */}
          {event.description && (
            <View style={styles.detailItem}>
              <Text style={styles.detailLabel}>ABOUT</Text>
              <Text style={styles.detailText}>{event.description}</Text>
            </View>
          )}

          {/* Registration Deadline */}
          {event.registrationDeadline && (
            <View style={styles.detailItem}>
              <Text style={styles.detailLabel}>REGISTRATION DEADLINE</Text>
              <Text style={styles.detailText}>
                {new Date(event.registrationDeadline).toLocaleDateString('en-US', {
                  weekday: 'short',
                  month: 'short',
                  day: 'numeric',
                  hour: 'numeric',
                  minute: '2-digit',
                })}
              </Text>
            </View>
          )}
        </View>
      )}

      {/* ═══════════════════════════════════════════════════════════════════
          CANCEL REGISTRATION (for registered/waitlisted users)
          ═══════════════════════════════════════════════════════════════════ */}
      {(event.isRegistered || event.amIWaitlisted) && (
        <View style={styles.cancelSection}>
          <TouchableOpacity style={styles.cancelButton} onPress={onCancelRegistration}>
            <Text style={styles.cancelButtonText}>
              {event.amIWaitlisted ? 'Leave Waitlist' : 'Cancel Registration'}
            </Text>
          </TouchableOpacity>
        </View>
      )}

      <View style={{ height: 20 }} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  content: {
    padding: spacing.md,
  },

  // ═══════════════════════════════════════════════════════════════════
  // TICKET CARD
  // ═══════════════════════════════════════════════════════════════════
  ticketCard: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  orgName: {
    fontSize: 14,
    fontWeight: '700',
    color: colors.text.muted,
    textAlign: 'center',
    letterSpacing: 1,
  },
  eventTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
    textAlign: 'center',
    marginTop: spacing.sm,
  },
  ticketDivider: {
    height: 1,
    backgroundColor: colors.border.default,
    marginVertical: spacing.md,
  },
  dateTimeRow: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    marginBottom: spacing.md,
  },
  dateTimeItem: {
    alignItems: 'center',
  },
  dateTimeValue: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text.primary,
  },
  dateTimeLabel: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: 2,
  },
  venue: {
    fontSize: 14,
    color: colors.text.secondary,
    textAlign: 'center',
    marginBottom: spacing.sm,
  },
  statusBadgeRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'center',
    gap: spacing.xs,
    marginTop: spacing.sm,
  },
  skillLevelsRow: {
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  skillLevelsLabel: {
    fontSize: 12,
    color: colors.text.muted,
    marginBottom: spacing.xs,
  },

  // ═══════════════════════════════════════════════════════════════════
  // PAYMENT SECTION
  // ═══════════════════════════════════════════════════════════════════
  paymentDivider: {
    height: 1,
    backgroundColor: colors.border.default,
    marginVertical: spacing.lg,
    marginHorizontal: -spacing.lg,
    // Dashed effect approximation
    borderStyle: 'dashed',
  },
  paymentSection: {
    alignItems: 'center',
  },
  waitlistPaymentSection: {
    alignItems: 'center',
  },
  waitlistPositionText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.sm,
  },
  waitlistPaymentPrompt: {
    fontSize: 14,
    color: colors.text.muted,
    marginBottom: spacing.md,
    textAlign: 'center',
  },
  paymentRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    width: '100%',
    marginBottom: spacing.md,
  },
  paymentAmount: {
    alignItems: 'flex-start',
  },
  amountValue: {
    fontSize: 32,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  teamSection: {
    alignItems: 'center',
  },
  teamBadge: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: radius.sm,
  },
  teamBlack: {
    backgroundColor: colors.bg.darkest,
    borderWidth: 2,
    borderColor: colors.text.primary,
  },
  teamWhite: {
    backgroundColor: colors.text.primary,
  },
  teamBadgeText: {
    fontSize: 14,
    fontWeight: '800',
  },
  teamBlackText: {
    color: colors.text.primary,
  },
  teamWhiteText: {
    color: colors.bg.darkest,
  },
  teamLabel: {
    fontSize: 10,
    color: colors.text.muted,
    marginTop: spacing.xs,
    letterSpacing: 1,
  },
  paymentActions: {
    width: '100%',
    gap: spacing.sm,
  },
  venmoButton: {
    backgroundColor: '#008CFF',
    paddingVertical: 14,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  venmoButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
  markPaidButton: {
    backgroundColor: 'transparent',
    paddingVertical: 14,
    borderRadius: radius.md,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border.muted,
  },
  markPaidButtonText: {
    color: colors.text.secondary,
    fontSize: 16,
    fontWeight: '500',
  },
  noVenmoText: {
    fontSize: 14,
    color: colors.text.muted,
    textAlign: 'center',
    fontStyle: 'italic',
  },
  statusMessage: {
    fontSize: 14,
    color: colors.text.muted,
    textAlign: 'center',
  },
  disclaimer: {
    fontSize: 11,
    color: colors.text.subtle,
    textAlign: 'center',
    marginTop: spacing.md,
  },

  // Cost preview for non-registered
  costPreview: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  costPreviewLabel: {
    fontSize: 14,
    color: colors.text.muted,
  },
  costPreviewValue: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text.primary,
  },

  // ═══════════════════════════════════════════════════════════════════
  // DETAILS SECTION
  // ═══════════════════════════════════════════════════════════════════
  detailsSection: {
    marginTop: spacing.md,
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    gap: spacing.md,
  },
  detailItem: {
    gap: spacing.xs,
  },
  detailLabel: {
    fontSize: 11,
    fontWeight: '600',
    color: colors.text.muted,
    letterSpacing: 0.5,
  },
  detailText: {
    fontSize: 15,
    color: colors.text.secondary,
    lineHeight: 22,
  },

  // ═══════════════════════════════════════════════════════════════════
  // CANCEL REGISTRATION
  // ═══════════════════════════════════════════════════════════════════
  cancelSection: {
    marginTop: spacing.lg,
    alignItems: 'center',
  },
  cancelButton: {
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.lg,
  },
  cancelButtonText: {
    color: colors.status.error,
    fontSize: 14,
    fontWeight: '500',
  },
});
