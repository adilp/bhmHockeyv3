import { useEffect, useState, useMemo, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { useLocalSearchParams } from 'expo-router';
import { eventService } from '@bhmhockey/api-client';
import type { EventRegistrationDto, EventDto, TeamAssignment, RosterOrderItem } from '@bhmhockey/shared';
import { useEventStore } from '../../../stores/eventStore';
import { EmptyState, SectionHeader, DraggableRoster, PlayerDetailModal } from '../../../components';
import { colors, spacing, radius } from '../../../theme';

export default function EventRegistrationsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [allRegistrations, setAllRegistrations] = useState<EventRegistrationDto[]>([]);
  const [event, setEvent] = useState<EventDto | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedPlayer, setSelectedPlayer] = useState<EventRegistrationDto | null>(null);
  const [isModalVisible, setIsModalVisible] = useState(false);
  const isUpdatingRoster = useRef(false);
  const { updatePaymentStatus, updateTeamAssignment, removeRegistration } = useEventStore();

  // Filter into registered and waitlisted
  const registrations = useMemo(() =>
    allRegistrations.filter(r => !r.isWaitlisted),
    [allRegistrations]
  );

  const waitlist = useMemo(() =>
    allRegistrations.filter(r => r.isWaitlisted).sort(
      (a, b) => (a.waitlistPosition ?? 999) - (b.waitlistPosition ?? 999)
    ),
    [allRegistrations]
  );

  useEffect(() => {
    loadData();
  }, [id]);

  const loadData = async () => {
    if (!id) return;
    setIsLoading(true);
    try {
      // Load event and registrations in parallel
      const [eventData, registrationsData] = await Promise.all([
        eventService.getById(id),
        eventService.getRegistrations(id),
      ]);
      setEvent(eventData);
      setAllRegistrations(registrationsData);
    } catch (error) {
      console.error('Failed to load data:', error);
      Alert.alert('Error', 'Failed to load event data');
    } finally {
      setIsLoading(false);
    }
  };

  const reloadRegistrations = async () => {
    if (!id) return;
    try {
      const data = await eventService.getRegistrations(id);
      setAllRegistrations(data);
    } catch (error) {
      Alert.alert('Error', 'Failed to reload registrations');
    }
  };

  // Player modal handlers
  const handlePlayerPress = (registration: EventRegistrationDto) => {
    setSelectedPlayer(registration);
    setIsModalVisible(true);
  };

  const handleCloseModal = () => {
    setIsModalVisible(false);
    setSelectedPlayer(null);
  };

  // Payment handlers (now called from modal)
  const handleMarkPaid = async (registration: EventRegistrationDto) => {
    if (!id) return;

    Alert.alert(
      'Mark as Paid',
      `Mark payment from ${registration.user.firstName} as verified?`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Yes, Mark Paid',
          onPress: async () => {
            const success = await updatePaymentStatus(id, registration.id, 'Verified');
            if (success) await reloadRegistrations();
          },
        },
      ]
    );
  };

  const handleVerifyPayment = async (registration: EventRegistrationDto) => {
    if (!id) return;

    Alert.alert(
      'Verify Payment',
      `Verify payment from ${registration.user.firstName}?`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Yes, Verify',
          onPress: async () => {
            const success = await updatePaymentStatus(id, registration.id, 'Verified');
            if (success) await reloadRegistrations();
          },
        },
      ]
    );
  };

  const handleResetPayment = async (registration: EventRegistrationDto) => {
    if (!id) return;

    Alert.alert(
      'Reset Payment',
      `Reset payment status for ${registration.user.firstName} to Unpaid?`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Reset',
          style: 'destructive',
          onPress: async () => {
            const success = await updatePaymentStatus(id, registration.id, 'Pending');
            if (success) await reloadRegistrations();
          },
        },
      ]
    );
  };

  // Team swap handler (from modal)
  const handleSwapTeam = async (registration: EventRegistrationDto) => {
    if (!id) return;

    const newTeam: TeamAssignment = registration.teamAssignment === 'Black' ? 'White' : 'Black';
    const success = await updateTeamAssignment(id, registration.id, newTeam);
    if (success) await reloadRegistrations();
  };

  // Remove handler (from modal)
  const handleRemove = async (registration: EventRegistrationDto) => {
    if (!id) return;

    const isWaitlisted = registration.isWaitlisted;
    const userName = `${registration.user.firstName} ${registration.user.lastName}`;

    Alert.alert(
      isWaitlisted ? 'Remove from Waitlist' : 'Remove Registration',
      `Are you sure you want to remove ${userName}?${!isWaitlisted ? '\n\nThe next person on the waitlist will be promoted.' : ''}`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Remove',
          style: 'destructive',
          onPress: async () => {
            const success = await removeRegistration(id, registration.id);
            if (success) await reloadRegistrations();
          },
        },
      ]
    );
  };

  // Roster order change handler with optimistic update and concurrency guard
  const handleRosterChange = async (items: RosterOrderItem[]) => {
    if (!id) return;

    // Prevent concurrent updates
    if (isUpdatingRoster.current) return;
    isUpdatingRoster.current = true;

    // Optimistic update: immediately update local state
    setAllRegistrations(prev => {
      // Create a map of registration ID -> new values
      const updateMap = new Map(
        items.map(item => [item.registrationId, {
          teamAssignment: item.teamAssignment,
          rosterOrder: item.rosterOrder
        }])
      );

      // Update each registration with its new team/order
      return prev.map(reg => {
        const update = updateMap.get(reg.id);
        if (update) {
          return {
            ...reg,
            teamAssignment: update.teamAssignment,
            rosterOrder: update.rosterOrder,
          };
        }
        return reg;
      });
    });

    // Save to API in background
    try {
      await eventService.updateRosterOrder(id, items);
    } catch (error) {
      Alert.alert('Error', 'Failed to save roster order. Please try again.');
      // Refetch to restore correct state on error
      await reloadRegistrations();
    } finally {
      isUpdatingRoster.current = false;
    }
  };

  // Waitlist item render - canManage checked at render time
  const renderWaitlistItem = (item: EventRegistrationDto, isAdmin: boolean) => (
    <View key={item.id} style={styles.waitlistRow}>
      <View style={styles.waitlistPositionBadge}>
        <Text style={styles.waitlistPositionText} allowFontScaling={false}>#{item.waitlistPosition}</Text>
      </View>
      <View style={styles.waitlistUserInfo}>
        <Text style={styles.waitlistUserName} allowFontScaling={false}>
          {item.user.firstName} {item.user.lastName}
        </Text>
        <Text style={styles.waitlistUserMeta} allowFontScaling={false}>
          {item.registeredPosition || 'Skater'}
        </Text>
      </View>
      {isAdmin && (
        <TouchableOpacity
          style={styles.removeButtonSmall}
          onPress={() => handleRemove(item)}
        >
          <Text style={styles.removeButtonText} allowFontScaling={false}>Remove</Text>
        </TouchableOpacity>
      )}
    </View>
  );

  // Show loading while fetching
  if (isLoading || !event) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
      </View>
    );
  }

  // Permission check - only organizers/admins can manage the roster
  const canManage = event.canManage;
  // Show payment info only to admins
  const showPayment = canManage;

  // Debug logging - remove after testing
  console.log('📋 Registrations - canManage:', canManage, 'eventId:', event.id);

  return (
    <GestureHandlerRootView style={styles.container}>
      <ScrollView style={styles.scrollView}>
        {/* Summary Header */}
        {event && (
          <View style={styles.summaryHeader}>
            <View style={styles.statBox}>
              <Text style={styles.statValue} allowFontScaling={false}>{registrations.length}</Text>
              <Text style={styles.statLabel} allowFontScaling={false}>Registered</Text>
            </View>
            {showPayment && (
              <>
                <View style={styles.statBox}>
                  <Text style={[styles.statValue, { color: colors.primary.green }]} allowFontScaling={false}>
                    {registrations.filter(r => r.paymentStatus === 'Verified').length}
                  </Text>
                  <Text style={styles.statLabel} allowFontScaling={false}>Paid</Text>
                </View>
                <View style={styles.statBox}>
                  <Text style={[styles.statValue, { color: colors.status.error }]} allowFontScaling={false}>
                    {registrations.filter(r => r.paymentStatus !== 'Verified').length}
                  </Text>
                  <Text style={styles.statLabel} allowFontScaling={false}>Unpaid</Text>
                </View>
              </>
            )}
            {waitlist.length > 0 && (
              <View style={styles.statBox}>
                <Text style={[styles.statValue, { color: colors.status.warning }]} allowFontScaling={false}>
                  {waitlist.length}
                </Text>
                <Text style={styles.statLabel} allowFontScaling={false}>Waitlist</Text>
              </View>
            )}
          </View>
        )}

        {/* Roster View */}
        <View style={styles.section}>
          {registrations.length === 0 ? (
            <EmptyState
              icon="👥"
              title="No Registrations"
              message="No one has registered for this event yet"
            />
          ) : (
            <DraggableRoster
              registrations={registrations}
              onPlayerPress={handlePlayerPress}
              onRosterChange={canManage ? handleRosterChange : undefined}
              readOnly={!canManage}
            />
          )}
        </View>

        {/* Waitlist Section */}
        {waitlist.length > 0 && (
          <View style={styles.section}>
            <SectionHeader title="Waitlist" count={waitlist.length} />
            <View style={styles.waitlistSection}>
              {waitlist.map((item) => renderWaitlistItem(item, canManage))}
            </View>
          </View>
        )}

        {/* Bottom padding */}
        <View style={{ height: 40 }} />
      </ScrollView>

      {/* Player Detail Modal */}
      <PlayerDetailModal
        visible={isModalVisible}
        registration={selectedPlayer}
        showPayment={showPayment}
        isAdmin={canManage}
        onClose={handleCloseModal}
        onSwapTeam={canManage ? handleSwapTeam : undefined}
        onRemove={canManage ? handleRemove : undefined}
        onMarkPaid={canManage ? handleMarkPaid : undefined}
        onVerifyPayment={canManage ? handleVerifyPayment : undefined}
        onResetPayment={canManage ? handleResetPayment : undefined}
      />
    </GestureHandlerRootView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  scrollView: {
    flex: 1,
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
  summaryHeader: {
    flexDirection: 'row',
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  statBox: {
    flex: 1,
    alignItems: 'center',
  },
  statValue: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text.primary,
  },
  statLabel: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: 2,
  },
  section: {
    padding: spacing.md,
  },
  // Waitlist styles
  waitlistSection: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
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
    borderRadius: 16,
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
    marginTop: 2,
  },
  removeButtonSmall: {
    backgroundColor: 'transparent',
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: radius.sm,
    borderWidth: 1,
    borderColor: colors.status.error,
  },
  removeButtonText: {
    color: colors.status.error,
    fontSize: 12,
    fontWeight: '600',
  },
});
