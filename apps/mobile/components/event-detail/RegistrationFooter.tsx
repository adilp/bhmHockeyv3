import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { colors, spacing, radius } from '../../theme';

interface RegistrationFooterProps {
  isAuthenticated: boolean;
  isRegistered: boolean;
  isWaitlisted: boolean;
  isFull: boolean;
  isProcessing: boolean;
  onRegister: () => void;
}

export function RegistrationFooter({
  isAuthenticated,
  isRegistered,
  isWaitlisted,
  isFull,
  isProcessing,
  onRegister,
}: RegistrationFooterProps) {
  const insets = useSafeAreaInsets();

  // Not authenticated - show login prompt
  if (!isAuthenticated) {
    return (
      <View style={[styles.container, { paddingBottom: Math.max(insets.bottom, spacing.md) }]}>
        <View style={styles.loginPrompt}>
          <Text style={styles.loginPromptText}>Log in to register for this event</Text>
        </View>
      </View>
    );
  }

  // Already registered or waitlisted - no footer needed (cancel is in info tab)
  if (isRegistered || isWaitlisted) {
    return null;
  }

  // Event full - show join waitlist
  if (isFull) {
    return (
      <View style={[styles.container, { paddingBottom: Math.max(insets.bottom, spacing.md) }]}>
        <TouchableOpacity
          style={styles.waitlistButton}
          onPress={onRegister}
          disabled={isProcessing}
          activeOpacity={0.7}
        >
          <Text style={styles.waitlistButtonText}>Join Waitlist</Text>
        </TouchableOpacity>
      </View>
    );
  }

  // Default - show register button
  return (
    <View style={[styles.container, { paddingBottom: Math.max(insets.bottom, spacing.md) }]}>
      <TouchableOpacity
        style={styles.registerButton}
        onPress={onRegister}
        disabled={isProcessing}
        activeOpacity={0.7}
      >
        <Text style={styles.registerButtonText}>Register for Event</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: colors.bg.dark,
    paddingHorizontal: spacing.md,
    paddingTop: spacing.md,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  registerButton: {
    backgroundColor: colors.primary.teal,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  registerButtonText: {
    color: colors.bg.darkest,
    fontSize: 18,
    fontWeight: '600',
  },
  waitlistButton: {
    backgroundColor: colors.status.warning,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  waitlistButtonText: {
    color: colors.bg.darkest,
    fontSize: 18,
    fontWeight: '600',
  },
  loginPrompt: {
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  loginPromptText: {
    fontSize: 16,
    color: colors.text.muted,
  },
});
