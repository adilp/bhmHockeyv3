import { Stack } from 'expo-router';
import { colors } from '../../theme';

export default function EventsLayout() {
  return (
    <Stack
      screenOptions={{
        headerStyle: {
          backgroundColor: colors.bg.darkest,
        },
        headerTintColor: colors.primary.teal,
        headerTitleStyle: {
          color: colors.text.primary,
          fontWeight: '600',
        },
        contentStyle: {
          backgroundColor: colors.bg.darkest,
        },
      }}
    >
      <Stack.Screen
        name="create"
        options={{
          title: 'Create Event',
          headerBackTitle: 'Cancel',
          presentation: 'modal',
        }}
      />
      <Stack.Screen
        name="edit"
        options={{
          title: 'Edit Event',
          headerBackTitle: 'Cancel',
        }}
      />
      <Stack.Screen
        name="[id]"
        options={{
          headerShown: false,
        }}
      />
    </Stack>
  );
}
