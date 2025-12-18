import { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  Linking,
} from 'react-native';
import { useRouter } from 'expo-router';
import { userService } from '@bhmhockey/api-client';
import { useAuthStore } from '../../stores/authStore';
import type { User, SkillLevel } from '@bhmhockey/shared';
import {
  FormSection,
  FormInput,
  PositionSelector,
  buildPositionsFromState,
  createStateFromPositions,
} from '../../components';
import { colors, spacing, radius } from '../../theme';

export default function ProfileScreen() {
  const router = useRouter();
  const { user: authUser, setUser: setAuthUser, logout } = useAuthStore();

  const [user, setUser] = useState<User | null>(authUser);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  // Form state
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [venmoHandle, setVenmoHandle] = useState('');

  // Multi-position state
  const [isGoalie, setIsGoalie] = useState(false);
  const [goalieSkill, setGoalieSkill] = useState<SkillLevel>('Bronze');
  const [isSkater, setIsSkater] = useState(false);
  const [skaterSkill, setSkaterSkill] = useState<SkillLevel>('Bronze');

  useEffect(() => {
    if (authUser) {
      setUser(authUser);
      setFirstName(authUser.firstName);
      setLastName(authUser.lastName);
      setPhoneNumber(authUser.phoneNumber || '');
      setVenmoHandle(authUser.venmoHandle || '');

      // Load positions using helper
      const positionState = createStateFromPositions(authUser.positions);
      setIsGoalie(positionState.isGoalie ?? false);
      setGoalieSkill(positionState.goalieSkill ?? 'Bronze');
      setIsSkater(positionState.isSkater ?? false);
      setSkaterSkill(positionState.skaterSkill ?? 'Bronze');
    }
  }, [authUser]);

  const handleSave = async () => {
    if (!isGoalie && !isSkater) {
      Alert.alert('Error', 'Please select at least one position (Goalie or Skater)');
      return;
    }

    try {
      setSaving(true);

      const positions = buildPositionsFromState({ isGoalie, goalieSkill, isSkater, skaterSkill });

      const updates = {
        firstName,
        lastName,
        phoneNumber: phoneNumber || undefined,
        positions,
        venmoHandle: venmoHandle || undefined,
      };

      const updatedUser = await userService.updateProfile(updates);
      setUser(updatedUser);
      setAuthUser(updatedUser);

      Alert.alert('Success', 'Profile updated successfully!');
    } catch (error: any) {
      console.error('Failed to update profile:', error);
      const message = error?.response?.data?.message || 'Failed to update profile. Please try again.';
      Alert.alert('Error', message);
    } finally {
      setSaving(false);
    }
  };

  const handleLogout = async () => {
    Alert.alert(
      'Logout',
      'Are you sure you want to logout?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Logout',
          style: 'destructive',
          onPress: async () => {
            await logout();
            router.replace('/(auth)/login');
          },
        },
      ]
    );
  };

  const handleDeleteAccount = async () => {
    Alert.alert(
      'Delete Account',
      'Are you sure you want to delete your account? This action cannot be undone.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Delete Account',
          style: 'destructive',
          onPress: () => {
            // Second confirmation
            Alert.alert(
              'Final Confirmation',
              'This will permanently delete your account and all associated data. Type DELETE to confirm.',
              [
                { text: 'Cancel', style: 'cancel' },
                {
                  text: 'Yes, Delete My Account',
                  style: 'destructive',
                  onPress: async () => {
                    try {
                      setSaving(true);
                      await userService.deleteAccount();
                      await logout();
                      router.replace('/(auth)/login');
                      Alert.alert('Account Deleted', 'Your account has been successfully deleted.');
                    } catch (error: any) {
                      console.error('Failed to delete account:', error);
                      const message = error?.response?.data?.message || 'Failed to delete account. Please try again.';
                      Alert.alert('Error', message);
                    } finally {
                      setSaving(false);
                    }
                  },
                },
              ]
            );
          },
        },
      ]
    );
  };

  if (loading) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
        <Text style={styles.loadingText}>Loading profile...</Text>
      </View>
    );
  }

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>Profile</Text>
        <Text style={styles.subtitle}>{user?.email}</Text>
      </View>

      <View style={styles.form}>
        <FormSection title="Basic Information">
          <FormInput
            label="First Name"
            placeholder="Enter first name"
            value={firstName}
            onChangeText={setFirstName}
          />

          <FormInput
            label="Last Name"
            placeholder="Enter last name"
            value={lastName}
            onChangeText={setLastName}
          />

          <FormInput
            label="Phone Number"
            placeholder="(123) 456-7890"
            value={phoneNumber}
            onChangeText={setPhoneNumber}
            keyboardType="phone-pad"
          />
        </FormSection>

        <FormSection title="Positions" hint="Select at least one position">
          <PositionSelector
            isGoalie={isGoalie}
            goalieSkill={goalieSkill}
            isSkater={isSkater}
            skaterSkill={skaterSkill}
            onGoalieChange={setIsGoalie}
            onGoalieSkillChange={setGoalieSkill}
            onSkaterChange={setIsSkater}
            onSkaterSkillChange={setSkaterSkill}
            disabled={saving}
          />
        </FormSection>

        <FormSection title="Payment">
          <FormInput
            label="Venmo Handle"
            placeholder="@username"
            value={venmoHandle}
            onChangeText={setVenmoHandle}
            autoCapitalize="none"
            hint="For receiving payments from events"
          />
        </FormSection>

        <TouchableOpacity
          style={[styles.saveButton, saving && styles.saveButtonDisabled]}
          onPress={handleSave}
          disabled={saving}
        >
          {saving ? (
            <ActivityIndicator color="#fff" />
          ) : (
            <Text style={styles.saveButtonText}>Save Profile</Text>
          )}
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.logoutButton}
          onPress={handleLogout}
          disabled={saving}
        >
          <Text style={styles.logoutButtonText}>Logout</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.deleteAccountButton}
          onPress={handleDeleteAccount}
          disabled={saving}
        >
          <Text style={styles.deleteAccountButtonText}>Delete Account</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.linkButton}
          onPress={() => Linking.openURL('https://bhmhockey-mb3md.ondigitalocean.app/privacy')}
        >
          <Text style={styles.linkButtonText}>Privacy Policy</Text>
        </TouchableOpacity>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
  loadingText: {
    marginTop: spacing.md,
    fontSize: 16,
    color: colors.text.muted,
  },
  header: {
    padding: spacing.lg,
    backgroundColor: colors.bg.dark,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    marginBottom: spacing.xs,
    color: colors.text.primary,
  },
  subtitle: {
    fontSize: 16,
    color: colors.text.muted,
  },
  form: {
    padding: spacing.lg,
  },
  saveButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.lg,
    padding: spacing.md,
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  saveButtonDisabled: {
    opacity: 0.6,
  },
  saveButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },
  logoutButton: {
    backgroundColor: 'transparent',
    borderWidth: 1,
    borderColor: colors.status.error,
    borderRadius: radius.lg,
    padding: spacing.md,
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  logoutButtonText: {
    color: colors.status.error,
    fontSize: 16,
    fontWeight: '600',
  },
  deleteAccountButton: {
    backgroundColor: colors.status.error,
    borderRadius: radius.lg,
    padding: spacing.md,
    alignItems: 'center',
    marginTop: spacing.lg,
  },
  deleteAccountButtonText: {
    color: colors.text.primary,
    fontSize: 16,
    fontWeight: '600',
  },
  linkButton: {
    alignItems: 'center',
    marginTop: spacing.lg,
    marginBottom: spacing.xl,
  },
  linkButtonText: {
    color: colors.text.muted,
    fontSize: 14,
    textDecorationLine: 'underline',
  },
});
