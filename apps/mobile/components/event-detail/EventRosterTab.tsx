import { useEffect, useState, useMemo, useRef, useCallback } from 'react';
import {
  View,
  StyleSheet,
  ScrollView,
  ActivityIndicator,
  Alert,
  TouchableOpacity,
  Text,
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
import { AddPlayerModal } from './AddPlayerModal';
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
  const [isPublishing, setIsPublishing] = useState(false);
  const [isAddPlayerModalVisible, setIsAddPlayerModalVisible] = useState(false);
  const isUpdatingRoster = useRef(false);
  const hasLoadedOnce = useRef(false);
  const { updatePaymentStatus, updateTeamAssignment, removeRegistration, publishRoster } = useEventStore();

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

  const handlePlayerAdded = () => {
    // Reload registrations to show the newly added player
    reloadRegistrations();
  };

  const handlePublishRoster = async () => {
    Alert.alert(
      'Publish Roster',
      'This will notify all players of their placement. Players will see their team assignment and waitlist position. Continue?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Publish',
          onPress: async () => {
            setIsPublishing(true);
            try {
              const result = await publishRoster(eventId);
              if (result?.success) {
                Alert.alert(
                  'Roster Published',
                  `Roster is now live! ${result.notificationsSent} player${result.notificationsSent === 1 ? '' : 's'} notified.`
                );
              } else {
                Alert.alert('Error', result?.message || 'Failed to publish roster');
              }
            } catch (error) {
              Alert.alert('Error', 'Failed to publish roster. Please try again.');
            } finally {
              setIsPublishing(false);
            }
          },
        },
      ]
    );
  };

  // Payment handlers
  const handleMarkPaid = async (registration: EventRegistrationDto) => {
    // Optimistic update
    setAllRegistrations((prev) =>
      prev.map((reg) =>
        reg.id === registration.id ? { ...reg, paymentStatus: 'Verified' } : reg
      )
    );

    const result = await updatePaymentStatus(eventId, registration.id, 'Verified');
    if (!result) {
      Alert.alert('Error', 'Failed to verify payment');
      await reloadRegistrations();
    } else if (result.promoted) {
      // User was promoted from waitlist - need full refresh to get updated state
      await reloadRegistrations();
    }
  };

  const handleVerifyPayment = async (registration: EventRegistrationDto) => {
    // Optimistic update
    setAllRegistrations((prev) =>
      prev.map((reg) =>
        reg.id === registration.id ? { ...reg, paymentStatus: 'Verified' } : reg
      )
    );

    const result = await updatePaymentStatus(eventId, registration.id, 'Verified');
    if (!result) {
      Alert.alert('Error', 'Failed to verify payment');
      await reloadRegistrations();
    } else if (result.promoted) {
      // User was promoted from waitlist - need full refresh to get updated state
      await reloadRegistrations();
    }
  };

  const handleResetPayment = async (registration: EventRegistrationDto) => {
    // Optimistic update
    setAllRegistrations((prev) =>
      prev.map((reg) =>
        reg.id === registration.id ? { ...reg, paymentStatus: 'Pending' } : reg
      )
    );

    const success = await updatePaymentStatus(eventId, registration.id, 'Pending');
    if (!success) {
      Alert.alert('Error', 'Failed to reset payment status');
      await reloadRegistrations();
    }
  };

  // Team swap handler
  const handleSwapTeam = async (registration: EventRegistrationDto) => {
    const newTeam: TeamAssignment = registration.teamAssignment === 'Black' ? 'White' : 'Black';

    // Optimistic update
    setAllRegistrations((prev) =>
      prev.map((reg) =>
        reg.id === registration.id ? { ...reg, teamAssignment: newTeam } : reg
      )
    );

    const success = await updateTeamAssignment(eventId, registration.id, newTeam);
    if (!success) {
      Alert.alert('Error', 'Failed to swap team');
      await reloadRegistrations();
    }
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
            // Optimistic update - remove immediately
            setAllRegistrations((prev) => {
              const filtered = prev.filter((reg) => reg.id !== registration.id);
              // Renumber waitlist if we removed a waitlisted player
              if (isWaitlisted) {
                return filtered.map((reg) => {
                  if (reg.isWaitlisted && reg.waitlistPosition && registration.waitlistPosition) {
                    if (reg.waitlistPosition > registration.waitlistPosition) {
                      return { ...reg, waitlistPosition: reg.waitlistPosition - 1 };
                    }
                  }
                  return reg;
                });
              }
              return filtered;
            });

            const success = await removeRegistration(eventId, registration.id);
            if (!success) {
              Alert.alert('Error', 'Failed to remove registration');
              await reloadRegistrations();
            } else if (!isWaitlisted) {
              // Rostered player removed - waitlist promotion may have happened
              await reloadRegistrations();
            }
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
            // Determine team assignment (balance teams)
            const blackCount = allRegistrations.filter(
              (r) => !r.isWaitlisted && r.teamAssignment === 'Black'
            ).length;
            const whiteCount = allRegistrations.filter(
              (r) => !r.isWaitlisted && r.teamAssignment === 'White'
            ).length;
            const newTeam: TeamAssignment = blackCount <= whiteCount ? 'Black' : 'White';

            // Optimistic update - move to roster immediately
            setAllRegistrations((prev) => {
              // Get remaining waitlist to renumber
              const remainingWaitlist = prev
                .filter((r) => r.isWaitlisted && r.id !== registration.id)
                .sort((a, b) => (a.waitlistPosition ?? 999) - (b.waitlistPosition ?? 999));

              return prev.map((reg) => {
                if (reg.id === registration.id) {
                  return {
                    ...reg,
                    isWaitlisted: false,
                    waitlistPosition: undefined,
                    teamAssignment: newTeam,
                    status: 'Registered',
                  };
                }
                // Renumber remaining waitlist
                if (reg.isWaitlisted) {
                  const newPosition = remainingWaitlist.findIndex((r) => r.id === reg.id) + 1;
                  return { ...reg, waitlistPosition: newPosition };
                }
                return reg;
              });
            });

            // API call in background
            try {
              const result = await eventService.moveToRoster(eventId, registration.id);
              if (!result.success) {
                Alert.alert('Error', result.message || 'Failed to move player');
                await reloadRegistrations(); // Revert on failure
              }
            } catch (error: any) {
              const message = error?.response?.data?.message || 'Failed to move player to roster';
              Alert.alert('Error', message);
              await reloadRegistrations(); // Revert on error
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
            // Calculate next waitlist position
            const currentWaitlist = allRegistrations.filter((r) => r.isWaitlisted);
            const maxPosition = currentWaitlist.reduce(
              (max, r) => Math.max(max, r.waitlistPosition ?? 0),
              0
            );
            const nextPosition = maxPosition + 1;

            // Optimistic update - move to waitlist immediately
            setAllRegistrations((prev) =>
              prev.map((reg) => {
                if (reg.id === registration.id) {
                  return {
                    ...reg,
                    isWaitlisted: true,
                    waitlistPosition: nextPosition,
                    teamAssignment: undefined,
                    rosterOrder: undefined,
                    paymentDeadlineAt: undefined,
                    status: 'Waitlisted',
                  };
                }
                return reg;
              })
            );

            // API call in background
            try {
              const result = await eventService.moveToWaitlist(eventId, registration.id);
              if (!result.success) {
                Alert.alert('Error', result.message || 'Failed to move player');
                await reloadRegistrations(); // Revert on failure
              }
            } catch (error: any) {
              const message = error?.response?.data?.message || 'Failed to move player to waitlist';
              Alert.alert('Error', message);
              await reloadRegistrations(); // Revert on error
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

  // Slot position label change handler (optimistic + debounced API call)
  const slotLabelTimerRef = useRef<ReturnType<typeof setTimeout>>();
  const preChangeLabelsRef = useRef<Record<number, string> | undefined>();

  const handleSlotLabelChange = useCallback((slotIndex: number, newLabel: string | null) => {
    // Capture the original labels before the first tap in a rapid sequence (for rollback)
    if (!slotLabelTimerRef.current) {
      preChangeLabelsRef.current = event.slotPositionLabels;
    }

    const currentLabels = event.slotPositionLabels || {};
    const { [slotIndex]: _, ...labelsWithoutSlot } = currentLabels;
    const newLabels = newLabel === null ? labelsWithoutSlot : { ...currentLabels, [slotIndex]: newLabel };

    // Optimistic: update UI immediately
    useEventStore.setState({ selectedEvent: { ...event, slotPositionLabels: newLabels } });

    // Debounce: only send API call after tapping stops
    clearTimeout(slotLabelTimerRef.current);
    const rollbackLabels = preChangeLabelsRef.current;
    const rollbackEvent = { ...event, slotPositionLabels: rollbackLabels };
    slotLabelTimerRef.current = setTimeout(async () => {
      slotLabelTimerRef.current = undefined;
      try {
        await eventService.update(eventId, { slotPositionLabels: newLabels });
      } catch (error) {
        // Rollback to state before rapid tapping started
        useEventStore.setState({ selectedEvent: rollbackEvent });
        Alert.alert('Error', 'Failed to update position label. Please try again.');
      }
    }, 500);
  }, [eventId, event]);

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
        {/* Organizer action buttons */}
        {canManage && (
          <View style={styles.organizerActions}>
            {/* Add Player button */}
            <TouchableOpacity
              style={styles.addPlayerButton}
              onPress={() => setIsAddPlayerModalVisible(true)}
            >
              <Text style={styles.addPlayerButtonText} allowFontScaling={false}>
                + Add Player
              </Text>
            </TouchableOpacity>

            {/* Publish Roster button (only on unpublished events) */}
            {!event.isRosterPublished && (
              <TouchableOpacity
                style={[styles.publishButton, isPublishing && styles.publishButtonDisabled]}
                onPress={handlePublishRoster}
                disabled={isPublishing}
              >
                {isPublishing ? (
                  <ActivityIndicator size="small" color={colors.text.primary} />
                ) : (
                  <Text style={styles.publishButtonText} allowFontScaling={false}>
                    Publish Roster
                  </Text>
                )}
              </TouchableOpacity>
            )}
          </View>
        )}

        {/* Roster View */}
        <View>
          {registrations.length === 0 ? (
            <EmptyState
              icon="people-outline"
              title="No Registrations"
              message="No one has registered for this event yet"
            />
          ) : (
            <DraggableRoster
              registrations={registrations}
              onPlayerPress={handlePlayerPress}
              onRosterChange={canManage ? handleRosterChange : undefined}
              readOnly={!canManage}
              slotPositionLabels={event.slotPositionLabels}
              onSlotLabelChange={canManage ? handleSlotLabelChange : undefined}
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

      {/* Add Player Modal */}
      <AddPlayerModal
        visible={isAddPlayerModalVisible}
        eventId={eventId}
        onClose={() => setIsAddPlayerModalVisible(false)}
        onPlayerAdded={handlePlayerAdded}
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
  organizerActions: {
    flexDirection: 'row',
    gap: spacing.sm,
    marginBottom: spacing.md,
  },
  addPlayerButton: {
    flex: 1,
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.primary.teal,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    borderRadius: radius.lg,
    alignItems: 'center',
  },
  addPlayerButtonText: {
    color: colors.primary.teal,
    fontSize: 16,
    fontWeight: '600',
  },
  publishButton: {
    flex: 1,
    backgroundColor: colors.primary.teal,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    borderRadius: radius.lg,
    alignItems: 'center',
  },
  publishButtonDisabled: {
    opacity: 0.6,
  },
  publishButtonText: {
    color: colors.text.primary,
    fontSize: 16,
    fontWeight: '600',
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
