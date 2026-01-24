import { Stack } from 'expo-router';
import { colors } from '../../../../theme';

export default function RegisterLayout() {
  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: colors.bg.dark },
        headerTintColor: colors.text.primary,
        headerShadowVisible: false,
      }}
    />
  );
}
