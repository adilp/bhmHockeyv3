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
import { useLocalSearchParams, useRouter } from 'expo-router';
import { authService } from '@bhmhockey/api-client';
import { colors, spacing, radius } from '../../theme';
import {
  RESET_CODE_LENGTH,
  MIN_PASSWORD_LENGTH,
  getResetErrorMessage,
  isValidEmail,
  normalizeResetCode,
  validateNewPassword,
} from '../../utils/passwordReset';

/**
 * Reset a forgotten password by email.
 *
 * Reached two ways:
 * - From a reset email's link: bhmhockey://reset-password?token=... (via the
 *   bhmhockey.com/reset page). The token proves the email, so only a new
 *   password is asked for.
 * - From "Forgot Password?" on login: enter an email, receive a link and a
 *   6-digit code, then type the code here. This is also the fallback when a
 *   link won't open.
 */
type Step = 'email' | 'code' | 'link';

export default function ResetPasswordScreen() {
  const router = useRouter();
  const params = useLocalSearchParams<{ token?: string; email?: string }>();
  const linkToken = typeof params.token === 'string' && params.token.length > 0 ? params.token : null;

  const [step, setStep] = useState<Step>(linkToken ? 'link' : 'email');
  const [email, setEmail] = useState(typeof params.email === 'string' ? params.email : '');
  const [code, setCode] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const goToLogin = () => router.replace('/(auth)/login');

  const sendResetEmail = async () => {
    if (!isValidEmail(email)) {
      setError('Enter a valid email address');
      return;
    }

    setError(null);
    setLoading(true);
    try {
      const response = await authService.requestPasswordReset({ email: email.trim() });
      setNotice(response.message);
      setStep('code');
    } catch (e) {
      setError(getResetErrorMessage(e, 'Could not send the reset email. Please try again.'));
    } finally {
      setLoading(false);
    }
  };

  const submitNewPassword = async () => {
    const problem = validateNewPassword(password, confirmPassword);
    if (problem) {
      setError(problem);
      return;
    }
    if (step === 'code') {
      if (!isValidEmail(email)) {
        setError('Enter the email address the code was sent to');
        return;
      }
      if (code.length !== RESET_CODE_LENGTH) {
        setError(`Enter the ${RESET_CODE_LENGTH}-digit code from the email`);
        return;
      }
    }

    setError(null);
    setLoading(true);
    try {
      await authService.confirmPasswordReset(
        step === 'link' && linkToken
          ? { newPassword: password, token: linkToken }
          : { newPassword: password, email: email.trim(), code }
      );
      Alert.alert(
        'Password Reset',
        'Your password has been changed. Sign in with your new password.',
        [{ text: 'Sign In', onPress: goToLogin }],
        { cancelable: false }
      );
    } catch (e) {
      setError(getResetErrorMessage(e, 'Could not reset your password. Please try again.'));
    } finally {
      setLoading(false);
    }
  };

  // A link that's expired or already used leaves the player stuck on a screen
  // that can't work - offer the code path instead of a dead end
  const switchToCode = () => {
    setError(null);
    setNotice(null);
    setStep('email');
  };

  const title =
    step === 'link' ? 'Choose a New Password' : step === 'code' ? 'Enter Your Code' : 'Reset Password';
  const subtitle =
    step === 'link'
      ? 'Enter a new password for your account.'
      : step === 'code'
        ? `Enter the ${RESET_CODE_LENGTH}-digit code from the email we sent${email ? ` to ${email.trim()}` : ''}, then choose a new password.`
        : "Enter your account's email and we'll send you a link and a code to reset your password.";

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
    >
      <ScrollView contentContainerStyle={styles.scrollContent} keyboardShouldPersistTaps="handled">
        <View style={styles.header}>
          <Text style={styles.title} allowFontScaling={false}>{title}</Text>
          <Text style={styles.subtitle} allowFontScaling={false}>{subtitle}</Text>
        </View>

        {notice && step === 'code' && (
          <View style={styles.notice}>
            <Text style={styles.noticeText} allowFontScaling={false}>{notice}</Text>
          </View>
        )}

        <View style={styles.form}>
          {step === 'email' && (
            <View style={styles.field}>
              <Text style={styles.label} allowFontScaling={false}>Email</Text>
              <TextInput
                style={styles.input}
                placeholder="you@example.com"
                placeholderTextColor={colors.text.muted}
                value={email}
                onChangeText={setEmail}
                autoCapitalize="none"
                autoCorrect={false}
                keyboardType="email-address"
                autoComplete="email"
                textContentType="emailAddress"
                editable={!loading}
                allowFontScaling={false}
              />
            </View>
          )}

          {step === 'code' && (
            <>
              <View style={styles.field}>
                <Text style={styles.label} allowFontScaling={false}>Email</Text>
                <TextInput
                  style={styles.input}
                  placeholder="you@example.com"
                  placeholderTextColor={colors.text.muted}
                  value={email}
                  onChangeText={setEmail}
                  autoCapitalize="none"
                  autoCorrect={false}
                  keyboardType="email-address"
                  autoComplete="email"
                  textContentType="emailAddress"
                  editable={!loading}
                  allowFontScaling={false}
                />
              </View>
              <View style={styles.field}>
                <Text style={styles.label} allowFontScaling={false}>{RESET_CODE_LENGTH}-Digit Code</Text>
                <TextInput
                  style={[styles.input, styles.codeInput]}
                  placeholder="123456"
                  placeholderTextColor={colors.text.muted}
                  value={code}
                  onChangeText={(value) => setCode(normalizeResetCode(value))}
                  keyboardType="number-pad"
                  autoComplete="one-time-code"
                  textContentType="oneTimeCode"
                  maxLength={RESET_CODE_LENGTH + 2}
                  editable={!loading}
                  allowFontScaling={false}
                />
              </View>
            </>
          )}

          {(step === 'code' || step === 'link') && (
            <>
              <View style={styles.field}>
                <Text style={styles.label} allowFontScaling={false}>New Password</Text>
                <TextInput
                  style={styles.input}
                  placeholder={`At least ${MIN_PASSWORD_LENGTH} characters`}
                  placeholderTextColor={colors.text.muted}
                  value={password}
                  onChangeText={setPassword}
                  secureTextEntry
                  autoComplete="password-new"
                  textContentType="newPassword"
                  editable={!loading}
                  allowFontScaling={false}
                />
              </View>
              <View style={styles.field}>
                <Text style={styles.label} allowFontScaling={false}>Confirm New Password</Text>
                <TextInput
                  style={styles.input}
                  placeholder="Enter it again"
                  placeholderTextColor={colors.text.muted}
                  value={confirmPassword}
                  onChangeText={setConfirmPassword}
                  secureTextEntry
                  autoComplete="password-new"
                  textContentType="newPassword"
                  editable={!loading}
                  allowFontScaling={false}
                />
              </View>
            </>
          )}

          {error && (
            <Text style={styles.error} allowFontScaling={false}>{error}</Text>
          )}

          <TouchableOpacity
            style={[styles.button, loading && styles.buttonDisabled]}
            onPress={step === 'email' ? sendResetEmail : submitNewPassword}
            disabled={loading}
          >
            {loading ? (
              <ActivityIndicator color={colors.bg.darkest} />
            ) : (
              <Text style={styles.buttonText} allowFontScaling={false}>
                {step === 'email' ? 'Send Reset Email' : 'Reset Password'}
              </Text>
            )}
          </TouchableOpacity>

          {step === 'email' && (
            <TouchableOpacity
              style={styles.secondaryAction}
              onPress={() => {
                setError(null);
                setStep('code');
              }}
              disabled={loading}
            >
              <Text style={styles.link} allowFontScaling={false}>I already have a code</Text>
            </TouchableOpacity>
          )}

          {step === 'code' && (
            <TouchableOpacity style={styles.secondaryAction} onPress={sendResetEmail} disabled={loading}>
              <Text style={styles.link} allowFontScaling={false}>Send a new email</Text>
            </TouchableOpacity>
          )}

          {step === 'link' && (
            <TouchableOpacity style={styles.secondaryAction} onPress={switchToCode} disabled={loading}>
              <Text style={styles.link} allowFontScaling={false}>Link not working? Use a code instead</Text>
            </TouchableOpacity>
          )}

          <TouchableOpacity style={styles.secondaryAction} onPress={goToLogin} disabled={loading}>
            <Text style={styles.mutedLink} allowFontScaling={false}>Back to Sign In</Text>
          </TouchableOpacity>
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
    justifyContent: 'center',
    padding: spacing.lg,
  },
  header: {
    marginBottom: spacing.xl,
    alignItems: 'center',
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    marginBottom: spacing.sm,
    color: colors.text.primary,
    textAlign: 'center',
  },
  subtitle: {
    fontSize: 15,
    color: colors.text.muted,
    textAlign: 'center',
    lineHeight: 21,
  },
  notice: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    padding: spacing.md,
    marginBottom: spacing.lg,
  },
  noticeText: {
    fontSize: 14,
    color: colors.text.secondary,
    lineHeight: 20,
  },
  form: {
    width: '100%',
  },
  field: {
    marginBottom: spacing.lg,
  },
  label: {
    fontSize: 14,
    fontWeight: '600',
    marginBottom: spacing.sm,
    color: colors.text.secondary,
  },
  input: {
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 16,
    color: colors.text.primary,
  },
  codeInput: {
    fontSize: 24,
    letterSpacing: 8,
    textAlign: 'center',
  },
  error: {
    color: colors.status.error,
    fontSize: 14,
    marginBottom: spacing.md,
    textAlign: 'center',
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
  secondaryAction: {
    alignItems: 'center',
    marginTop: spacing.lg,
  },
  link: {
    fontSize: 14,
    color: colors.primary.teal,
    fontWeight: '600',
  },
  mutedLink: {
    fontSize: 14,
    color: colors.text.muted,
  },
});
