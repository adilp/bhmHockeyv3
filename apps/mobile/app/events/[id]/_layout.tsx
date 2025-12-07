import { Stack } from 'expo-router';
import { colors } from '../../../theme';

export default function EventDetailLayout() {
  return (
    <Stack
      screenOptions={{
        headerStyle: {
          backgroundColor: colors.bg.dark,
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
        name="index"
        options={{
          title: 'Event',
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
