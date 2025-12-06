import { useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { useLocalSearchParams, useNavigation } from 'expo-router';
import { eventService } from '@bhmhockey/api-client';
import type { EventRegistrationDto } from '@bhmhockey/shared';
import { useEventStore } from '../../../stores/eventStore';
import { getPaymentStatusInfo } from '../../../utils/venmo';
import { EmptyState } from '../../../components';
import { colors, spacing, radius } from '../../../theme';

export default function EventRegistrationsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const navigation = useNavigation();
  const [registrations, setRegistrations] = useState<EventRegistrationDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const { updatePaymentStatus, selectedEvent } = useEventStore();

  useEffect(() => {
    navigation.setOptions({
      title: 'Registrations',
      headerStyle: { backgroundColor: colors.bg.dark },
      headerTintColor: colors.text.primary,
    });
    loadRegistrations();
  }, [id]);

  const loadRegistrations = async () => {
    if (!id) return;
    setIsLoading(true);
    try {
      const data = await eventService.getRegistrations(id);
      setRegistrations(data);
    } catch (error) {
      Alert.alert('Error', 'Failed to load registrations');
    } finally {
      setIsLoading(false);
    }
  };

  const handleMarkPaid = async (registrationId: string) => {
    if (!id) return;

    Alert.alert(
      'Mark as Paid',
      'Did you receive payment (cash, Venmo, or other)?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Yes, Mark Paid',
          onPress: async () => {
            const success = await updatePaymentStatus(id, registrationId, 'Verified');
            if (success) await loadRegistrations();
          },
        },
      ]
    );
  };

  const handleVerifyPayment = async (registrationId: string) => {
    if (!id) return;

    Alert.alert(
      'Verify Payment',
      'Have you received this payment?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Yes, Verify',
          onPress: async () => {
            const success = await updatePaymentStatus(id, registrationId, 'Verified');
            if (success) await loadRegistrations();
          },
        },
      ]
    );
  };

  const handleResetPayment = async (registrationId: string) => {
    if (!id) return;

    Alert.alert(
      'Reset Payment',
      'Are you sure you want to reset this payment status to Pending?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Reset',
          style: 'destructive',
          onPress: async () => {
            const success = await updatePaymentStatus(id, registrationId, 'Pending');
            if (success) await loadRegistrations();
          },
        },
      ]
    );
  };

  const renderRegistration = ({ item }: { item: EventRegistrationDto }) => {
    const statusInfo = getPaymentStatusInfo(item.paymentStatus);
    const showPaymentActions = selectedEvent?.cost && selectedEvent.cost > 0;

    return (
      <View style={styles.registrationCard}>
        {/* Avatar */}
        <View style={styles.avatar}>
          <Text style={styles.avatarText}>
            {item.user.firstName.charAt(0)}{item.user.lastName.charAt(0)}
          </Text>
        </View>

        {/* User Info */}
        <View style={styles.userInfo}>
          <Text style={styles.userName}>
            {item.user.firstName} {item.user.lastName}
          </Text>
          <Text style={styles.userMeta}>
            {item.registeredPosition || 'Player'} • {item.user.email}
          </Text>
        </View>

        {/* Payment Status & Actions */}
        {showPaymentActions && (
          <View style={styles.paymentSection}>
            <View style={[styles.statusBadge, { backgroundColor: statusInfo.backgroundColor }]}>
              <Text style={[styles.statusText, { color: statusInfo.color }]}>
                {statusInfo.label}
              </Text>
            </View>

            {item.paymentStatus === 'Pending' && (
              <TouchableOpacity
                style={styles.actionButton}
                onPress={() => handleMarkPaid(item.id)}
              >
                <Text style={styles.actionButtonText}>Mark Paid</Text>
              </TouchableOpacity>
            )}

            {item.paymentStatus === 'MarkedPaid' && (
              <TouchableOpacity
                style={[styles.actionButton, styles.verifyButton]}
                onPress={() => handleVerifyPayment(item.id)}
              >
                <Text style={styles.actionButtonText}>Verify</Text>
              </TouchableOpacity>
            )}

            {item.paymentStatus === 'Verified' && (
              <TouchableOpacity
                style={styles.resetButton}
                onPress={() => handleResetPayment(item.id)}
              >
                <Text style={styles.resetButtonText}>Reset</Text>
              </TouchableOpacity>
            )}
          </View>
        )}
      </View>
    );
  };

  if (isLoading) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* Summary Header */}
      {selectedEvent && (
        <View style={styles.summaryHeader}>
          <View style={styles.statBox}>
            <Text style={styles.statValue}>{registrations.length}</Text>
            <Text style={styles.statLabel}>Registered</Text>
          </View>
          {selectedEvent.cost > 0 && (
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
        </View>
      )}

      <FlatList
        data={registrations}
        keyExtractor={(item) => item.id}
        renderItem={renderRegistration}
        contentContainerStyle={styles.list}
        ListEmptyComponent={
          <EmptyState
            icon="👥"
            title="No Registrations"
            message="No one has registered for this event yet"
          />
        }
      />
    </View>
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
  list: {
    padding: spacing.md,
  },
  registrationCard: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginBottom: spacing.sm,
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  avatar: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: colors.bg.active,
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: spacing.md,
  },
  avatarText: {
    fontSize: 14,
    fontWeight: '700',
    color: colors.text.muted,
  },
  userInfo: {
    flex: 1,
    minWidth: 150,
  },
  userName: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  userMeta: {
    fontSize: 13,
    color: colors.text.muted,
    marginTop: 2,
  },
  paymentSection: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    marginTop: spacing.sm,
    width: '100%',
    paddingTop: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  statusBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: radius.sm,
  },
  statusText: {
    fontSize: 12,
    fontWeight: '600',
  },
  actionButton: {
    backgroundColor: colors.primary.teal,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    marginLeft: 'auto',
  },
  verifyButton: {
    backgroundColor: colors.primary.green,
  },
  actionButtonText: {
    color: colors.bg.darkest,
    fontSize: 14,
    fontWeight: '600',
  },
  resetButton: {
    backgroundColor: 'transparent',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.status.error,
    marginLeft: 'auto',
  },
  resetButtonText: {
    color: colors.status.error,
    fontSize: 14,
    fontWeight: '600',
  },
});
