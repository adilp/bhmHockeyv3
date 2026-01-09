import { useState, useEffect, useRef, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  Linking,
  Platform,
} from 'react-native';
import { useRouter } from 'expo-router';
import Constants from 'expo-constants';
import { userService } from '@bhmhockey/api-client';
import { useAuthStore } from '../../stores/authStore';
import type { User, SkillLevel, UserBadgeDto } from '@bhmhockey/shared';
import {
  FormSection,
  FormInput,
  PositionSelector,
  buildPositionsFromState,
  createStateFromPositions,
  TrophyCase,
} from '../../components';
import { colors, spacing, radius } from '../../theme';

const BADGE_SAVE_DEBOUNCE_MS = 500;

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

  // Badge state
  const [badges, setBadges] = useState<UserBadgeDto[]>([]);
  const [isLoadingBadges, setIsLoadingBadges] = useState(false);
  const [badgeSaveError, setBadgeSaveError] = useState<string | null>(null);
  const badgeSaveTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const previousBadgeOrderRef = useRef<string[]>([]);

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

      // Fetch user's full badges
      fetchBadges(authUser.id);
    }
  }, [authUser]);

  // Cleanup debounce timeout on unmount
  useEffect(() => {
    return () => {
      if (badgeSaveTimeoutRef.current) {
        clearTimeout(badgeSaveTimeoutRef.current);
      }
    };
  }, []);

  const fetchBadges = async (userId: string) => {
    try {
      setIsLoadingBadges(true);
      const userBadges = await userService.getUserBadges(userId);
      setBadges(userBadges);
      previousBadgeOrderRef.current = userBadges.map(b => b.id);
    } catch (error) {
      console.error('Failed to fetch badges:', error);
      // Fall back to badges from auth user if available
      if (authUser?.badges) {
        setBadges(authUser.badges);
        previousBadgeOrderRef.current = authUser.badges.map(b => b.id);
      }
    } finally {
      setIsLoadingBadges(false);
    }
  };

  const handleBadgeOrderChange = useCallback((badgeIds: string[]) => {
    // Clear any existing save timeout
    if (badgeSaveTimeoutRef.current) {
      clearTimeout(badgeSaveTimeoutRef.current);
    }

    // Clear any previous error
    setBadgeSaveError(null);

    // Optimistically update local state
    const reorderedBadges = badgeIds
      .map(id => badges.find(b => b.id === id))
      .filter((b): b is UserBadgeDto => b !== undefined);
    setBadges(reorderedBadges);

    // Debounce the API call
    badgeSaveTimeoutRef.current = setTimeout(async () => {
      try {
        await userService.updateBadgeOrder(badgeIds);
        // Update the previous order reference on success
        previousBadgeOrderRef.current = badgeIds;

        // Update auth user's top 3 badges for roster card display
        if (authUser) {
          const top3Badges = reorderedBadges.slice(0, 3);
          setAuthUser({
            ...authUser,
            badges: top3Badges,
          });
        }
      } catch (error: any) {
        console.error('Failed to save badge order:', error);

        // Rollback to previous order
        const previousBadges = previousBadgeOrderRef.current
          .map(id => badges.find(b => b.id === id))
          .filter((b): b is UserBadgeDto => b !== undefined);
        setBadges(previousBadges);

        // Show error message
        const message = error?.response?.data?.message || 'Failed to save badge order';
        setBadgeSaveError(message);
        Alert.alert('Error', message);
      }
    }, BADGE_SAVE_DEBOUNCE_MS);
  }, [badges, authUser, setAuthUser]);

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
        {/* Trophy Case Section - at top for easy access */}
        <FormSection title="Trophy Case">
          {isLoadingBadges ? (
            <View style={styles.badgeLoading}>
              <ActivityIndicator size="small" color={colors.primary.teal} />
            </View>
          ) : (
            <TrophyCase
              badges={badges}
              editable={true}
              onOrderChange={handleBadgeOrderChange}
            />
          )}
          {badgeSaveError && (
            <Text style={styles.badgeError}>{badgeSaveError}</Text>
          )}
        </FormSection>

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

        <View style={styles.versionInfo}>
          <Text style={styles.versionText}>
            v{Constants.expoConfig?.version || '?.?.?'} ({Platform.OS === 'ios'
              ? `build ${Constants.expoConfig?.ios?.buildNumber || '?'}`
              : `build ${Constants.expoConfig?.android?.versionCode || '?'}`})
          </Text>
          <Text style={styles.versionText}>
            runtime {typeof Constants.expoConfig?.runtimeVersion === 'string'
              ? Constants.expoConfig.runtimeVersion
              : '?'}
          </Text>
        </View>
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
  badgeLoading: {
    padding: spacing.lg,
    alignItems: 'center',
  },
  badgeError: {
    fontSize: 12,
    color: colors.status.error,
    marginTop: spacing.sm,
    textAlign: 'center',
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
  },
  linkButtonText: {
    color: colors.text.muted,
    fontSize: 14,
    textDecorationLine: 'underline',
  },
  versionInfo: {
    alignItems: 'center',
    marginTop: spacing.xl,
    marginBottom: spacing.xl,
    paddingTop: spacing.lg,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  versionText: {
    color: colors.text.subtle,
    fontSize: 12,
    marginBottom: spacing.xs,
  },
});
