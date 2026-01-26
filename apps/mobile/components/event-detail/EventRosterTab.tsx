import { useEffect, useState, useMemo, useRef, useCallback } from 'react';
import {
  View,
  StyleSheet,
  ScrollView,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { eventService } from '@bhmhockey/api-client';
import type {
  EventRegistrationDto,
  EventDto,
  TeamAssignment,
  RosterOrderItem,
  WaitlistOrderItem,
} from '@bhmhockey/shared';
import { useEventStore } from '../../stores/eventStore';
import { EmptyState } from '../EmptyState';
import { SectionHeader } from '../SectionHeader';
import { DraggableRoster } from '../DraggableRoster';
import { DraggableWaitlist } from '../DraggableWaitlist';
import { PlayerDetailModal } from '../PlayerDetailModal';
import { DraftModeRoster } from './DraftModeRoster';
import { colors, spacing, radius } from '../../theme';

interface EventRosterTabProps {
  eventId: string;
  event: EventDto;
  canManage: boolean;
}

export function EventRosterTab({ eventId, event, canManage }: EventRosterTabProps) {
  const [allRegistrations, setAllRegistrations] = useState<EventRegistrationDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedPlayer, setSelectedPlayer] = useState<EventRegistrationDto | null>(null);
  const [isModalVisible, setIsModalVisible] = useState(false);
  const isUpdatingRoster = useRef(false);
  const hasLoadedOnce = useRef(false);
  const { updatePaymentStatus, updateTeamAssignment, removeRegistration } = useEventStore();

  // Filter into registered and waitlisted
  const registrations = useMemo(
    () => allRegistrations.filter((r) => !r.isWaitlisted),
    [allRegistrations]
  );

  const waitlist = useMemo(
    () =>
      allRegistrations
        .filter((r) => r.isWaitlisted)
        .sort((a, b) => (a.waitlistPosition ?? 999) - (b.waitlistPosition ?? 999)),
    [allRegistrations]
  );

  // Reset loaded state when eventId changes
  useEffect(() => {
    hasLoadedOnce.current = false;
  }, [eventId]);

  // Load registrations with cleanup to prevent memory leaks
  // Re-fetch when registeredCount or waitlistCount changes (e.g., after registration)
  useEffect(() => {
    let cancelled = false;

    const loadRegistrations = async () => {
      if (!eventId) return;
      // Only show loading spinner on initial load, not on re-fetches
      if (!hasLoadedOnce.current) {
        setIsLoading(true);
      }
      try {
        const registrationsData = await eventService.getRegistrations(eventId);
        if (!cancelled) {
          setAllRegistrations(registrationsData);
          hasLoadedOnce.current = true;
        }
      } catch (error) {
        if (!cancelled) {
          console.error('Failed to load registrations:', error);
          Alert.alert('Error', 'Failed to load roster data');
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    };

    loadRegistrations();
    return () => {
      cancelled = true;
    };
  }, [eventId, event.registeredCount, event.waitlistCount]);

  const reloadRegistrations = useCallback(async () => {
    if (!eventId) return;
    try {
      const data = await eventService.getRegistrations(eventId);
      setAllRegistrations(data);
    } catch (error) {
      Alert.alert('Error', 'Failed to reload registrations');
    }
  }, [eventId]);

  // Player modal handlers
  const handlePlayerPress = (registration: EventRegistrationDto) => {
    setSelectedPlayer(registration);
    setIsModalVisible(true);
  };

  const handleCloseModal = () => {
    setIsModalVisible(false);
    setSelectedPlayer(null);
  };

  // Payment handlers
  const handleMarkPaid = async (registration: EventRegistrationDto) => {
    Alert.alert('Mark as Paid', `Mark payment from ${registration.user.firstName} as verified?`, [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Yes, Mark Paid',
        onPress: async () => {
          const result = await updatePaymentStatus(eventId, registration.id, 'Verified');
          if (result) {
            await reloadRegistrations();
            const message = result.promoted
              ? 'Payment verified and user promoted to roster'
              : 'Payment verified';
            Alert.alert('Success', message);
          } else {
            Alert.alert('Error', 'Failed to verify payment');
          }
        },
      },
    ]);
  };

  const handleVerifyPayment = async (registration: EventRegistrationDto) => {
    Alert.alert('Verify Payment', `Verify payment from ${registration.user.firstName}?`, [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Yes, Verify',
        onPress: async () => {
          const result = await updatePaymentStatus(eventId, registration.id, 'Verified');
          if (result) {
            await reloadRegistrations();
            const message = result.promoted
              ? 'Payment verified and user promoted to roster'
              : 'Payment verified';
            Alert.alert('Success', message);
          } else {
            Alert.alert('Error', 'Failed to verify payment');
          }
        },
      },
    ]);
  };

  const handleResetPayment = async (registration: EventRegistrationDto) => {
    Alert.alert(
      'Reset Payment',
      `Reset payment status for ${registration.user.firstName} to Unpaid?`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Reset',
          style: 'destructive',
          onPress: async () => {
            const success = await updatePaymentStatus(eventId, registration.id, 'Pending');
            if (success) {
              await reloadRegistrations();
            } else {
              Alert.alert('Error', 'Failed to reset payment status');
            }
          },
        },
      ]
    );
  };

  // Team swap handler
  const handleSwapTeam = async (registration: EventRegistrationDto) => {
    const newTeam: TeamAssignment = registration.teamAssignment === 'Black' ? 'White' : 'Black';
    const success = await updateTeamAssignment(eventId, registration.id, newTeam);
    if (success) await reloadRegistrations();
  };

  // Remove handler
  const handleRemove = async (registration: EventRegistrationDto) => {
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
            const success = await removeRegistration(eventId, registration.id);
            if (success) await reloadRegistrations();
          },
        },
      ]
    );
  };

  // Move to roster handler (waitlisted -> rostered)
  const handleMoveToRoster = async (registration: EventRegistrationDto) => {
    const userName = `${registration.user.firstName} ${registration.user.lastName}`;

    Alert.alert(
      'Move to Roster',
      `Move ${userName} from the waitlist to the roster?`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Move',
          onPress: async () => {
            try {
              const result = await eventService.moveToRoster(eventId, registration.id);
              if (result.success) {
                await reloadRegistrations();
                Alert.alert('Success', 'Player moved to roster');
              } else {
                Alert.alert('Error', result.message || 'Failed to move player');
              }
            } catch (error: any) {
              const message = error?.response?.data?.message || 'Failed to move player to roster';
              Alert.alert('Error', message);
            }
          },
        },
      ]
    );
  };

  // Move to waitlist handler (rostered -> waitlisted)
  const handleMoveToWaitlist = async (registration: EventRegistrationDto) => {
    const userName = `${registration.user.firstName} ${registration.user.lastName}`;

    Alert.alert(
      'Move to Waitlist',
      `Move ${userName} from the roster to the waitlist?\n\nTheir team assignment and payment deadline will be cleared.`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Move',
          style: 'destructive',
          onPress: async () => {
            try {
              const result = await eventService.moveToWaitlist(eventId, registration.id);
              if (result.success) {
                await reloadRegistrations();
                Alert.alert('Success', 'Player moved to waitlist');
              } else {
                Alert.alert('Error', result.message || 'Failed to move player');
              }
            } catch (error: any) {
              const message = error?.response?.data?.message || 'Failed to move player to waitlist';
              Alert.alert('Error', message);
            }
          },
        },
      ]
    );
  };

  // Waitlist item tap handler - shows player detail modal (same as roster)
  const handleWaitlistItemPress = (registration: EventRegistrationDto) => {
    handlePlayerPress(registration);
  };

  // Roster order change handler
  const handleRosterChange = async (items: RosterOrderItem[]) => {
    if (isUpdatingRoster.current) return;
    isUpdatingRoster.current = true;

    // Optimistic update
    setAllRegistrations((prev) => {
      const updateMap = new Map(
        items.map((item) => [
          item.registrationId,
          {
            teamAssignment: item.teamAssignment,
            rosterOrder: item.rosterOrder,
          },
        ])
      );

      return prev.map((reg) => {
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

    try {
      await eventService.updateRosterOrder(eventId, items);
    } catch (error) {
      Alert.alert('Error', 'Failed to save roster order. Please try again.');
      await reloadRegistrations();
    } finally {
      isUpdatingRoster.current = false;
    }
  };

  // Waitlist reorder handler
  const handleWaitlistReorder = async (items: WaitlistOrderItem[]) => {
    try {
      await eventService.reorderWaitlist(eventId, items);
    } catch (error) {
      Alert.alert('Error', 'Failed to save waitlist order. Please try again.');
      await reloadRegistrations();
      throw error; // Re-throw so DraggableWaitlist can revert optimistic update
    }
  };

  if (isLoading) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
      </View>
    );
  }

  // Draft mode: non-organizers see simplified view without roster details
  if (!event.isRosterPublished && !canManage) {
    return <DraftModeRoster event={event} />;
  }

  const showPayment = canManage;

  return (
    <View style={styles.container}>
      <ScrollView style={styles.scrollView} contentContainerStyle={styles.scrollContent}>
        {/* Roster View */}
        <View>
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
          <View style={styles.waitlistContainer}>
            <SectionHeader title="Waitlist" count={waitlist.length} />
            <View style={styles.waitlistSection}>
              <DraggableWaitlist
                waitlist={waitlist}
                canManage={canManage}
                onItemPress={handleWaitlistItemPress}
                onReorder={handleWaitlistReorder}
              />
            </View>
          </View>
        )}
      </ScrollView>

      {/* Player Detail Modal */}
      <PlayerDetailModal
        visible={isModalVisible}
        registration={selectedPlayer}
        showPayment={showPayment}
        isAdmin={canManage}
        onClose={handleCloseModal}
        onSwapTeam={canManage && !selectedPlayer?.isWaitlisted ? handleSwapTeam : undefined}
        onRemove={canManage ? handleRemove : undefined}
        onMarkPaid={canManage ? handleMarkPaid : undefined}
        onVerifyPayment={canManage ? handleVerifyPayment : undefined}
        onResetPayment={canManage ? handleResetPayment : undefined}
        onMoveToRoster={canManage && selectedPlayer?.isWaitlisted ? handleMoveToRoster : undefined}
        onMoveToWaitlist={canManage && !selectedPlayer?.isWaitlisted ? handleMoveToWaitlist : undefined}
      />
    </View>
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
  scrollContent: {
    paddingHorizontal: spacing.md,
    paddingTop: spacing.md,
    paddingBottom: spacing.xl,
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
  waitlistContainer: {
    marginTop: spacing.lg,
  },
  waitlistSection: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
});
