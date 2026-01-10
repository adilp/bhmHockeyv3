import { useEffect, useState, useMemo, useRef, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { eventService } from '@bhmhockey/api-client';
import type {
  EventRegistrationDto,
  EventDto,
  TeamAssignment,
  RosterOrderItem,
} from '@bhmhockey/shared';
import { useEventStore } from '../../stores/eventStore';
import { EmptyState } from '../EmptyState';
import { SectionHeader } from '../SectionHeader';
import { DraggableRoster } from '../DraggableRoster';
import { PlayerDetailModal } from '../PlayerDetailModal';
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

  // Load registrations with cleanup to prevent memory leaks
  useEffect(() => {
    let cancelled = false;

    const loadRegistrations = async () => {
      if (!eventId) return;
      setIsLoading(true);
      try {
        const registrationsData = await eventService.getRegistrations(eventId);
        if (!cancelled) {
          setAllRegistrations(registrationsData);
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
  }, [eventId]);

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
          const success = await updatePaymentStatus(eventId, registration.id, 'Verified');
          if (success) await reloadRegistrations();
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
          const success = await updatePaymentStatus(eventId, registration.id, 'Verified');
          if (success) await reloadRegistrations();
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
            if (success) await reloadRegistrations();
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

  // Waitlist item render
  const renderWaitlistItem = (item: EventRegistrationDto) => (
    <View key={item.id} style={styles.waitlistRow}>
      <View style={styles.waitlistPositionBadge}>
        <Text style={styles.waitlistPositionText} allowFontScaling={false}>
          #{item.waitlistPosition}
        </Text>
      </View>
      <View style={styles.waitlistUserInfo}>
        <Text style={styles.waitlistUserName} allowFontScaling={false}>
          {item.user.firstName} {item.user.lastName}
        </Text>
        <Text style={styles.waitlistUserMeta} allowFontScaling={false}>
          {item.registeredPosition || 'Skater'}
        </Text>
      </View>
      {canManage && (
        <TouchableOpacity style={styles.removeButtonSmall} onPress={() => handleRemove(item)}>
          <Text style={styles.removeButtonText} allowFontScaling={false}>
            Remove
          </Text>
        </TouchableOpacity>
      )}
    </View>
  );

  if (isLoading) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
      </View>
    );
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
              {waitlist.map((item) => renderWaitlistItem(item))}
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
        onSwapTeam={canManage ? handleSwapTeam : undefined}
        onRemove={canManage ? handleRemove : undefined}
        onMarkPaid={canManage ? handleMarkPaid : undefined}
        onVerifyPayment={canManage ? handleVerifyPayment : undefined}
        onResetPayment={canManage ? handleResetPayment : undefined}
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
