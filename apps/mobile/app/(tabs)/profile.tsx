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
} from 'react-native';
import { useRouter } from 'expo-router';
import { Picker } from '@react-native-picker/picker';
import { userService } from '@bhmhockey/api-client';
import { useAuthStore } from '../../stores/authStore';
import { useOrganizationStore } from '../../stores/organizationStore';
import { useEventStore } from '../../stores/eventStore';
import type { User, SkillLevel, Position } from '@bhmhockey/shared';
import { SKILL_LEVELS, POSITIONS } from '@bhmhockey/shared';

export default function ProfileScreen() {
  const router = useRouter();
  const { user: authUser, setUser: setAuthUser, logout } = useAuthStore();
  const { mySubscriptions, fetchMySubscriptions } = useOrganizationStore();
  const { myRegistrations, fetchMyRegistrations } = useEventStore();

  const [user, setUser] = useState<User | null>(authUser);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  // Form state
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [skillLevel, setSkillLevel] = useState<SkillLevel | ''>('');
  const [position, setPosition] = useState<Position | ''>('');
  const [venmoHandle, setVenmoHandle] = useState('');

  useEffect(() => {
    if (authUser) {
      setUser(authUser);
      setFirstName(authUser.firstName);
      setLastName(authUser.lastName);
      setPhoneNumber(authUser.phoneNumber || '');
      setSkillLevel(authUser.skillLevel || '');
      setPosition(authUser.position || '');
      setVenmoHandle(authUser.venmoHandle || '');
    }
  }, [authUser]);

  useEffect(() => {
    if (authUser) {
      fetchMySubscriptions();
      fetchMyRegistrations();
    }
  }, [authUser]);

  const handleSave = async () => {
    try {
      setSaving(true);

      const updates = {
        firstName,
        lastName,
        phoneNumber: phoneNumber || undefined,
        skillLevel: skillLevel || undefined,
        position: position || undefined,
        venmoHandle: venmoHandle || undefined,
      };

      const updatedUser = await userService.updateProfile(updates);
      setUser(updatedUser);
      setAuthUser(updatedUser);

      Alert.alert('Success', 'Profile updated successfully!');
    } catch (error) {
      console.error('Failed to update profile:', error);
      Alert.alert('Error', 'Failed to update profile. Please try again.');
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
          <Text style={styles.sectionTitle}>Hockey Profile</Text>

          <View style={styles.field}>
            <Text style={styles.label}>Skill Level</Text>
            <View style={styles.pickerContainer}>
              <Picker
                selectedValue={skillLevel}
                onValueChange={(value) => setSkillLevel(value as SkillLevel)}
                style={styles.picker}
              >
                <Picker.Item label="Select skill level..." value="" />
                {SKILL_LEVELS.map((level) => (
                  <Picker.Item key={level} label={level} value={level} />
                ))}
              </Picker>
            </View>
          </View>

          <View style={styles.field}>
            <Text style={styles.label}>Position</Text>
            <View style={styles.pickerContainer}>
              <Picker
                selectedValue={position}
                onValueChange={(value) => setPosition(value as Position)}
                style={styles.picker}
              >
                <Picker.Item label="Select position..." value="" />
                {POSITIONS.map((pos) => (
                  <Picker.Item key={pos} label={pos} value={pos} />
                ))}
              </Picker>
            </View>
          </View>

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

        {mySubscriptions.length > 0 && (
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>My Organizations</Text>
            {mySubscriptions.map((sub) => (
              <TouchableOpacity
                key={sub.id}
                style={styles.subscriptionCard}
                onPress={() => router.push(`/organizations/${sub.organization.id}`)}
              >
                <View style={styles.subscriptionInfo}>
                  <Text style={styles.subscriptionName}>{sub.organization.name}</Text>
                  {sub.organization.skillLevel && (
                    <Text style={styles.subscriptionSkill}>{sub.organization.skillLevel}</Text>
                  )}
                </View>
                <Text style={styles.subscriptionArrow}>›</Text>
              </TouchableOpacity>
            ))}
          </View>
        )}

        {myRegistrations.length > 0 && (
          <View style={styles.section}>
            <Text style={styles.sectionTitle}>My Upcoming Events</Text>
            {myRegistrations.slice(0, 5).map((event) => (
              <TouchableOpacity
                key={event.id}
                style={styles.subscriptionCard}
                onPress={() => router.push(`/events/${event.id}`)}
              >
                <View style={styles.subscriptionInfo}>
                  <Text style={styles.subscriptionName}>{event.name}</Text>
                  <Text style={styles.subscriptionSkill}>
                    {new Date(event.eventDate).toLocaleDateString('en-US', {
                      weekday: 'short',
                      month: 'short',
                      day: 'numeric',
                    })} - {event.organizationName}
                  </Text>
                </View>
                <Text style={styles.subscriptionArrow}>›</Text>
              </TouchableOpacity>
            ))}
          </View>
        )}

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
    marginBottom: 16,
    color: '#333',
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
  pickerContainer: {
    backgroundColor: '#f9f9f9',
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    overflow: 'hidden',
  },
  picker: {
    height: 50,
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
  subscriptionCard: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingVertical: 12,
    paddingHorizontal: 4,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  subscriptionInfo: {
    flex: 1,
  },
  subscriptionName: {
    fontSize: 16,
    fontWeight: '500',
    color: '#333',
  },
  subscriptionSkill: {
    fontSize: 12,
    color: '#666',
    marginTop: 2,
  },
  subscriptionArrow: {
    fontSize: 24,
    color: '#ccc',
    marginLeft: 8,
  },
});
