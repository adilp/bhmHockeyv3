import { useEffect } from 'react';
import { Alert } from 'react-native';
import { useRouter, Stack } from 'expo-router';
import { useEventStore } from '../../stores/eventStore';
import { useOrganizationStore } from '../../stores/organizationStore';
import { EventForm, EventFormData } from '../../components';
import { colors } from '../../theme';

export default function CreateEventScreen() {
  const router = useRouter();
  const { createEvent, isCreating } = useEventStore();
  const { myOrganizations, fetchMyOrganizations } = useOrganizationStore();

  // Fetch user's organizations on mount
  useEffect(() => {
    fetchMyOrganizations();
  }, []);

  const handleSubmit = async (data: EventFormData): Promise<boolean> => {
    const result = await createEvent({
      organizationId: data.organizationId,
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

    if (result) {
      Alert.alert('Success', 'Event created successfully!', [
        { text: 'OK', onPress: () => router.back() }
      ]);
      return true;
    }
    return false;
  };

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Create Event',
          headerBackTitle: 'Cancel',
          headerStyle: { backgroundColor: colors.bg.darkest },
          headerTintColor: colors.primary.teal,
          headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
        }}
      />

      <EventForm
        mode="create"
        organizations={myOrganizations}
        onSubmit={handleSubmit}
        isSubmitting={isCreating}
      />
    </>
  );
}
