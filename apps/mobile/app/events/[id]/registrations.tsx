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
import { useLocalSearchParams } from 'expo-router';
import { eventService } from '@bhmhockey/api-client';
import type { EventRegistrationDto } from '@bhmhockey/shared';
import { useEventStore } from '../../../stores/eventStore';
import { getPaymentStatusInfo } from '../../../utils/venmo';

export default function EventRegistrationsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [registrations, setRegistrations] = useState<EventRegistrationDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const { updatePaymentStatus, selectedEvent } = useEventStore();

  useEffect(() => {
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
            if (success) {
              await loadRegistrations();
            }
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
            if (success) {
              await loadRegistrations();
            }
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
            if (success) {
              await loadRegistrations();
            }
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
        <View style={styles.userInfo}>
          <Text style={styles.userName}>
            {item.user.firstName} {item.user.lastName}
          </Text>
          <Text style={styles.userEmail}>{item.user.email}</Text>
        </View>

        {showPaymentActions && (
          <View style={styles.paymentSection}>
            <View style={[styles.statusBadge, { backgroundColor: statusInfo.backgroundColor }]}>
              <Text style={[styles.statusText, { color: statusInfo.color }]}>
                {statusInfo.label}
              </Text>
            </View>

            {item.paymentStatus === 'Pending' && (
              <TouchableOpacity
                style={styles.markPaidButton}
                onPress={() => handleMarkPaid(item.id)}
              >
                <Text style={styles.markPaidButtonText}>Mark Paid</Text>
              </TouchableOpacity>
            )}

            {item.paymentStatus === 'MarkedPaid' && (
              <TouchableOpacity
                style={styles.verifyButton}
                onPress={() => handleVerifyPayment(item.id)}
              >
                <Text style={styles.verifyButtonText}>Verify</Text>
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
        <ActivityIndicator size="large" color="#007AFF" />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <FlatList
        data={registrations}
        keyExtractor={(item) => item.id}
        renderItem={renderRegistration}
        contentContainerStyle={styles.list}
        ListEmptyComponent={
          <Text style={styles.emptyText}>No registrations yet</Text>
        }
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  list: {
    padding: 16,
  },
  registrationCard: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
  },
  userInfo: {
    marginBottom: 12,
  },
  userName: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
  },
  userEmail: {
    fontSize: 14,
    color: '#666',
    marginTop: 2,
  },
  paymentSection: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: '#f0f0f0',
  },
  statusBadge: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
  },
  statusText: {
    fontSize: 12,
    fontWeight: '600',
  },
  markPaidButton: {
    backgroundColor: '#007AFF',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 8,
  },
  markPaidButtonText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '600',
  },
  verifyButton: {
    backgroundColor: '#4CAF50',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 8,
  },
  verifyButtonText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '600',
  },
  resetButton: {
    backgroundColor: '#fff',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#FF3B30',
  },
  resetButtonText: {
    color: '#FF3B30',
    fontSize: 14,
    fontWeight: '600',
  },
  emptyText: {
    textAlign: 'center',
    color: '#666',
    fontSize: 16,
    marginTop: 40,
  },
});
