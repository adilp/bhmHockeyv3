import { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TextInput,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
  Platform,
  Switch,
} from 'react-native';
import { useRouter } from 'expo-router';
import { Picker } from '@react-native-picker/picker';
import { userService } from '@bhmhockey/api-client';
import { useAuthStore } from '../../stores/authStore';
import type { User, SkillLevel, UserPositions } from '@bhmhockey/shared';
import { SKILL_LEVELS } from '@bhmhockey/shared';
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

      // Load positions
      if (authUser.positions) {
        if (authUser.positions.goalie) {
          setIsGoalie(true);
          setGoalieSkill(authUser.positions.goalie);
        }
        if (authUser.positions.skater) {
          setIsSkater(true);
          setSkaterSkill(authUser.positions.skater);
        }
      }
    }
  }, [authUser]);

  const handleSave = async () => {
    // Validate at least one position is selected
    if (!isGoalie && !isSkater) {
      Alert.alert('Error', 'Please select at least one position (Goalie or Skater)');
      return;
    }

    try {
      setSaving(true);

      // Build positions object
      const positions: UserPositions = {};
      if (isGoalie) {
        positions.goalie = goalieSkill;
      }
      if (isSkater) {
        positions.skater = skaterSkill;
      }

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
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Basic Information</Text>

          <View style={styles.field}>
            <Text style={styles.label}>First Name</Text>
            <TextInput
              style={styles.input}
              value={firstName}
              onChangeText={setFirstName}
              placeholder="Enter first name"
              placeholderTextColor={colors.text.muted}
            />
          </View>

          <View style={styles.field}>
            <Text style={styles.label}>Last Name</Text>
            <TextInput
              style={styles.input}
              value={lastName}
              onChangeText={setLastName}
              placeholder="Enter last name"
              placeholderTextColor={colors.text.muted}
            />
          </View>

          <View style={styles.field}>
            <Text style={styles.label}>Phone Number</Text>
            <TextInput
              style={styles.input}
              value={phoneNumber}
              onChangeText={setPhoneNumber}
              placeholder="(123) 456-7890"
              placeholderTextColor={colors.text.muted}
              keyboardType="phone-pad"
            />
          </View>
        </View>

        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Positions</Text>
          <Text style={styles.sectionHint}>Select at least one position</Text>

          {/* Goalie Position */}
          <View style={styles.positionRow}>
            <View style={styles.positionHeader}>
              <Switch
                value={isGoalie}
                onValueChange={setIsGoalie}
                trackColor={{ false: colors.bg.hover, true: colors.primary.teal }}
                thumbColor={isGoalie ? colors.text.primary : colors.text.muted}
              />
              <Text style={[styles.positionLabel, isGoalie && styles.positionLabelActive]}>
                Goalie
              </Text>
            </View>
            {isGoalie && (
              <View style={styles.skillPickerContainer}>
                <Picker
                  selectedValue={goalieSkill}
                  onValueChange={(value) => setGoalieSkill(value as SkillLevel)}
                  style={styles.skillPicker}
                  itemStyle={styles.pickerItem}
                  dropdownIconColor={colors.text.primary}
                >
                  {SKILL_LEVELS.map((level) => (
                    <Picker.Item key={level} label={level} value={level} color={Platform.OS === 'ios' ? colors.text.primary : undefined} />
                  ))}
                </Picker>
              </View>
            )}
          </View>

          {/* Skater Position */}
          <View style={styles.positionRow}>
            <View style={styles.positionHeader}>
              <Switch
                value={isSkater}
                onValueChange={setIsSkater}
                trackColor={{ false: colors.bg.hover, true: colors.primary.teal }}
                thumbColor={isSkater ? colors.text.primary : colors.text.muted}
              />
              <Text style={[styles.positionLabel, isSkater && styles.positionLabelActive]}>
                Skater
              </Text>
            </View>
            {isSkater && (
              <View style={styles.skillPickerContainer}>
                <Picker
                  selectedValue={skaterSkill}
                  onValueChange={(value) => setSkaterSkill(value as SkillLevel)}
                  style={styles.skillPicker}
                  itemStyle={styles.pickerItem}
                  dropdownIconColor={colors.text.primary}
                >
                  {SKILL_LEVELS.map((level) => (
                    <Picker.Item key={level} label={level} value={level} color={Platform.OS === 'ios' ? colors.text.primary : undefined} />
                  ))}
                </Picker>
              </View>
            )}
          </View>
        </View>

        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Payment</Text>

          <View style={styles.field}>
            <Text style={styles.label}>Venmo Handle</Text>
            <TextInput
              style={styles.input}
              value={venmoHandle}
              onChangeText={setVenmoHandle}
              placeholder="@username"
              placeholderTextColor={colors.text.muted}
              autoCapitalize="none"
            />
            <Text style={styles.hint}>For receiving payments from events</Text>
          </View>
        </View>

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
  section: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginBottom: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: spacing.xs,
    color: colors.text.primary,
  },
  sectionHint: {
    fontSize: 14,
    color: colors.text.muted,
    marginBottom: spacing.md,
  },
  field: {
    marginBottom: spacing.md,
  },
  label: {
    fontSize: 14,
    fontWeight: '500',
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
  positionRow: {
    marginBottom: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
    paddingBottom: spacing.md,
  },
  positionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  positionLabel: {
    fontSize: 16,
    marginLeft: spacing.md,
    color: colors.text.muted,
  },
  positionLabelActive: {
    color: colors.primary.teal,
    fontWeight: '600',
  },
  skillPickerContainer: {
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderRadius: radius.md,
    overflow: 'hidden',
    marginLeft: 52,
  },
  skillPicker: {
    height: Platform.OS === 'ios' ? 120 : 50,
    width: '100%',
    color: colors.text.primary,
  },
  pickerItem: {
    fontSize: 16,
    color: colors.text.primary,
  },
  hint: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: spacing.xs,
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
});
