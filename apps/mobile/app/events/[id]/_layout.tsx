import { Stack } from 'expo-router';
import { colors } from '../../../theme';

export default function EventDetailLayout() {
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
