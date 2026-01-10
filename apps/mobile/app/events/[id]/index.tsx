import { useCallback, useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ActivityIndicator,
  Alert,
  ActionSheetIOS,
  Platform,
  TouchableOpacity,
} from 'react-native';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { useLocalSearchParams, useRouter, useFocusEffect, Stack } from 'expo-router';
import { useEventStore } from '../../../stores/eventStore';
import { useAuthStore } from '../../../stores/authStore';
import { openVenmoPayment } from '../../../utils/venmo';
import {
  SegmentedControl,
  EventInfoTab,
  EventRosterTab,
  EventChatTab,
  RegistrationFooter,
} from '../../../components';
import type { TabKey } from '../../../components';
import { colors, spacing } from '../../../theme';
import type { Position } from '@bhmhockey/shared';

export default function EventDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { isAuthenticated, user } = useAuthStore();
  const [selectedTab, setSelectedTab] = useState<TabKey>('info');
  const [isProcessing, setIsProcessing] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);

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

  // Smart default tab selection - only runs when event ID changes (new event loaded)
  useEffect(() => {
    if (!selectedEvent) return;

    // Determine default tab based on user's relationship to event
    let defaultTab: TabKey = 'info';

    if (selectedEvent.canManage) {
      // Organizers default to roster
      defaultTab = 'roster';
    } else if (selectedEvent.isRegistered && selectedEvent.cost > 0) {
      // Registered users with unpaid/pending status default to info (payment section)
      if (
        selectedEvent.myPaymentStatus === 'Pending' ||
        selectedEvent.myPaymentStatus === 'MarkedPaid'
      ) {
        defaultTab = 'info';
      } else {
        // Paid users default to roster
        defaultTab = 'roster';
      }
    }

    setSelectedTab(defaultTab);
  }, [selectedEvent?.id]); // Only re-run when viewing a different event

  const handleRefresh = async () => {
    if (!id) return;
    setIsRefreshing(true);
    try {
      await fetchEventById(id);
    } finally {
      setIsRefreshing(false);
    }
  };

  const handleRegister = async () => {
    if (!id || !isAuthenticated || !user) return;

    const positions = user.positions;
    const positionCount = positions
      ? Object.keys(positions).filter((k) => positions[k as keyof typeof positions]).length
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

    const showResultMessage = (
      result: { status: string; waitlistPosition?: number | null; message: string } | null,
      position: string
    ) => {
      if (!result) return;
      if (result.status === 'Waitlisted') {
        Alert.alert(
          'Added to Waitlist',
          `You're #${result.waitlistPosition} on the waitlist as a ${position}. We'll notify you when a spot opens up!`
        );
      } else {
        Alert.alert('Success', `You have been registered as a ${position}!`);
      }
    };

    // Single position - register directly
    if (positionCount === 1) {
      const position = positions?.goalie ? 'Goalie' : 'Skater';
      setIsProcessing(true);
      try {
        const result = await register(id, position as Position);
        showResultMessage(result, position);
      } finally {
        setIsProcessing(false);
      }
      return;
    }

    // Multiple positions - show picker
    // Note: isProcessing is set inside callbacks since ActionSheet/Alert are non-blocking
    if (Platform.OS === 'ios') {
      ActionSheetIOS.showActionSheetWithOptions(
        {
          options: ['Cancel', 'Goalie', 'Skater'],
          cancelButtonIndex: 0,
          title: 'Register as which position?',
        },
        async (buttonIndex) => {
          if (buttonIndex === 0) return; // Cancel pressed

          setIsProcessing(true);
          try {
            const position = buttonIndex === 1 ? 'Goalie' : 'Skater';
            const result = await register(id, position);
            showResultMessage(result, position);
          } finally {
            setIsProcessing(false);
          }
        }
      );
    } else {
      Alert.alert('Register as which position?', 'Select the position you want to play', [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Goalie',
          onPress: async () => {
            setIsProcessing(true);
            try {
              const result = await register(id, 'Goalie');
              showResultMessage(result, 'Goalie');
            } finally {
              setIsProcessing(false);
            }
          },
        },
        {
          text: 'Skater',
          onPress: async () => {
            setIsProcessing(true);
            try {
              const result = await register(id, 'Skater');
              showResultMessage(result, 'Skater');
            } finally {
              setIsProcessing(false);
            }
          },
        },
      ]);
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

    Alert.alert(title, message, [
      { text: 'No', style: 'cancel' },
      {
        text: 'Yes',
        style: 'destructive',
        onPress: async () => {
          setIsProcessing(true);
          try {
            const success = await cancelRegistration(id);
            if (success) Alert.alert('Done', successMessage);
          } finally {
            setIsProcessing(false);
          }
        },
      },
    ]);
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
  const canManage = selectedEvent?.canManage || false;

  const getOrgAbbreviation = (name: string) => {
    if (!name) return 'Event';
    const words = name.split(' ');
    if (words.length > 1) {
      return words
        .slice(0, 3)
        .map((w) => w[0])
        .join('')
        .toUpperCase();
    }
    return name.slice(0, 3).toUpperCase();
  };

  const renderTabContent = () => {
    if (!selectedEvent || !id) return null;

    switch (selectedTab) {
      case 'info':
        return (
          <EventInfoTab
            event={selectedEvent}
            canManage={canManage}
            onPayWithVenmo={handlePayWithVenmo}
            onMarkAsPaid={handleMarkAsPaid}
            onCancelRegistration={handleCancelRegistration}
            onRefresh={handleRefresh}
            isRefreshing={isRefreshing}
          />
        );
      case 'roster':
        return <EventRosterTab eventId={id} event={selectedEvent} canManage={canManage} />;
      case 'chat':
        return <EventChatTab />;
      default:
        return null;
    }
  };

  const headerTitle = selectedEvent
    ? getOrgAbbreviation(selectedEvent.organizationName || '')
    : 'Event';

  return (
    <GestureHandlerRootView style={styles.container}>
      <Stack.Screen
        options={{
          title: headerTitle,
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
          headerBackTitle: 'Back',
          headerRight: canManage
            ? () => (
                <TouchableOpacity
                  onPress={() => router.push(`/events/edit?id=${selectedEvent?.id}`)}
                  style={styles.headerButton}
                >
                  <Text style={styles.headerButtonText}>Edit</Text>
                </TouchableOpacity>
              )
            : undefined,
        }}
      />

      {isLoading || !selectedEvent ? (
        <View style={styles.centered}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={styles.loadingText}>Loading event...</Text>
        </View>
      ) : error ? (
        <View style={styles.centered}>
          <Text style={styles.errorText}>{error}</Text>
        </View>
      ) : (
        <View style={styles.content}>
          {/* Segmented Control */}
          <SegmentedControl selectedTab={selectedTab} onTabChange={setSelectedTab} />

          {/* Tab Content */}
          <View style={styles.tabContent}>{renderTabContent()}</View>

          {/* Registration Footer */}
          <RegistrationFooter
            isAuthenticated={isAuthenticated}
            isRegistered={selectedEvent.isRegistered}
            isWaitlisted={isWaitlisted || false}
            isFull={isFull}
            isProcessing={isProcessing}
            onRegister={handleRegister}
          />
        </View>
      )}
    </GestureHandlerRootView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  content: {
    flex: 1,
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
  tabContent: {
    flex: 1,
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
});
