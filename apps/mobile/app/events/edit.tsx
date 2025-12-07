import { useState, useEffect, useRef } from 'react';
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
import { useRouter, useLocalSearchParams, Stack } from 'expo-router';
import { Picker } from '@react-native-picker/picker';
import DateTimePicker from '@react-native-community/datetimepicker';
import { eventService } from '@bhmhockey/api-client';
import type { EventVisibility, SkillLevel, EventDto } from '@bhmhockey/shared';
import { SkillLevelSelector } from '../../components';
import { colors, spacing, radius } from '../../theme';

export default function EditEventScreen() {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();

  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [event, setEvent] = useState<EventDto | null>(null);

  // Form state
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [eventDate, setEventDate] = useState(new Date());
  const [showDatePicker, setShowDatePicker] = useState(false);
  const [showTimePicker, setShowTimePicker] = useState(false);
  const [duration, setDuration] = useState('60');
  const [venue, setVenue] = useState('');
  const [maxPlayers, setMaxPlayers] = useState('12');
  const [cost, setCost] = useState('0');
  const [visibility, setVisibility] = useState<EventVisibility>('Public');
  const [skillLevels, setSkillLevels] = useState<SkillLevel[]>([]);

  // Keyboard accessory ID for iOS
  const inputAccessoryViewID = 'editEventFormAccessory';

  const nameRef = useRef<TextInput | null>(null);
  const descriptionRef = useRef<TextInput | null>(null);
  const venueRef = useRef<TextInput | null>(null);
  const maxPlayersRef = useRef<TextInput | null>(null);
  const costRef = useRef<TextInput | null>(null);

  // Picker modal states (for iOS)
  const [showVisibilityPicker, setShowVisibilityPicker] = useState(false);

  useEffect(() => {
    loadEvent();
  }, [id]);

  const loadEvent = async () => {
    if (!id) return;

    setIsLoading(true);
    try {
      const eventData = await eventService.getById(id);
      setEvent(eventData);

      // Populate form fields
      setName(eventData.name);
      setDescription(eventData.description || '');
      setEventDate(new Date(eventData.eventDate));
      setDuration(String(eventData.duration));
      setVenue(eventData.venue || '');
      setMaxPlayers(String(eventData.maxPlayers));
      setCost(String(eventData.cost));
      setVisibility(eventData.visibility as EventVisibility);
      setSkillLevels((eventData.skillLevels as SkillLevel[]) || []);
    } catch (error) {
      Alert.alert('Error', 'Failed to load event');
      router.back();
    } finally {
      setIsLoading(false);
    }
  };

  const getVisibilityDisplayName = () => {
    switch (visibility) {
      case 'Public': return 'Public - Anyone can join';
      case 'OrganizationMembers': return 'Members Only';
      case 'InviteOnly': return 'Invite Only - Private';
      default: return visibility;
    }
  };

  const handleDateChange = (_: any, selectedDate?: Date) => {
    setShowDatePicker(false);
    if (selectedDate) {
      const newDate = new Date(eventDate);
      newDate.setFullYear(selectedDate.getFullYear());
      newDate.setMonth(selectedDate.getMonth());
      newDate.setDate(selectedDate.getDate());
      setEventDate(newDate);
    }
  };

  const handleTimeChange = (_: any, selectedTime?: Date) => {
    setShowTimePicker(false);
    if (selectedTime) {
      const newDate = new Date(eventDate);
      newDate.setHours(selectedTime.getHours());
      newDate.setMinutes(selectedTime.getMinutes());
      setEventDate(newDate);
    }
  };

  const formatDate = (date: Date) => {
    return date.toLocaleDateString('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  };

  const formatTime = (date: Date) => {
    return date.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
    });
  };

  const validateForm = (): boolean => {
    if (!name.trim()) {
      Alert.alert('Error', 'Please enter an event name');
      return false;
    }
    const durationNum = parseInt(duration, 10);
    if (isNaN(durationNum) || durationNum < 15 || durationNum > 480) {
      Alert.alert('Error', 'Duration must be between 15 and 480 minutes');
      return false;
    }
    const maxPlayersNum = parseInt(maxPlayers, 10);
    if (isNaN(maxPlayersNum) || maxPlayersNum < 2 || maxPlayersNum > 100) {
      Alert.alert('Error', 'Max players must be between 2 and 100');
      return false;
    }
    const costNum = parseFloat(cost);
    if (isNaN(costNum) || costNum < 0) {
      Alert.alert('Error', 'Cost must be 0 or greater');
      return false;
    }
    return true;
  };

  const handleSubmit = async () => {
    if (!id || !validateForm()) return;

    setIsSaving(true);
    try {
      await eventService.update(id, {
        name: name.trim(),
        description: description.trim() || undefined,
        eventDate: eventDate.toISOString(),
        duration: parseInt(duration, 10),
        venue: venue.trim() || undefined,
        maxPlayers: parseInt(maxPlayers, 10),
        cost: parseFloat(cost),
        visibility,
        skillLevels: skillLevels.length > 0 ? skillLevels : undefined,
      });

      Alert.alert('Success', 'Event updated successfully!', [
        { text: 'OK', onPress: () => router.back() }
      ]);
    } catch (error) {
      Alert.alert('Error', 'Failed to update event. Please try again.');
    } finally {
      setIsSaving(false);
    }
  };

  if (isLoading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
      </View>
    );
  }

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Edit Event',
          headerBackTitle: 'Cancel',
          headerStyle: { backgroundColor: colors.bg.darkest },
          headerTintColor: colors.primary.teal,
          headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
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
          contentInsetAdjustmentBehavior="automatic"
        >
          {/* Event Name */}
          <View style={styles.field}>
            <Text style={styles.label}>Event Name *</Text>
            <TextInput
              ref={nameRef}
              style={styles.input}
              value={name}
              onChangeText={setName}
              placeholder="e.g., Friday Night Hockey"
              placeholderTextColor={colors.text.placeholder}
              returnKeyType="next"
              onSubmitEditing={() => descriptionRef.current?.focus()}
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>

          {/* Description */}
          <View style={styles.field}>
            <Text style={styles.label}>Description</Text>
            <TextInput
              ref={descriptionRef}
              style={[styles.input, styles.textArea]}
              value={description}
              onChangeText={setDescription}
              placeholder="Tell people what to expect..."
              placeholderTextColor={colors.text.placeholder}
              multiline
              numberOfLines={3}
              returnKeyType="next"
              blurOnSubmit
              onSubmitEditing={() => venueRef.current?.focus()}
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>

          {/* Date & Time */}
          <View style={styles.row}>
            <View style={[styles.field, { flex: 1, marginRight: 8 }]}>
              <Text style={styles.label}>Date *</Text>
              <TouchableOpacity
                style={styles.pickerButton}
                onPress={() => setShowDatePicker(true)}
              >
                <Text style={styles.pickerButtonText}>{formatDate(eventDate)}</Text>
              </TouchableOpacity>
            </View>
            <View style={[styles.field, { flex: 1, marginLeft: 8 }]}>
              <Text style={styles.label}>Time *</Text>
              <TouchableOpacity
                style={styles.pickerButton}
                onPress={() => setShowTimePicker(true)}
              >
                <Text style={styles.pickerButtonText}>{formatTime(eventDate)}</Text>
              </TouchableOpacity>
            </View>
          </View>

          {showDatePicker && (
            <DateTimePicker
              value={eventDate}
              mode="date"
              display={Platform.OS === 'ios' ? 'spinner' : 'default'}
              onChange={handleDateChange}
            />
          )}

          {showTimePicker && (
            <DateTimePicker
              value={eventDate}
              mode="time"
              display={Platform.OS === 'ios' ? 'spinner' : 'default'}
              onChange={handleTimeChange}
            />
          )}

          {/* Duration */}
          <View style={styles.field}>
            <Text style={styles.label}>Duration (minutes) *</Text>
            <TextInput
              style={styles.input}
              value={duration}
              onChangeText={setDuration}
              placeholder="60"
              placeholderTextColor={colors.text.placeholder}
              keyboardType="number-pad"
            />
          </View>

          {/* Venue */}
          <View style={styles.field}>
            <Text style={styles.label}>Venue</Text>
            <TextInput
              ref={venueRef}
              style={styles.input}
              value={venue}
              onChangeText={setVenue}
              placeholder="e.g., Birmingham Ice Arena"
              placeholderTextColor={colors.text.placeholder}
              returnKeyType="next"
              onSubmitEditing={() => maxPlayersRef.current?.focus()}
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>

          {/* Max Players & Cost */}
          <View style={styles.row}>
            <View style={[styles.field, { flex: 1, marginRight: 8 }]}>
              <Text style={styles.label}>Max Players *</Text>
              <TextInput
                ref={maxPlayersRef}
                style={styles.input}
                value={maxPlayers}
                onChangeText={setMaxPlayers}
                placeholder="12"
                placeholderTextColor={colors.text.placeholder}
                keyboardType="number-pad"
                returnKeyType="next"
                onSubmitEditing={() => costRef.current?.focus()}
                inputAccessoryViewID={inputAccessoryViewID}
              />
            </View>
            <View style={[styles.field, { flex: 1, marginLeft: 8 }]}>
              <Text style={styles.label}>Cost ($)</Text>
              <TextInput
                ref={costRef}
                style={styles.input}
                value={cost}
                onChangeText={setCost}
                placeholder="0"
                placeholderTextColor={colors.text.placeholder}
                keyboardType="decimal-pad"
                returnKeyType="done"
                onSubmitEditing={() => Keyboard.dismiss()}
                inputAccessoryViewID={inputAccessoryViewID}
              />
            </View>
          </View>

          {/* Visibility */}
          <View style={styles.field}>
            <Text style={styles.label}>Who can see this event?</Text>
            <TouchableOpacity
              style={styles.pickerButton}
              onPress={() => setShowVisibilityPicker(true)}
            >
              <Text style={styles.pickerButtonText}>{getVisibilityDisplayName()}</Text>
              <Text style={styles.pickerArrow}>▼</Text>
            </TouchableOpacity>
          </View>

          {/* Skill Levels */}
          <View style={styles.field}>
            <SkillLevelSelector
              selected={skillLevels}
              onChange={setSkillLevels}
              label="Skill Levels (optional)"
            />
            <Text style={styles.skillNote}>
              Leave empty to allow all skill levels
            </Text>
          </View>

          {/* Submit Button */}
          <TouchableOpacity
            style={[styles.submitButton, isSaving && styles.submitButtonDisabled]}
            onPress={handleSubmit}
            disabled={isSaving}
          >
            {isSaving ? (
              <ActivityIndicator color={colors.bg.darkest} />
            ) : (
              <Text style={styles.submitButtonText}>Save Changes</Text>
            )}
          </TouchableOpacity>

          {/* Bottom padding */}
          <View style={{ height: 40 }} />

          {/* Visibility Picker Modal */}
          <Modal
            visible={showVisibilityPicker}
            transparent
            animationType="slide"
          >
            <View style={styles.modalOverlay}>
              <View style={styles.modalContent}>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>Visibility</Text>
                  <TouchableOpacity onPress={() => setShowVisibilityPicker(false)}>
                    <Text style={styles.modalDone}>Done</Text>
                  </TouchableOpacity>
                </View>
                <Picker
                  selectedValue={visibility}
                  onValueChange={(value) => setVisibility(value)}
                  style={styles.modalPicker}
                  {...(Platform.OS === 'android' ? { mode: 'dialog' as const } : {})}
                  {...(Platform.OS === 'ios' ? { itemStyle: { color: '#000' }, themeVariant: 'light' as const } : {})}
                >
                  <Picker.Item label="Public - Anyone can join" value="Public" color="#000" />
                  {event?.organizationId && (
                    <Picker.Item label="Members Only - Organization subscribers" value="OrganizationMembers" color="#000" />
                  )}
                  <Picker.Item label="Invite Only - Private event" value="InviteOnly" color="#000" />
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
  textArea: {
    height: 80,
    textAlignVertical: 'top',
  },
  row: {
    flexDirection: 'row',
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
  pickerArrow: {
    fontSize: 12,
    color: colors.text.muted,
    marginLeft: spacing.sm,
  },
  skillNote: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: spacing.xs,
  },
  submitButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  submitButtonDisabled: {
    opacity: 0.7,
  },
  submitButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    backgroundColor: '#fff',
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    paddingBottom: 34,
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
  },
  modalDone: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.primary.teal,
  },
  modalPicker: {
    height: 200,
  },
  accessoryBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'flex-end',
    paddingHorizontal: 12,
    paddingVertical: 10,
    borderTopWidth: 1,
    borderTopColor: '#e0e0e0',
    backgroundColor: '#fff',
  },
});
