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
import { useRouter } from 'expo-router';
import { Picker } from '@react-native-picker/picker';
import DateTimePicker from '@react-native-community/datetimepicker';
import { useEventStore } from '../../stores/eventStore';
import { useOrganizationStore } from '../../stores/organizationStore';
import type { EventVisibility, SkillLevel } from '@bhmhockey/shared';
import { SkillLevelSelector } from '../../components';

export default function CreateEventScreen() {
  const router = useRouter();
  const { createEvent, isCreating } = useEventStore();
  const { myOrganizations, fetchMyOrganizations } = useOrganizationStore();

  // Fetch user's organizations on mount
  useEffect(() => {
    fetchMyOrganizations();
  }, []);

  // Form state
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [selectedOrgId, setSelectedOrgId] = useState<string | null>(null);
  const [eventDate, setEventDate] = useState(new Date(Date.now() + 7 * 24 * 60 * 60 * 1000)); // Default: 1 week from now
  const [showDatePicker, setShowDatePicker] = useState(false);
  const [showTimePicker, setShowTimePicker] = useState(false);
  const [duration, setDuration] = useState('60');
  const [venue, setVenue] = useState('');
  const [maxPlayers, setMaxPlayers] = useState('12');
  const [cost, setCost] = useState('0');
  const [visibility, setVisibility] = useState<EventVisibility>('Public');
  const [skillLevels, setSkillLevels] = useState<SkillLevel[]>([]);

  // Keyboard accessory ID for iOS
  const inputAccessoryViewID = 'eventFormAccessory';

  const nameRef = useRef<TextInput | null>(null);
  const descriptionRef = useRef<TextInput | null>(null);
  const venueRef = useRef<TextInput | null>(null);
  const maxPlayersRef = useRef<TextInput | null>(null);
  const costRef = useRef<TextInput | null>(null);

  // Picker modal states (for iOS)
  const [showOrgPicker, setShowOrgPicker] = useState(false);
  const [showVisibilityPicker, setShowVisibilityPicker] = useState(false);

  // Reset visibility when org changes (OrganizationMembers only valid with org)
  useEffect(() => {
    if (!selectedOrgId && visibility === 'OrganizationMembers') {
      setVisibility('Public');
    }
  }, [selectedOrgId]);

  // Get display names for pickers
  const getOrgDisplayName = () => {
    if (!selectedOrgId) return 'Myself (Pickup Game)';
    const org = myOrganizations.find(o => o.id === selectedOrgId);
    return org?.name || 'Unknown';
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
    if (eventDate <= new Date()) {
      Alert.alert('Error', 'Event date must be in the future');
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
    if (!validateForm()) return;

    const result = await createEvent({
      organizationId: selectedOrgId || undefined,
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

    if (result) {
      Alert.alert('Success', 'Event created successfully!', [
        { text: 'OK', onPress: () => router.back() }
      ]);
    }
  };

  return (
    <KeyboardAvoidingView
      style={{ flex: 1 }}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={Platform.OS === 'ios' ? 80 : 0}
    >
      <ScrollView
        style={styles.container}
        keyboardShouldPersistTaps="handled"
        keyboardDismissMode={Platform.OS === 'ios' ? 'interactive' : 'on-drag'}
        contentInsetAdjustmentBehavior="automatic"
      >
      {/* Organization Selector (only show if user owns organizations) */}
      {myOrganizations.length > 0 && (
        <View style={styles.field}>
          <Text style={styles.label}>Create for</Text>
          <TouchableOpacity
            style={styles.pickerButton}
            onPress={() => setShowOrgPicker(true)}
          >
            <Text style={styles.pickerButtonText}>{getOrgDisplayName()}</Text>
            <Text style={styles.pickerArrow}>▼</Text>
          </TouchableOpacity>
          {selectedOrgId && (
            <Text style={styles.orgNote}>
              This event will appear under your organization
            </Text>
          )}
        </View>
      )}

      {/* Event Name */}
      <View style={styles.field}>
        <Text style={styles.label}>Event Name *</Text>
        <TextInput
          ref={nameRef}
          style={styles.input}
          value={name}
          onChangeText={setName}
          placeholder="e.g., Friday Night Hockey"
          placeholderTextColor="#999"
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
          placeholderTextColor="#999"
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
          minimumDate={new Date()}
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
          placeholderTextColor="#999"
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
          placeholderTextColor="#999"
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
            placeholderTextColor="#999"
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
            placeholderTextColor="#999"
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
        {visibility === 'OrganizationMembers' && (
          <Text style={styles.visibilityNote}>
            Only subscribers of your organization can see and join this event
          </Text>
        )}
        {visibility === 'InviteOnly' && (
          <Text style={styles.visibilityNote}>
            Only you can see this event for now. Invite system coming soon!
          </Text>
        )}
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
        style={[styles.submitButton, isCreating && styles.submitButtonDisabled]}
        onPress={handleSubmit}
        disabled={isCreating}
      >
        {isCreating ? (
          <ActivityIndicator color="#fff" />
        ) : (
          <Text style={styles.submitButtonText}>Create Event</Text>
        )}
      </TouchableOpacity>

      {/* Bottom padding */}
      <View style={{ height: 40 }} />

      {/* Organization Picker Modal */}
      <Modal
        visible={showOrgPicker}
        transparent
        animationType="slide"
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>Create for</Text>
              <TouchableOpacity onPress={() => setShowOrgPicker(false)}>
                <Text style={styles.modalDone}>Done</Text>
              </TouchableOpacity>
            </View>
            <Picker
              selectedValue={selectedOrgId || 'personal'}
              onValueChange={(value) => setSelectedOrgId(value === 'personal' ? null : value)}
              style={styles.modalPicker}
              {...(Platform.OS === 'android' ? { mode: 'dialog' as const } : {})}
              {...(Platform.OS === 'ios' ? { itemStyle: { color: '#000' }, themeVariant: 'light' as const } : {})}
            >
              <Picker.Item label="Myself (Pickup Game)" value="personal" color="#000" />
              {myOrganizations.map((org) => (
                <Picker.Item key={org.id} label={org.name} value={org.id} color="#000" />
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
              {selectedOrgId && (
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
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
    padding: 16,
  },
  field: {
    marginBottom: 20,
  },
  label: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
    marginBottom: 8,
  },
  input: {
    backgroundColor: '#fff',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  textArea: {
    height: 80,
    textAlignVertical: 'top',
  },
  row: {
    flexDirection: 'row',
  },
  pickerButton: {
    backgroundColor: '#fff',
    borderRadius: 8,
    padding: 12,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  pickerButtonText: {
    fontSize: 16,
    color: '#333',
    flex: 1,
  },
  pickerArrow: {
    fontSize: 12,
    color: '#999',
    marginLeft: 8,
  },
  pickerContainer: {
    backgroundColor: '#fff',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    overflow: 'hidden',
  },
  picker: {
    height: 50,
  },
  orgNote: {
    fontSize: 12,
    color: '#007AFF',
    marginTop: 8,
  },
  visibilityNote: {
    fontSize: 12,
    color: '#856404',
    marginTop: 8,
    fontStyle: 'italic',
  },
  skillNote: {
    fontSize: 12,
    color: '#8B949E',
    marginTop: 4,
  },
  submitButton: {
    backgroundColor: '#007AFF',
    borderRadius: 12,
    padding: 16,
    alignItems: 'center',
    marginTop: 8,
  },
  submitButtonDisabled: {
    backgroundColor: '#A0C4FF',
  },
  submitButtonText: {
    color: '#fff',
    fontSize: 18,
    fontWeight: '600',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    backgroundColor: '#fff',
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    paddingBottom: 34, // Safe area padding
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
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
    color: '#007AFF',
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
