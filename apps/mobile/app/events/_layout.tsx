import { Stack } from 'expo-router';

export default function EventsLayout() {
  return (
    <Stack>
      <Stack.Screen
        name="create"
        options={{
          title: 'Create Event',
          headerBackTitle: 'Cancel',
          presentation: 'modal',
        }}
      />
      <Stack.Screen
        name="[id]"
        options={{
          headerBackTitle: 'Back',
        }}
      />
    </Stack>
  );
}
