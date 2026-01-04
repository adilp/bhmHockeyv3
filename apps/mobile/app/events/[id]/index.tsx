import { useEffect, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  ActionSheetIOS,
  Platform,
} from 'react-native';
import { useLocalSearchParams, useRouter, useFocusEffect, Stack } from 'expo-router';
import { useEventStore } from '../../../stores/eventStore';
import { useAuthStore } from '../../../stores/authStore';
import { openVenmoPayment, getPaymentStatusInfo } from '../../../utils/venmo';
import { Badge } from '../../../components';
import { colors, spacing, radius } from '../../../theme';
import type { Position } from '@bhmhockey/shared';

export default function EventDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { isAuthenticated, user } = useAuthStore();

  const {
    selectedEvent,
    isLoading,
    error,
    fetchEventById,
    register,
    cancelRegistration,
    clearSelectedEvent,
    clearError,
    markPayment,
    cancelEvent,
  } = useEventStore();

  useFocusEffect(
    useCallback(() => {
      if (id) {
        fetchEventById(id);
      }
      return () => {
        clearSelectedEvent();
        clearError();
      };
    }, [id])
  );


  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  };

  const formatTime = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
    });
  };

  const handleRegister = async () => {
    if (!id || !isAuthenticated || !user) return;

    const positions = user.positions;
    const positionCount = positions
      ? Object.keys(positions).filter(k => positions[k as keyof typeof positions]).length
      : 0;

    if (positionCount === 0) {
      Alert.alert(
        'Set Up Profile',
        'Please set up your positions in your profile before registering for events.',
        [
          { text: 'Cancel', style: 'cancel' },
          { text: 'Go to Profile', onPress: () => router.push('/(tabs)/profile') },
        ]
      );
      return;
    }

    // Helper to show result message
    const showResultMessage = (result: { status: string; waitlistPosition?: number | null; message: string } | null, position: string) => {
      if (!result) return;
      if (result.status === 'Waitlisted') {
        Alert.alert('Added to Waitlist', `You're #${result.waitlistPosition} on the waitlist as a ${position}. We'll notify you when a spot opens up!`);
      } else {
        Alert.alert('Success', `You have been registered as a ${position}!`);
      }
    };

    if (positionCount === 1) {
      const position = positions?.goalie ? 'Goalie' : 'Skater';
      const result = await register(id, position as Position);
      showResultMessage(result, position);
      return;
    }

    if (Platform.OS === 'ios') {
      ActionSheetIOS.showActionSheetWithOptions(
        {
          options: ['Cancel', 'Goalie', 'Skater'],
          cancelButtonIndex: 0,
          title: 'Register as which position?',
        },
        async (buttonIndex) => {
          if (buttonIndex === 1) {
            const result = await register(id, 'Goalie');
            showResultMessage(result, 'Goalie');
          } else if (buttonIndex === 2) {
            const result = await register(id, 'Skater');
            showResultMessage(result, 'Skater');
          }
        }
      );
    } else {
      Alert.alert(
        'Register as which position?',
        'Select the position you want to play',
        [
          { text: 'Cancel', style: 'cancel' },
          {
            text: 'Goalie',
            onPress: async () => {
              const result = await register(id, 'Goalie');
              showResultMessage(result, 'Goalie');
            },
          },
          {
            text: 'Skater',
            onPress: async () => {
              const result = await register(id, 'Skater');
              showResultMessage(result, 'Skater');
            },
          },
        ]
      );
    }
  };

  const handleCancelRegistration = async () => {
    if (!id) return;

    const isWaitlisted = selectedEvent?.amIWaitlisted;
    const title = isWaitlisted ? 'Leave Waitlist' : 'Cancel Registration';
    const message = isWaitlisted
      ? 'Are you sure you want to leave the waitlist?'
      : 'Are you sure you want to cancel your registration?';
    const successMessage = isWaitlisted
      ? 'You have been removed from the waitlist.'
      : 'Your registration has been cancelled.';

    Alert.alert(
      title,
      message,
      [
        { text: 'No', style: 'cancel' },
        {
          text: 'Yes',
          style: 'destructive',
          onPress: async () => {
            const success = await cancelRegistration(id);
            if (success) Alert.alert('Done', successMessage);
          },
        },
      ]
    );
  };

  const handlePayWithVenmo = async () => {
    if (!selectedEvent || !selectedEvent.creatorVenmoHandle) {
      Alert.alert('Error', 'Organizer has not set up their Venmo handle.');
      return;
    }

    await openVenmoPayment(
      selectedEvent.creatorVenmoHandle,
      selectedEvent.cost,
      selectedEvent.name || 'Hockey'
    );
  };

  const handleMarkAsPaid = async () => {
    if (!id) return;

    Alert.alert(
      'Confirm Payment',
      'Have you completed the Venmo payment to the organizer? They will verify receipt of payment.',
      [
        { text: 'Not Yet', style: 'cancel' },
        {
          text: "Yes, I've Paid",
          onPress: async () => {
            const success = await markPayment(id);
            if (success) {
              Alert.alert(
                'Payment Marked',
                'The organizer will verify your payment. You can check your Venmo app to confirm the transaction.'
              );
            }
          },
        },
      ]
    );
  };

  const handleDeleteEvent = () => {
    if (!id) return;

    Alert.alert(
      'Delete Event',
      'Are you sure you want to delete this event? This action cannot be undone. All registrations will be cancelled.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Delete',
          style: 'destructive',
          onPress: async () => {
            const success = await cancelEvent(id);
            if (success) {
              Alert.alert('Event Deleted', 'The event has been cancelled.');
              router.back();
            }
          },
        },
      ]
    );
  };

  const spotsLeft = selectedEvent ? selectedEvent.maxPlayers - selectedEvent.registeredCount : 0;
  const isFull = spotsLeft <= 0;
  const isWaitlisted = selectedEvent?.amIWaitlisted;
  // Can register (or join waitlist) if authenticated and not already registered/waitlisted
  const canRegister = isAuthenticated && selectedEvent && !selectedEvent.isRegistered && !isWaitlisted;

  return (
    <>
      <Stack.Screen
        options={{
          title: selectedEvent?.name || 'Event',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
          headerBackTitle: 'Back',
          headerRight: selectedEvent?.canManage ? () => (
            <TouchableOpacity
              onPress={() => router.push(`/events/edit?id=${id}`)}
              style={styles.headerButton}
            >
              <Text style={styles.headerButtonText}>Edit</Text>
            </TouchableOpacity>
          ) : undefined,
        }}
      />

      {(isLoading || !selectedEvent) ? (
        <View style={styles.centered}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={styles.loadingText}>Loading event...</Text>
        </View>
      ) : error ? (
        <View style={styles.centered}>
          <Text style={styles.errorText}>{error}</Text>
        </View>
      ) : (
      <ScrollView style={styles.container}>
      {/* Header Section */}
      <View style={styles.header}>
        {selectedEvent.name && (
          <Text style={styles.title}>{selectedEvent.name}</Text>
        )}
        <Text style={styles.organization}>
          {selectedEvent.organizationName || 'Pickup Game'}
        </Text>
        <View style={styles.badgeRow}>
          {selectedEvent.canManage && (
            <Badge variant="purple">Organizer</Badge>
          )}
          {selectedEvent.isRegistered && (
            <Badge variant="green">You're Registered</Badge>
          )}
          {isWaitlisted && (
            <Badge variant="warning">#{selectedEvent.myWaitlistPosition} on Waitlist</Badge>
          )}
          {selectedEvent.myTeamAssignment && (
            <View style={[
              styles.teamBadge,
              selectedEvent.myTeamAssignment === 'Black' ? styles.teamBlack : styles.teamWhite
            ]}>
              <Text style={[
                styles.teamBadgeText,
                selectedEvent.myTeamAssignment === 'Black' ? styles.teamBlackText : styles.teamWhiteText
              ]}>
                Team {selectedEvent.myTeamAssignment}
              </Text>
            </View>
          )}
          {selectedEvent.visibility === 'InviteOnly' && (
            <Badge variant="warning">Invite Only</Badge>
          )}
          {selectedEvent.visibility === 'OrganizationMembers' && (
            <Badge variant="teal">Members Only</Badge>
          )}
        </View>
      </View>

      {/* Date & Time Section */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Date & Time</Text>
        <View style={styles.infoRow}>
          <Text style={styles.infoLabel}>Date</Text>
          <Text style={styles.infoValue}>{formatDate(selectedEvent.eventDate)}</Text>
        </View>
        <View style={styles.infoRow}>
          <Text style={styles.infoLabel}>Time</Text>
          <Text style={styles.infoValue}>{formatTime(selectedEvent.eventDate)}</Text>
        </View>
        <View style={[styles.infoRow, styles.infoRowLast]}>
          <Text style={styles.infoLabel}>Duration</Text>
          <Text style={styles.infoValue}>{selectedEvent.duration} minutes</Text>
        </View>
      </View>

      {/* Location Section */}
      {selectedEvent.venue && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Location</Text>
          <Text style={styles.venueText}>{selectedEvent.venue}</Text>
        </View>
      )}

      {/* Description Section */}
      {selectedEvent.description && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>About</Text>
          <Text style={styles.descriptionText}>{selectedEvent.description}</Text>
        </View>
      )}

      {/* Availability Section */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Availability</Text>
        <View style={styles.availabilityContainer}>
          <View style={styles.availabilityInfo}>
            <Text style={[styles.spotsNumber, isFull && styles.spotsNumberFull]}>
              {spotsLeft}
            </Text>
            <Text style={styles.spotsLabel}>spots left</Text>
          </View>
          <View style={styles.progressContainer}>
            <View style={styles.progressBar}>
              <View
                style={[
                  styles.progressFill,
                  { width: `${(selectedEvent.registeredCount / selectedEvent.maxPlayers) * 100}%` },
                  isFull && styles.progressFillFull,
                ]}
              />
            </View>
            <Text style={styles.progressText}>
              {selectedEvent.registeredCount} / {selectedEvent.maxPlayers} registered
              {selectedEvent.waitlistCount > 0 && ` · ${selectedEvent.waitlistCount} on waitlist`}
            </Text>
          </View>
        </View>
      </View>

      {/* Cost Section */}
      {selectedEvent.cost > 0 && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Cost</Text>
          <Text style={styles.costText}>${selectedEvent.cost.toFixed(2)}</Text>
        </View>
      )}

      {/* Payment Section */}
      {selectedEvent.isRegistered && selectedEvent.cost > 0 && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Payment</Text>

          {selectedEvent.myPaymentStatus && (
            <View style={[
              styles.paymentStatusBadge,
              { backgroundColor: getPaymentStatusInfo(selectedEvent.myPaymentStatus).backgroundColor }
            ]}>
              <Text style={[
                styles.paymentStatusText,
                { color: getPaymentStatusInfo(selectedEvent.myPaymentStatus).color }
              ]}>
                {getPaymentStatusInfo(selectedEvent.myPaymentStatus).label}
              </Text>
            </View>
          )}

          {selectedEvent.myPaymentStatus === 'Pending' && (
            <View style={styles.paymentActions}>
              {selectedEvent.creatorVenmoHandle ? (
                <>
                  <TouchableOpacity style={styles.venmoButton} onPress={handlePayWithVenmo}>
                    <Text style={styles.venmoButtonText}>Pay with Venmo</Text>
                  </TouchableOpacity>

                  <TouchableOpacity style={styles.markPaidButton} onPress={handleMarkAsPaid}>
                    <Text style={styles.markPaidButtonText}>I've Already Paid</Text>
                  </TouchableOpacity>

                  <Text style={styles.paymentDisclaimer}>
                    Payment goes directly to the organizer via Venmo. BHM Hockey does not
                    process payments or mediate disputes.
                  </Text>
                </>
              ) : (
                <Text style={styles.noVenmoText}>
                  Organizer hasn't set up Venmo. Contact them directly for payment.
                </Text>
              )}
            </View>
          )}

          {selectedEvent.myPaymentStatus === 'MarkedPaid' && (
            <Text style={styles.waitingText}>
              Your payment is awaiting verification by the organizer.
            </Text>
          )}

          {selectedEvent.myPaymentStatus === 'Verified' && (
            <Text style={styles.verifiedText}>
              Your payment has been verified. You're all set!
            </Text>
          )}
        </View>
      )}

      {/* Registration Deadline */}
      {selectedEvent.registrationDeadline && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Registration Deadline</Text>
          <Text style={styles.deadlineText}>
            {formatDate(selectedEvent.registrationDeadline)} at {formatTime(selectedEvent.registrationDeadline)}
          </Text>
        </View>
      )}

      {/* Action Button */}
      {isAuthenticated && (
        <View style={styles.buttonContainer}>
          {selectedEvent.isRegistered ? (
            <TouchableOpacity style={styles.cancelButton} onPress={handleCancelRegistration}>
              <Text style={styles.cancelButtonText}>Cancel Registration</Text>
            </TouchableOpacity>
          ) : isWaitlisted ? (
            <TouchableOpacity style={styles.cancelButton} onPress={handleCancelRegistration}>
              <Text style={styles.cancelButtonText}>Leave Waitlist</Text>
            </TouchableOpacity>
          ) : (
            <TouchableOpacity
              style={[styles.registerButton, isFull && styles.waitlistButton]}
              onPress={handleRegister}
            >
              <Text style={[styles.registerButtonText, isFull && styles.waitlistButtonText]}>
                {isFull ? 'Join Waitlist' : 'Register for Event'}
              </Text>
            </TouchableOpacity>
          )}
        </View>
      )}

      {!isAuthenticated && (
        <View style={styles.loginPrompt}>
          <Text style={styles.loginPromptText}>Log in to register for this event</Text>
        </View>
      )}

      {/* Organizer Actions */}
      {selectedEvent.canManage && (
        <View style={styles.organizerSection}>
          <TouchableOpacity
            style={styles.viewRegistrationsButton}
            onPress={() => router.push(`/events/${id}/registrations`)}
          >
            <Text style={styles.viewRegistrationsButtonText}>
              View Registrations ({selectedEvent.registeredCount})
            </Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={styles.deleteEventButton}
            onPress={handleDeleteEvent}
          >
            <Text style={styles.deleteEventButtonText}>Delete Event</Text>
          </TouchableOpacity>
        </View>
      )}

      <View style={{ height: 40 }} />
    </ScrollView>
      )}
    </>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.lg,
    backgroundColor: colors.bg.darkest,
  },
  loadingText: {
    marginTop: spacing.sm,
    fontSize: 16,
    color: colors.text.muted,
  },
  errorText: {
    fontSize: 16,
    color: colors.status.error,
    textAlign: 'center',
  },
  header: {
    backgroundColor: colors.bg.dark,
    padding: spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  organization: {
    fontSize: 16,
    color: colors.text.muted,
  },
  badgeRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.sm,
    marginTop: spacing.md,
  },
  section: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    marginTop: spacing.sm,
  },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.md,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  infoRowLast: {
    borderBottomWidth: 0,
  },
  infoLabel: {
    fontSize: 16,
    color: colors.text.muted,
  },
  infoValue: {
    fontSize: 16,
    color: colors.text.primary,
    fontWeight: '500',
  },
  venueText: {
    fontSize: 16,
    color: colors.text.secondary,
    lineHeight: 24,
  },
  descriptionText: {
    fontSize: 16,
    color: colors.text.secondary,
    lineHeight: 24,
  },
  availabilityContainer: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  availabilityInfo: {
    alignItems: 'center',
    marginRight: spacing.lg,
  },
  spotsNumber: {
    fontSize: 36,
    fontWeight: '700',
    color: colors.primary.green,
  },
  spotsNumberFull: {
    color: colors.status.error,
  },
  spotsLabel: {
    fontSize: 14,
    color: colors.text.muted,
  },
  progressContainer: {
    flex: 1,
  },
  progressBar: {
    height: 8,
    backgroundColor: colors.bg.hover,
    borderRadius: radius.sm,
    overflow: 'hidden',
  },
  progressFill: {
    height: '100%',
    backgroundColor: colors.primary.green,
    borderRadius: radius.sm,
  },
  progressFillFull: {
    backgroundColor: colors.status.error,
  },
  progressText: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: spacing.sm,
  },
  costText: {
    fontSize: 28,
    fontWeight: '700',
    color: colors.text.primary,
  },
  deadlineText: {
    fontSize: 16,
    color: colors.text.secondary,
  },
  buttonContainer: {
    padding: spacing.lg,
  },
  registerButton: {
    backgroundColor: colors.primary.teal,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  registerButtonText: {
    color: colors.bg.darkest,
    fontSize: 18,
    fontWeight: '600',
  },
  cancelButton: {
    backgroundColor: 'transparent',
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    alignItems: 'center',
    borderWidth: 2,
    borderColor: colors.status.error,
  },
  cancelButtonText: {
    color: colors.status.error,
    fontSize: 18,
    fontWeight: '600',
  },
  disabledButton: {
    backgroundColor: colors.bg.hover,
  },
  disabledButtonText: {
    color: colors.text.subtle,
  },
  waitlistButton: {
    backgroundColor: colors.status.warning,
  },
  waitlistButtonText: {
    color: colors.bg.darkest,
  },
  loginPrompt: {
    padding: spacing.lg,
    alignItems: 'center',
  },
  loginPromptText: {
    fontSize: 16,
    color: colors.text.muted,
  },
  paymentStatusBadge: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    alignSelf: 'flex-start',
    marginBottom: spacing.md,
  },
  paymentStatusText: {
    fontSize: 14,
    fontWeight: '600',
  },
  paymentActions: {
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
    borderColor: '#008CFF',
  },
  markPaidButtonText: {
    color: '#008CFF',
    fontSize: 16,
    fontWeight: '600',
  },
  noVenmoText: {
    fontSize: 14,
    color: colors.text.muted,
    fontStyle: 'italic',
  },
  waitingText: {
    fontSize: 14,
    color: colors.status.warning,
  },
  verifiedText: {
    fontSize: 14,
    color: colors.primary.green,
    fontWeight: '500',
  },
  paymentDisclaimer: {
    fontSize: 12,
    color: colors.text.subtle,
    textAlign: 'center',
    marginTop: spacing.sm,
    fontStyle: 'italic',
  },
  organizerSection: {
    paddingHorizontal: spacing.lg,
  },
  viewRegistrationsButton: {
    backgroundColor: colors.primary.purple,
    paddingVertical: 14,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  viewRegistrationsButtonText: {
    color: colors.text.primary,
    fontSize: 16,
    fontWeight: '600',
  },
  deleteEventButton: {
    backgroundColor: 'transparent',
    paddingVertical: 14,
    borderRadius: radius.md,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.status.error,
    marginTop: spacing.sm,
  },
  deleteEventButtonText: {
    color: colors.status.error,
    fontSize: 16,
    fontWeight: '600',
  },
  headerButton: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
  },
  headerButtonText: {
    color: colors.primary.teal,
    fontSize: 16,
    fontWeight: '600',
  },
  // Team badge styles
  teamBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: radius.sm,
  },
  teamBlack: {
    backgroundColor: colors.bg.darkest,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  teamWhite: {
    backgroundColor: colors.text.primary,
  },
  teamBadgeText: {
    fontSize: 12,
    fontWeight: '700',
  },
  teamBlackText: {
    color: colors.text.primary,
  },
  teamWhiteText: {
    color: colors.bg.darkest,
  },
});
