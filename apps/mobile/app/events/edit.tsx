import { useState, useEffect } from 'react';
import { View, Alert, ActivityIndicator, StyleSheet } from 'react-native';
import { useRouter, useLocalSearchParams, Stack } from 'expo-router';
import { eventService } from '@bhmhockey/api-client';
import type { EventDto } from '@bhmhockey/shared';
import { EventForm, EventFormData } from '../../components';
import { colors } from '../../theme';

export default function EditEventScreen() {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();

  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
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
});
