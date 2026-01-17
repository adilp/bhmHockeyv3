import { Stack } from 'expo-router';
import { colors } from '../../theme';

export default function SettingsLayout() {
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
        name="change-password"
        options={{
          title: 'Change Password',
          headerShown: true,
        }}
      />
    </Stack>
  );
}
