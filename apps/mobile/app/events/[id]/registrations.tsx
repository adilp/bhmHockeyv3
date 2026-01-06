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
import type { EventRegistrationDto, TeamAssignment, RosterOrderItem } from '@bhmhockey/shared';
import { useEventStore } from '../../../stores/eventStore';
import { EmptyState, SectionHeader, DraggableRoster, PlayerDetailModal } from '../../../components';
import { colors, spacing, radius } from '../../../theme';

export default function EventRegistrationsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [allRegistrations, setAllRegistrations] = useState<EventRegistrationDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedPlayer, setSelectedPlayer] = useState<EventRegistrationDto | null>(null);
  const [isModalVisible, setIsModalVisible] = useState(false);
  const isUpdatingRoster = useRef(false);
  const { updatePaymentStatus, updateTeamAssignment, removeRegistration, selectedEvent, fetchEventById } = useEventStore();

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
      // Load event if not already loaded (needed for canManage and cost)
      if (!selectedEvent || selectedEvent.id !== id) {
        await fetchEventById(id);
      }
      await loadRegistrations();
    } finally {
      setIsLoading(false);
    }
  };

  const loadRegistrations = async () => {
    if (!id) return;
    try {
      const data = await eventService.getRegistrations(id);
      setAllRegistrations(data);
    } catch (error) {
      Alert.alert('Error', 'Failed to load registrations');
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
            if (success) await loadRegistrations();
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
            if (success) await loadRegistrations();
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
            if (success) await loadRegistrations();
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
    if (success) await loadRegistrations();
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
            if (success) await loadRegistrations();
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
      await loadRegistrations();
    } finally {
      isUpdatingRoster.current = false;
    }
  };

  // Waitlist item render
  const renderWaitlistItem = (item: EventRegistrationDto) => (
    <View key={item.id} style={styles.waitlistRow}>
      <View style={styles.waitlistPositionBadge}>
        <Text style={styles.waitlistPositionText}>#{item.waitlistPosition}</Text>
      </View>
      <View style={styles.waitlistUserInfo}>
        <Text style={styles.waitlistUserName}>
          {item.user.firstName} {item.user.lastName}
        </Text>
        <Text style={styles.waitlistUserMeta}>
          {item.registeredPosition || 'Skater'}
        </Text>
      </View>
      <TouchableOpacity
        style={styles.removeButtonSmall}
        onPress={() => handleRemove(item)}
      >
        <Text style={styles.removeButtonText}>Remove</Text>
      </TouchableOpacity>
    </View>
  );

  if (isLoading) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
      </View>
    );
  }

  // Always show payment status - useful for admins to track who paid
  const showPayment = true;

  return (
    <GestureHandlerRootView style={styles.container}>
      <ScrollView style={styles.scrollView}>
        {/* Summary Header */}
        {selectedEvent && (
          <View style={styles.summaryHeader}>
            <View style={styles.statBox}>
              <Text style={styles.statValue}>{registrations.length}</Text>
              <Text style={styles.statLabel}>Registered</Text>
            </View>
            {showPayment && (
              <>
                <View style={styles.statBox}>
                  <Text style={[styles.statValue, { color: colors.primary.green }]}>
                    {registrations.filter(r => r.paymentStatus === 'Verified').length}
                  </Text>
                  <Text style={styles.statLabel}>Paid</Text>
                </View>
                <View style={styles.statBox}>
                  <Text style={[styles.statValue, { color: colors.status.error }]}>
                    {registrations.filter(r => r.paymentStatus !== 'Verified').length}
                  </Text>
                  <Text style={styles.statLabel}>Unpaid</Text>
                </View>
              </>
            )}
            {waitlist.length > 0 && (
              <View style={styles.statBox}>
                <Text style={[styles.statValue, { color: colors.status.warning }]}>
                  {waitlist.length}
                </Text>
                <Text style={styles.statLabel}>Waitlist</Text>
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
              showPayment={showPayment}
              onPlayerPress={handlePlayerPress}
              onRosterChange={handleRosterChange}
            />
          )}
        </View>

        {/* Waitlist Section */}
        {waitlist.length > 0 && (
          <View style={styles.section}>
            <SectionHeader title="Waitlist" count={waitlist.length} />
            <View style={styles.waitlistSection}>
              {waitlist.map(renderWaitlistItem)}
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
        onClose={handleCloseModal}
        onSwapTeam={handleSwapTeam}
        onRemove={handleRemove}
        onMarkPaid={handleMarkPaid}
        onVerifyPayment={handleVerifyPayment}
        onResetPayment={handleResetPayment}
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
