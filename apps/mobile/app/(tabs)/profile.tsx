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
        <ActivityIndicator size="large" color="#007AFF" />
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
            />
          </View>

          <View style={styles.field}>
            <Text style={styles.label}>Last Name</Text>
            <TextInput
              style={styles.input}
              value={lastName}
              onChangeText={setLastName}
              placeholder="Enter last name"
            />
          </View>

          <View style={styles.field}>
            <Text style={styles.label}>Phone Number</Text>
            <TextInput
              style={styles.input}
              value={phoneNumber}
              onChangeText={setPhoneNumber}
              placeholder="(123) 456-7890"
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
                trackColor={{ false: '#e0e0e0', true: '#007AFF' }}
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
                >
                  {SKILL_LEVELS.map((level) => (
                    <Picker.Item key={level} label={level} value={level} color="#000" />
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
                trackColor={{ false: '#e0e0e0', true: '#007AFF' }}
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
                >
                  {SKILL_LEVELS.map((level) => (
                    <Picker.Item key={level} label={level} value={level} color="#000" />
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
    backgroundColor: '#f5f5f5',
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 12,
    fontSize: 16,
    color: '#666',
  },
  header: {
    padding: 20,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 16,
    color: '#666',
  },
  form: {
    padding: 20,
  },
  section: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 20,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 4,
    color: '#333',
  },
  sectionHint: {
    fontSize: 14,
    color: '#666',
    marginBottom: 16,
  },
  field: {
    marginBottom: 16,
  },
  label: {
    fontSize: 14,
    fontWeight: '500',
    marginBottom: 8,
    color: '#333',
  },
  input: {
    backgroundColor: '#f9f9f9',
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
  },
  positionRow: {
    marginBottom: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
    paddingBottom: 16,
  },
  positionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 8,
  },
  positionLabel: {
    fontSize: 16,
    marginLeft: 12,
    color: '#666',
  },
  positionLabelActive: {
    color: '#007AFF',
    fontWeight: '600',
  },
  skillPickerContainer: {
    backgroundColor: '#f9f9f9',
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    overflow: 'hidden',
    marginLeft: 52, // Align with label after switch
  },
  skillPicker: {
    height: Platform.OS === 'ios' ? 120 : 50,
    width: '100%',
  },
  pickerItem: {
    fontSize: 16,
    color: '#000',
  },
  hint: {
    fontSize: 12,
    color: '#999',
    marginTop: 4,
  },
  saveButton: {
    backgroundColor: '#007AFF',
    borderRadius: 12,
    padding: 16,
    alignItems: 'center',
    marginTop: 10,
  },
  saveButtonDisabled: {
    opacity: 0.6,
  },
  saveButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
  logoutButton: {
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#FF3B30',
    borderRadius: 12,
    padding: 16,
    alignItems: 'center',
    marginTop: 10,
  },
  logoutButtonText: {
    color: '#FF3B30',
    fontSize: 16,
    fontWeight: '600',
  },
});
