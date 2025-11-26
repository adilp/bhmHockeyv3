import { Stack } from 'expo-router';

export default function OrganizationsLayout() {
  return (
    <Stack>
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
