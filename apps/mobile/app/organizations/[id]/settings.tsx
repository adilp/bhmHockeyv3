import { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TextInput,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  Platform,
  Modal,
  KeyboardAvoidingView,
  Keyboard,
  InputAccessoryView,
} from 'react-native';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { Picker } from '@react-native-picker/picker';
import DateTimePicker from '@react-native-community/datetimepicker';
import { organizationService } from '@bhmhockey/api-client';
import type { Organization, EventVisibility } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../../theme';

const DAYS_OF_WEEK = [
  'Sunday',
  'Monday',
  'Tuesday',
  'Wednesday',
  'Thursday',
  'Friday',
  'Saturday',
];

// Platform-specific Picker props for dark theme
const pickerProps = Platform.select({
  ios: { itemStyle: { color: colors.text.primary }, themeVariant: 'dark' as const },
  android: { mode: 'dialog' as const },
}) ?? {};

const getPickerItemColor = () => Platform.OS === 'ios' ? colors.text.primary : undefined;

export default function OrganizationSettingsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();

  const [organization, setOrganization] = useState<Organization | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  // Form state
  const [defaultDayOfWeek, setDefaultDayOfWeek] = useState<number | null>(null);
  const [defaultStartTime, setDefaultStartTime] = useState<Date | null>(null);
  const [defaultDurationMinutes, setDefaultDurationMinutes] = useState('');
  const [defaultMaxPlayers, setDefaultMaxPlayers] = useState('');
  const [defaultCost, setDefaultCost] = useState('');
  const [defaultVenue, setDefaultVenue] = useState('');
  const [defaultVisibility, setDefaultVisibility] = useState<EventVisibility | null>(null);

  // UI state
  const [showDayPicker, setShowDayPicker] = useState(false);
  const [showTimePicker, setShowTimePicker] = useState(false);
  const [showVisibilityPicker, setShowVisibilityPicker] = useState(false);

  // Keyboard accessory ID for iOS
  const inputAccessoryViewID = 'settingsFormAccessory';

  useEffect(() => {
    loadOrganization();
  }, [id]);

  const loadOrganization = async () => {
    if (!id) return;

    setIsLoading(true);
    try {
      const org = await organizationService.getById(id);

      // Check if user is admin
      if (!org.isAdmin) {
        Alert.alert('Access Denied', 'Only organization admins can edit event defaults');
        router.back();
        return;
      }

      setOrganization(org);

      // Load existing defaults
      setDefaultDayOfWeek(org.defaultDayOfWeek ?? null);

      // Convert time string to Date object for picker
      if (org.defaultStartTime) {
        const [hours, minutes] = org.defaultStartTime.split(':').map(Number);
        const date = new Date();
        date.setHours(hours, minutes, 0, 0);
        setDefaultStartTime(date);
      }

      const toStr = (val: number | null | undefined): string => val != null ? String(val) : '';
      setDefaultDurationMinutes(toStr(org.defaultDurationMinutes));
      setDefaultMaxPlayers(toStr(org.defaultMaxPlayers));
      setDefaultCost(toStr(org.defaultCost));
      setDefaultVenue(org.defaultVenue ?? '');
      setDefaultVisibility(org.defaultVisibility ?? null);
    } catch (error) {
      Alert.alert('Error', 'Failed to load organization');
      router.back();
    } finally {
      setIsLoading(false);
    }
  };

  const handleTimeChange = (_: any, selectedTime?: Date) => {
    setShowTimePicker(false);
    if (selectedTime) {
      setDefaultStartTime(selectedTime);
    }
  };

  const formatTime = (date: Date | null) => {
    if (!date) return 'Not set';
    return date.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
    });
  };

  const getDayDisplayName = () => {
    if (defaultDayOfWeek === null) return 'Not set';
    return DAYS_OF_WEEK[defaultDayOfWeek];
  };

  const getVisibilityDisplayName = () => {
    if (!defaultVisibility) return 'Not set';
    switch (defaultVisibility) {
      case 'Public': return 'Public - Anyone can join';
      case 'OrganizationMembers': return 'Members Only';
      case 'InviteOnly': return 'Invite Only - Private';
      default: return defaultVisibility;
    }
  };

  const validateForm = (): boolean => {
    // Duration validation (if provided)
    if (defaultDurationMinutes.trim()) {
      const durationNum = parseInt(defaultDurationMinutes, 10);
      if (isNaN(durationNum) || durationNum < 15 || durationNum > 480) {
        Alert.alert('Error', 'Duration must be between 15 and 480 minutes');
        return false;
      }
    }

    // Max players validation (if provided)
    if (defaultMaxPlayers.trim()) {
      const maxPlayersNum = parseInt(defaultMaxPlayers, 10);
      if (isNaN(maxPlayersNum) || maxPlayersNum < 2 || maxPlayersNum > 100) {
        Alert.alert('Error', 'Max players must be between 2 and 100');
        return false;
      }
    }

    // Cost validation (if provided)
    if (defaultCost.trim()) {
      const costNum = parseFloat(defaultCost);
      if (isNaN(costNum) || costNum < 0) {
        Alert.alert('Error', 'Cost must be a positive number or 0');
        return false;
      }
    }

    return true;
  };

  const handleSave = async () => {
    if (!id || !organization) return;
    if (!validateForm()) return;

    setIsSaving(true);
    try {
      // Convert time to HH:mm:ss format
      const timeString = defaultStartTime
        ? `${String(defaultStartTime.getHours()).padStart(2, '0')}:${String(defaultStartTime.getMinutes()).padStart(2, '0')}:00`
        : null;

      const parseIntOrNull = (val: string): number | null => val.trim() ? parseInt(val, 10) : null;
      const parseFloatOrNull = (val: string): number | null => val.trim() ? parseFloat(val) : null;

      await organizationService.update(id, {
        defaultDayOfWeek,
        defaultStartTime: timeString,
        defaultDurationMinutes: parseIntOrNull(defaultDurationMinutes),
        defaultMaxPlayers: parseIntOrNull(defaultMaxPlayers),
        defaultCost: parseFloatOrNull(defaultCost),
        defaultVenue: defaultVenue.trim() || null,
        defaultVisibility,
      });

      Alert.alert('Success', 'Event defaults updated successfully', [
        { text: 'OK', onPress: () => router.back() }
      ]);
    } catch (error) {
      Alert.alert('Error', 'Failed to update event defaults');
    } finally {
      setIsSaving(false);
    }
  };

  const handleClearTime = () => {
    setDefaultStartTime(null);
  };

  if (isLoading) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Event Defaults',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.dark },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      </>
    );
  }

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Event Defaults',
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
        }}
      />

      <KeyboardAvoidingView
        style={{ flex: 1, backgroundColor: colors.bg.darkest }}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        keyboardVerticalOffset={Platform.OS === 'ios' ? 80 : 0}
      >
        <ScrollView
          style={styles.container}
          keyboardShouldPersistTaps="handled"
          keyboardDismissMode={Platform.OS === 'ios' ? 'interactive' : 'on-drag'}
        >
          {/* Explanation */}
          <View style={styles.explanationBox}>
            <Text style={styles.explanationText}>
              These defaults auto-fill when creating events for this organization. All fields are optional.
            </Text>
          </View>

          {/* Default Day */}
          <View style={styles.field}>
            <Text style={styles.label}>Default Day</Text>
            <TouchableOpacity
              style={styles.pickerButton}
              onPress={() => setShowDayPicker(true)}
            >
              <Text style={[
                styles.pickerButtonText,
                defaultDayOfWeek === null && styles.placeholderText
              ]}>
                {getDayDisplayName()}
              </Text>
              <Text style={styles.pickerArrow}>▼</Text>
            </TouchableOpacity>
          </View>

          {/* Default Time */}
          <View style={styles.field}>
            <Text style={styles.label}>Default Time</Text>
            <View style={styles.timeRow}>
              <TouchableOpacity
                style={[styles.pickerButton, { flex: 1 }]}
                onPress={() => setShowTimePicker(true)}
              >
                <Text style={[
                  styles.pickerButtonText,
                  defaultStartTime === null && styles.placeholderText
                ]}>
                  {formatTime(defaultStartTime)}
                </Text>
              </TouchableOpacity>
              {defaultStartTime !== null && (
                <TouchableOpacity
                  style={styles.clearButton}
                  onPress={handleClearTime}
                >
                  <Text style={styles.clearButtonText}>Clear</Text>
                </TouchableOpacity>
              )}
            </View>
          </View>

          {showTimePicker && (
            <DateTimePicker
              value={defaultStartTime || new Date()}
              mode="time"
              display={Platform.OS === 'ios' ? 'spinner' : 'default'}
              onChange={handleTimeChange}
            />
          )}

          {/* Duration */}
          <View style={styles.field}>
            <Text style={styles.label}>Duration (minutes)</Text>
            <TextInput
              style={styles.input}
              value={defaultDurationMinutes}
              onChangeText={setDefaultDurationMinutes}
              placeholder="Not set"
              placeholderTextColor={colors.text.muted}
              keyboardType="number-pad"
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>

          {/* Max Skaters */}
          <View style={styles.field}>
            <Text style={styles.label}>Max Skaters</Text>
            <TextInput
              style={styles.input}
              value={defaultMaxPlayers}
              onChangeText={setDefaultMaxPlayers}
              placeholder="Not set"
              placeholderTextColor={colors.text.muted}
              keyboardType="number-pad"
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>

          {/* Cost */}
          <View style={styles.field}>
            <Text style={styles.label}>Cost ($)</Text>
            <TextInput
              style={styles.input}
              value={defaultCost}
              onChangeText={setDefaultCost}
              placeholder="Not set"
              placeholderTextColor={colors.text.muted}
              keyboardType="decimal-pad"
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>

          {/* Venue */}
          <View style={styles.field}>
            <Text style={styles.label}>Venue</Text>
            <TextInput
              style={styles.input}
              value={defaultVenue}
              onChangeText={setDefaultVenue}
              placeholder="Not set"
              placeholderTextColor={colors.text.muted}
              returnKeyType="done"
              onSubmitEditing={() => Keyboard.dismiss()}
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>

          {/* Visibility */}
          <View style={styles.field}>
            <Text style={styles.label}>Visibility</Text>
            <TouchableOpacity
              style={styles.pickerButton}
              onPress={() => setShowVisibilityPicker(true)}
            >
              <Text style={[
                styles.pickerButtonText,
                defaultVisibility === null && styles.placeholderText
              ]}>
                {getVisibilityDisplayName()}
              </Text>
              <Text style={styles.pickerArrow}>▼</Text>
            </TouchableOpacity>
          </View>

          {/* Save Button */}
          <TouchableOpacity
            style={[styles.saveButton, isSaving && styles.saveButtonDisabled]}
            onPress={handleSave}
            disabled={isSaving}
          >
            {isSaving ? (
              <ActivityIndicator color={colors.bg.darkest} />
            ) : (
              <Text style={styles.saveButtonText}>Save Defaults</Text>
            )}
          </TouchableOpacity>

          {/* Bottom padding */}
          <View style={{ height: 40 }} />

          {/* Day Picker Modal */}
          <Modal
            visible={showDayPicker}
            transparent
            animationType="slide"
          >
            <View style={styles.modalOverlay}>
              <View style={styles.modalContent}>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>Default Day</Text>
                  <TouchableOpacity onPress={() => setShowDayPicker(false)}>
                    <Text style={styles.modalDone}>Done</Text>
                  </TouchableOpacity>
                </View>
                <Picker
                  selectedValue={defaultDayOfWeek ?? 'none'}
                  onValueChange={(value) => setDefaultDayOfWeek(value === 'none' ? null : (value as number))}
                  style={styles.modalPicker}
                  dropdownIconColor={colors.text.primary}
                  {...pickerProps}
                >
                  <Picker.Item label="Not set" value="none" color={getPickerItemColor()} />
                  {DAYS_OF_WEEK.map((day, index) => (
                    <Picker.Item key={index} label={day} value={index} color={getPickerItemColor()} />
                  ))}
                </Picker>
              </View>
            </View>
          </Modal>

          {/* Visibility Picker Modal */}
          <Modal
            visible={showVisibilityPicker}
            transparent
            animationType="slide"
          >
            <View style={styles.modalOverlay}>
              <View style={styles.modalContent}>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>Default Visibility</Text>
                  <TouchableOpacity onPress={() => setShowVisibilityPicker(false)}>
                    <Text style={styles.modalDone}>Done</Text>
                  </TouchableOpacity>
                </View>
                <Picker
                  selectedValue={defaultVisibility ?? 'none'}
                  onValueChange={(value) => setDefaultVisibility(value === 'none' ? null : (value as EventVisibility))}
                  style={styles.modalPicker}
                  dropdownIconColor={colors.text.primary}
                  {...pickerProps}
                >
                  <Picker.Item label="Not set" value="none" color={getPickerItemColor()} />
                  <Picker.Item label="Public - Anyone can join" value="Public" color={getPickerItemColor()} />
                  <Picker.Item label="Members Only - Organization subscribers" value="OrganizationMembers" color={getPickerItemColor()} />
                  <Picker.Item label="Invite Only - Private event" value="InviteOnly" color={getPickerItemColor()} />
                </Picker>
              </View>
            </View>
          </Modal>

          {/* iOS keyboard accessory with Done button */}
          {Platform.OS === 'ios' && (
            <InputAccessoryView nativeID={inputAccessoryViewID}>
              <View style={styles.accessoryBar}>
                <View style={{ flex: 1 }} />
                <TouchableOpacity onPress={() => Keyboard.dismiss()}>
                  <Text style={styles.modalDone}>Done</Text>
                </TouchableOpacity>
              </View>
            </InputAccessoryView>
          )}
        </ScrollView>
      </KeyboardAvoidingView>
    </>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
    padding: spacing.md,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
  explanationBox: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.md,
    marginBottom: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  explanationText: {
    fontSize: 14,
    color: colors.text.secondary,
    lineHeight: 20,
  },
  field: {
    marginBottom: spacing.lg,
  },
  label: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.xs,
  },
  input: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 15,
    borderWidth: 1,
    borderColor: colors.border.default,
    color: colors.text.primary,
  },
  pickerButton: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  pickerButtonText: {
    fontSize: 15,
    color: colors.text.primary,
    flex: 1,
  },
  placeholderText: {
    color: colors.text.muted,
  },
  pickerArrow: {
    fontSize: 12,
    color: colors.text.muted,
    marginLeft: spacing.sm,
  },
  timeRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  clearButton: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    backgroundColor: colors.bg.hover,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  clearButtonText: {
    fontSize: 14,
    color: colors.text.secondary,
    fontWeight: '600',
  },
  saveButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  saveButtonDisabled: {
    opacity: 0.7,
  },
  saveButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    backgroundColor: colors.bg.dark,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    paddingBottom: 34,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderBottomWidth: 0,
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
  },
  modalDone: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.primary.teal,
  },
  modalPicker: {
    height: 200,
    backgroundColor: colors.bg.dark,
  },
  accessoryBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'flex-end',
    paddingHorizontal: 12,
    paddingVertical: 10,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
    backgroundColor: colors.bg.elevated,
  },
});
