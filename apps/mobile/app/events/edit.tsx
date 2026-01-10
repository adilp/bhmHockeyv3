import { useState, useEffect } from 'react';
import { View, Text, Alert, ActivityIndicator, StyleSheet, ScrollView, TouchableOpacity } from 'react-native';
import { useRouter, useLocalSearchParams, Stack } from 'expo-router';
import { eventService } from '@bhmhockey/api-client';
import type { EventDto } from '@bhmhockey/shared';
import { EventForm, EventFormData } from '../../components';
import { useEventStore } from '../../stores/eventStore';
import { colors, spacing, radius } from '../../theme';

export default function EditEventScreen() {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();
  const { cancelEvent } = useEventStore();

  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [event, setEvent] = useState<EventDto | null>(null);

  useEffect(() => {
    loadEvent();
  }, [id]);

  const loadEvent = async () => {
    if (!id) return;

    setIsLoading(true);
    try {
      const eventData = await eventService.getById(id);
      setEvent(eventData);
    } catch (error) {
      Alert.alert('Error', 'Failed to load event');
      router.back();
    } finally {
      setIsLoading(false);
    }
  };

  const handleSubmit = async (data: EventFormData): Promise<boolean> => {
    if (!id) return false;

    setIsSaving(true);
    try {
      await eventService.update(id, {
        name: data.name,
        description: data.description,
        eventDate: data.eventDate,
        duration: data.duration,
        venue: data.venue,
        maxPlayers: data.maxPlayers,
        cost: data.cost,
        visibility: data.visibility,
        skillLevels: data.skillLevels,
      });

      Alert.alert('Success', 'Event updated successfully!', [
        { text: 'OK', onPress: () => router.back() }
      ]);
      return true;
    } catch (error) {
      Alert.alert('Error', 'Failed to update event. Please try again.');
      return false;
    } finally {
      setIsSaving(false);
    }
  };

  const handleDelete = () => {
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
            setIsDeleting(true);
            try {
              const success = await cancelEvent(id);
              if (success) {
                // Navigate first, then show confirmation
                router.replace('/(tabs)');
                // Small delay to let navigation start, then show alert
                setTimeout(() => {
                  Alert.alert('Event Deleted', 'The event has been cancelled.');
                }, 100);
              } else {
                Alert.alert('Error', 'Failed to delete event. Please try again.');
              }
            } catch (error) {
              console.error('Delete event error:', error);
              Alert.alert('Error', 'Failed to delete event. Please try again.');
            } finally {
              setIsDeleting(false);
            }
          },
        },
      ]
    );
  };

  if (isLoading) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Edit Event',
            headerBackTitle: 'Cancel',
            headerStyle: { backgroundColor: colors.bg.darkest },
            headerTintColor: colors.primary.teal,
            headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      </>
    );
  }

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Edit Event',
          headerBackTitle: 'Cancel',
          headerStyle: { backgroundColor: colors.bg.darkest },
          headerTintColor: colors.primary.teal,
          headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
        }}
      />

      <EventForm
        mode="edit"
        initialData={event || undefined}
        onSubmit={handleSubmit}
        isSubmitting={isSaving}
        footer={
          <View style={styles.dangerZone}>
            <Text style={styles.dangerZoneTitle}>Danger Zone</Text>
            <TouchableOpacity
              style={styles.deleteButton}
              onPress={handleDelete}
              disabled={isDeleting}
            >
              <Text style={styles.deleteButtonText}>
                {isDeleting ? 'Deleting...' : 'Delete Event'}
              </Text>
            </TouchableOpacity>
            <Text style={styles.dangerZoneWarning}>
              Permanently deletes this event and cancels all registrations
            </Text>
          </View>
        }
      />
    </>
  );
}

const styles = StyleSheet.create({
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
  dangerZone: {
    backgroundColor: 'rgba(248, 81, 73, 0.08)',
    padding: spacing.md,
    marginTop: spacing.xl,
    borderTopWidth: 1,
    borderTopColor: 'rgba(248, 81, 73, 0.3)',
  },
  dangerZoneTitle: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.status.error,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.md,
  },
  deleteButton: {
    backgroundColor: 'transparent',
    paddingVertical: 14,
    borderRadius: radius.md,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.status.error,
  },
  deleteButtonText: {
    color: colors.status.error,
    fontSize: 16,
    fontWeight: '600',
  },
  dangerZoneWarning: {
    fontSize: 12,
    color: colors.text.muted,
    textAlign: 'center',
    marginTop: spacing.sm,
  },
});
