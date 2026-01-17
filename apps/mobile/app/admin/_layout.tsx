import { Stack } from 'expo-router';
import { colors } from '../../theme';

export default function AdminLayout() {
  return (
    <Stack
      screenOptions={{
        headerStyle: {
          backgroundColor: colors.bg.dark,
        },
        headerTintColor: colors.text.primary,
        headerTitleStyle: {
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
          title: 'Admin',
          headerShown: true,
        }}
      />
    </Stack>
  );
}
