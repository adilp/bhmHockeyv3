import { Stack } from 'expo-router';
import { colors } from '../../theme';

export default function OrganizationsLayout() {
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
          title: 'Create Organization',
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
