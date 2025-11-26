import { useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { useLocalSearchParams, useNavigation } from 'expo-router';
import { useEventStore } from '../../stores/eventStore';
import { useAuthStore } from '../../stores/authStore';

export default function EventDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const navigation = useNavigation();
  const { isAuthenticated } = useAuthStore();
  const {
    selectedEvent,
    isLoading,
    error,
    fetchEventById,
    register,
    cancelRegistration,
    clearSelectedEvent,
    clearError,
  } = useEventStore();

  useEffect(() => {
    if (id) {
      fetchEventById(id);
    }
    return () => {
      clearSelectedEvent();
      clearError();
    };
  }, [id]);

  useEffect(() => {
    if (selectedEvent) {
      navigation.setOptions({
        title: selectedEvent.name,
      });
    }
  }, [selectedEvent, navigation]);

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
    if (!id || !isAuthenticated) return;

    const success = await register(id);
    if (success) {
      Alert.alert('Success', 'You have been registered for this event!');
    }
  };

  const handleCancelRegistration = async () => {
    if (!id) return;

    Alert.alert(
      'Cancel Registration',
      'Are you sure you want to cancel your registration?',
      [
        { text: 'No', style: 'cancel' },
        {
          text: 'Yes, Cancel',
          style: 'destructive',
          onPress: async () => {
            const success = await cancelRegistration(id);
            if (success) {
              Alert.alert('Cancelled', 'Your registration has been cancelled.');
            }
          },
        },
      ]
    );
  };

  if (isLoading || !selectedEvent) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color="#007AFF" />
        <Text style={styles.loadingText}>Loading event...</Text>
      </View>
    );
  }

  if (error) {
    return (
      <View style={styles.centered}>
        <Text style={styles.errorText}>{error}</Text>
      </View>
    );
  }

  const spotsLeft = selectedEvent.maxPlayers - selectedEvent.registeredCount;
  const isFull = spotsLeft <= 0;
  const canRegister = isAuthenticated && !selectedEvent.isRegistered && !isFull;

  return (
    <ScrollView style={styles.container}>
      {/* Header Section */}
      <View style={styles.header}>
        <Text style={styles.title}>{selectedEvent.name}</Text>
        <Text style={styles.organization}>
          {selectedEvent.organizationName || 'Pickup Game'}
        </Text>
        <View style={styles.badgeRow}>
          {selectedEvent.isRegistered && (
            <View style={styles.registeredBadge}>
              <Text style={styles.registeredBadgeText}>You're Registered</Text>
            </View>
          )}
          {selectedEvent.visibility === 'InviteOnly' && (
            <View style={styles.inviteOnlyBadge}>
              <Text style={styles.inviteOnlyBadgeText}>Invite Only</Text>
            </View>
          )}
          {selectedEvent.visibility === 'OrganizationMembers' && (
            <View style={styles.membersBadge}>
              <Text style={styles.membersBadgeText}>Members Only</Text>
            </View>
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
        <View style={styles.infoRow}>
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
                  {
                    width: `${(selectedEvent.registeredCount / selectedEvent.maxPlayers) * 100}%`,
                  },
                  isFull && styles.progressFillFull,
                ]}
              />
            </View>
            <Text style={styles.progressText}>
              {selectedEvent.registeredCount} / {selectedEvent.maxPlayers} registered
            </Text>
          </View>
        </View>
      </View>

      {/* Cost Section */}
      {selectedEvent.cost > 0 && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Cost</Text>
          <Text style={styles.costText}>${selectedEvent.cost.toFixed(2)}</Text>
          <Text style={styles.costNote}>Payment handled at event</Text>
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
            <TouchableOpacity
              style={styles.cancelButton}
              onPress={handleCancelRegistration}
            >
              <Text style={styles.cancelButtonText}>Cancel Registration</Text>
            </TouchableOpacity>
          ) : (
            <TouchableOpacity
              style={[styles.registerButton, !canRegister && styles.disabledButton]}
              onPress={handleRegister}
              disabled={!canRegister}
            >
              <Text style={[styles.registerButtonText, !canRegister && styles.disabledButtonText]}>
                {isFull ? 'Event is Full' : 'Register for Event'}
              </Text>
            </TouchableOpacity>
          )}
        </View>
      )}

      {!isAuthenticated && (
        <View style={styles.loginPrompt}>
          <Text style={styles.loginPromptText}>
            Log in to register for this event
          </Text>
        </View>
      )}

      {/* Bottom padding */}
      <View style={{ height: 40 }} />
    </ScrollView>
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
    padding: 20,
  },
  loadingText: {
    marginTop: 12,
    fontSize: 16,
    color: '#666',
  },
  errorText: {
    fontSize: 16,
    color: '#FF3B30',
    textAlign: 'center',
  },
  header: {
    backgroundColor: '#fff',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: '#333',
    marginBottom: 4,
  },
  organization: {
    fontSize: 16,
    color: '#666',
  },
  badgeRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
    marginTop: 12,
  },
  registeredBadge: {
    backgroundColor: '#4CAF50',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
  },
  registeredBadgeText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '600',
  },
  inviteOnlyBadge: {
    backgroundColor: '#FFF3CD',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
  },
  inviteOnlyBadgeText: {
    color: '#856404',
    fontSize: 14,
    fontWeight: '600',
  },
  membersBadge: {
    backgroundColor: '#CCE5FF',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
  },
  membersBadgeText: {
    color: '#004085',
    fontSize: 14,
    fontWeight: '600',
  },
  section: {
    backgroundColor: '#fff',
    padding: 16,
    marginTop: 12,
  },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '600',
    color: '#999',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: 12,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  infoLabel: {
    fontSize: 16,
    color: '#666',
  },
  infoValue: {
    fontSize: 16,
    color: '#333',
    fontWeight: '500',
  },
  venueText: {
    fontSize: 16,
    color: '#333',
    lineHeight: 24,
  },
  descriptionText: {
    fontSize: 16,
    color: '#333',
    lineHeight: 24,
  },
  availabilityContainer: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  availabilityInfo: {
    alignItems: 'center',
    marginRight: 20,
  },
  spotsNumber: {
    fontSize: 36,
    fontWeight: '700',
    color: '#4CAF50',
  },
  spotsNumberFull: {
    color: '#FF3B30',
  },
  spotsLabel: {
    fontSize: 14,
    color: '#666',
  },
  progressContainer: {
    flex: 1,
  },
  progressBar: {
    height: 8,
    backgroundColor: '#e0e0e0',
    borderRadius: 4,
    overflow: 'hidden',
  },
  progressFill: {
    height: '100%',
    backgroundColor: '#4CAF50',
    borderRadius: 4,
  },
  progressFillFull: {
    backgroundColor: '#FF3B30',
  },
  progressText: {
    fontSize: 14,
    color: '#666',
    marginTop: 8,
  },
  costText: {
    fontSize: 28,
    fontWeight: '700',
    color: '#333',
  },
  costNote: {
    fontSize: 14,
    color: '#999',
    marginTop: 4,
  },
  deadlineText: {
    fontSize: 16,
    color: '#333',
  },
  buttonContainer: {
    padding: 20,
  },
  registerButton: {
    backgroundColor: '#007AFF',
    paddingVertical: 16,
    borderRadius: 12,
    alignItems: 'center',
  },
  registerButtonText: {
    color: '#fff',
    fontSize: 18,
    fontWeight: '600',
  },
  cancelButton: {
    backgroundColor: '#fff',
    paddingVertical: 16,
    borderRadius: 12,
    alignItems: 'center',
    borderWidth: 2,
    borderColor: '#FF3B30',
  },
  cancelButtonText: {
    color: '#FF3B30',
    fontSize: 18,
    fontWeight: '600',
  },
  disabledButton: {
    backgroundColor: '#E0E0E0',
  },
  disabledButtonText: {
    color: '#999',
  },
  loginPrompt: {
    padding: 20,
    alignItems: 'center',
  },
  loginPromptText: {
    fontSize: 16,
    color: '#666',
  },
});
