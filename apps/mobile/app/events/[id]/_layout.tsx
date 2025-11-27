import { Stack } from 'expo-router';

export default function EventDetailLayout() {
  return (
    <Stack>
      <Stack.Screen
        name="index"
        options={{
          headerShown: false,
        }}
      />
      <Stack.Screen
        name="registrations"
        options={{
          title: 'Registrations',
          headerBackTitle: 'Back',
        }}
      />
    </Stack>
  );
}
