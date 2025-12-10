import { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  Alert,
  ActivityIndicator,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useAuthStore } from '../../stores/authStore';
import type { SkillLevel } from '@bhmhockey/shared';
import {
  FormSection,
  FormInput,
  PositionSelector,
  buildPositionsFromState,
} from '../../components';
import { colors, spacing, radius } from '../../theme';

export default function RegisterScreen() {
  const router = useRouter();
  const register = useAuthStore((state) => state.register);

  // Basic info
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  // Profile fields
  const [venmoHandle, setVenmoHandle] = useState('');
  const [isGoalie, setIsGoalie] = useState(false);
  const [goalieSkill, setGoalieSkill] = useState<SkillLevel>('Bronze');
  const [isSkater, setIsSkater] = useState(false);
  const [skaterSkill, setSkaterSkill] = useState<SkillLevel>('Bronze');

  const [loading, setLoading] = useState(false);

  const handleRegister = async () => {
    if (!firstName || !lastName || !email || !password || !confirmPassword) {
      Alert.alert('Error', 'Please fill in all required fields');
      return;
    }

    if (password !== confirmPassword) {
      Alert.alert('Error', 'Passwords do not match');
      return;
    }

    if (!isGoalie && !isSkater) {
      Alert.alert('Error', 'Please select at least one position (Goalie or Skater)');
      return;
    }

    const positions = buildPositionsFromState({ isGoalie, goalieSkill, isSkater, skaterSkill });

    try {
      setLoading(true);
      await register({
        firstName,
        lastName,
        email,
        password,
        positions,
        venmoHandle: venmoHandle || undefined,
      });
      router.replace('/(tabs)');
    } catch (error: any) {
      console.error('Registration error:', error);
      Alert.alert(
        'Registration Failed',
        error.message || 'Unable to create account. Please try again.'
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
    >
      <ScrollView
        contentContainerStyle={styles.scrollContent}
        keyboardShouldPersistTaps="handled"
      >
        <View style={styles.header}>
          <Text style={styles.title}>Create Account</Text>
          <Text style={styles.subtitle}>Sign up to get started</Text>
        </View>

        <View style={styles.form}>
          <FormSection title="Basic Information">
            <View style={styles.row}>
              <View style={styles.halfField}>
                <FormInput
                  label="First Name *"
                  placeholder="John"
                  value={firstName}
                  onChangeText={setFirstName}
                  autoComplete="given-name"
                  editable={!loading}
                />
              </View>
              <View style={styles.halfField}>
                <FormInput
                  label="Last Name *"
                  placeholder="Doe"
                  value={lastName}
                  onChangeText={setLastName}
                  autoComplete="family-name"
                  editable={!loading}
                />
              </View>
            </View>

            <FormInput
              label="Email *"
              placeholder="you@example.com"
              value={email}
              onChangeText={setEmail}
              autoCapitalize="none"
              keyboardType="email-address"
              autoComplete="email"
              editable={!loading}
            />

            <FormInput
              label="Password *"
              placeholder="Enter password"
              value={password}
              onChangeText={setPassword}
              secureTextEntry
              autoComplete="password-new"
              editable={!loading}
            />

            <FormInput
              label="Confirm Password *"
              placeholder="Re-enter password"
              value={confirmPassword}
              onChangeText={setConfirmPassword}
              secureTextEntry
              autoComplete="password-new"
              editable={!loading}
            />
          </FormSection>

          <FormSection title="Positions *" hint="Select at least one position">
            <PositionSelector
              isGoalie={isGoalie}
              goalieSkill={goalieSkill}
              isSkater={isSkater}
              skaterSkill={skaterSkill}
              onGoalieChange={setIsGoalie}
              onGoalieSkillChange={setGoalieSkill}
              onSkaterChange={setIsSkater}
              onSkaterSkillChange={setSkaterSkill}
              disabled={loading}
            />
          </FormSection>

          <FormSection title="Payment (Optional)">
            <FormInput
              label="Venmo Handle"
              placeholder="@username"
              value={venmoHandle}
              onChangeText={setVenmoHandle}
              autoCapitalize="none"
              editable={!loading}
              hint="For receiving payments when you organize events"
            />
          </FormSection>

          <TouchableOpacity
            style={[styles.button, loading && styles.buttonDisabled]}
            onPress={handleRegister}
            disabled={loading}
          >
            {loading ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <Text style={styles.buttonText}>Create Account</Text>
            )}
          </TouchableOpacity>

          <View style={styles.footer}>
            <Text style={styles.footerText}>Already have an account? </Text>
            <TouchableOpacity onPress={() => router.back()} disabled={loading}>
              <Text style={styles.link}>Sign In</Text>
            </TouchableOpacity>
          </View>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  scrollContent: {
    flexGrow: 1,
    padding: spacing.lg,
    paddingTop: spacing.xxl,
  },
  header: {
    marginBottom: spacing.lg,
    alignItems: 'center',
  },
  title: {
    fontSize: 32,
    fontWeight: 'bold',
    marginBottom: spacing.sm,
    color: colors.text.primary,
  },
  subtitle: {
    fontSize: 16,
    color: colors.text.muted,
  },
  form: {
    width: '100%',
  },
  row: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  halfField: {
    width: '48%',
  },
  button: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  buttonDisabled: {
    opacity: 0.6,
  },
  buttonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },
  footer: {
    flexDirection: 'row',
    justifyContent: 'center',
    marginTop: spacing.lg,
    marginBottom: spacing.xxl,
  },
  footerText: {
    fontSize: 14,
    color: colors.text.muted,
  },
  link: {
    fontSize: 14,
    color: colors.primary.teal,
    fontWeight: '600',
  },
});
