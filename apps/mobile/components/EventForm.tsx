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
import { Picker } from '@react-native-picker/picker';
import DateTimePicker from '@react-native-community/datetimepicker';
import type { EventVisibility, SkillLevel, EventDto, Organization } from '@bhmhockey/shared';
import { SkillLevelSelector } from './SkillLevelSelector';
import { colors, spacing, radius } from '../theme';

export interface EventFormData {
  organizationId?: string;
  name?: string;
  description?: string;
  eventDate: string;
  duration?: number;
  venue?: string;
  maxPlayers: number;
  cost: number;
  visibility: EventVisibility;
  skillLevels?: SkillLevel[];
}

interface EventFormProps {
  mode: 'create' | 'edit';
  initialData?: EventDto;
  organizations?: Organization[];
  onSubmit: (data: EventFormData) => Promise<boolean>;
  isSubmitting: boolean;
  footer?: React.ReactNode;
}

export function EventForm({
  mode,
  initialData,
  organizations = [],
  onSubmit,
  isSubmitting,
  footer,
}: EventFormProps) {
  // Form state
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [selectedOrgId, setSelectedOrgId] = useState<string | null>(null);
  const [eventDate, setEventDate] = useState(new Date(Date.now() + 7 * 24 * 60 * 60 * 1000));
  const [showDatePicker, setShowDatePicker] = useState(false);
  const [showTimePicker, setShowTimePicker] = useState(false);
  const [duration, setDuration] = useState('');
  const [venue, setVenue] = useState('');
  const [maxPlayers, setMaxPlayers] = useState('12');
  const [cost, setCost] = useState('');
  const [visibility, setVisibility] = useState<EventVisibility>('Public');
  const [skillLevels, setSkillLevels] = useState<SkillLevel[]>([]);

  // Keyboard accessory ID for iOS
  const inputAccessoryViewID = 'eventFormAccessory';

  const nameRef = useRef<TextInput | null>(null);
  const descriptionRef = useRef<TextInput | null>(null);
  const venueRef = useRef<TextInput | null>(null);
  const maxPlayersRef = useRef<TextInput | null>(null);
  const costRef = useRef<TextInput | null>(null);

  // Picker modal states
  const [showOrgPicker, setShowOrgPicker] = useState(false);
  const [showVisibilityPicker, setShowVisibilityPicker] = useState(false);

  // Populate form with initial data (for edit mode)
  useEffect(() => {
    if (initialData) {
      setName(initialData.name || '');
      setDescription(initialData.description || '');
      setSelectedOrgId(initialData.organizationId || null);
      setEventDate(new Date(initialData.eventDate));
      setDuration(String(initialData.duration));
      setVenue(initialData.venue || '');
      setMaxPlayers(String(initialData.maxPlayers));
      setCost(String(initialData.cost));
      setVisibility(initialData.visibility as EventVisibility);
      setSkillLevels((initialData.skillLevels as SkillLevel[]) || []);
    }
  }, [initialData]);

  // Reset visibility when org changes (OrganizationMembers only valid with org)
  useEffect(() => {
    if (!selectedOrgId && visibility === 'OrganizationMembers') {
      setVisibility('Public');
    }
  }, [selectedOrgId, visibility]);

  // Helper to check if org can be shown in visibility picker
  const hasOrganization = mode === 'edit'
    ? !!initialData?.organizationId
    : !!selectedOrgId;

  // Get display names for pickers
  const getOrgDisplayName = () => {
    if (!selectedOrgId) return 'Myself (Pickup Game)';
    const org = organizations.find(o => o.id === selectedOrgId);
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
    if (mode === 'create' && eventDate <= new Date()) {
      Alert.alert('Error', 'Event date must be in the future');
      return false;
    }
    // Duration is optional, but if provided must be valid
    if (duration.trim()) {
      const durationNum = parseInt(duration, 10);
      if (isNaN(durationNum) || durationNum < 15 || durationNum > 480) {
        Alert.alert('Error', 'Duration must be between 15 and 480 minutes');
        return false;
      }
    }
    const maxPlayersNum = parseInt(maxPlayers, 10);
    if (isNaN(maxPlayersNum) || maxPlayersNum < 2 || maxPlayersNum > 100) {
      Alert.alert('Error', 'Max players must be between 2 and 100');
      return false;
    }
    // Cost is required
    const costNum = parseFloat(cost);
    if (cost.trim() === '' || isNaN(costNum) || costNum < 0) {
      Alert.alert('Error', 'Please enter a cost (0 for free events)');
      return false;
    }
    return true;
  };

  const handleSubmit = async () => {
    if (!validateForm()) return;

    const formData: EventFormData = {
      organizationId: selectedOrgId || undefined,
      name: name.trim() || undefined,
      description: description.trim() || undefined,
      eventDate: eventDate.toISOString(),
      duration: duration.trim() ? parseInt(duration, 10) : undefined,
      venue: venue.trim() || undefined,
      maxPlayers: parseInt(maxPlayers, 10),
      cost: parseFloat(cost),
      visibility,
      skillLevels: skillLevels.length > 0 ? skillLevels : undefined,
    };

    await onSubmit(formData);
  };

  return (
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
        {/* Organization Selector (only show in create mode if user owns organizations) */}
        {mode === 'create' && organizations.length > 0 && (
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

        {/* Event Name */}
        <View style={styles.field}>
          <Text style={styles.label}>Event Name (optional)</Text>
          <TextInput
            ref={nameRef}
            style={styles.input}
            value={name}
            onChangeText={setName}
            placeholder="e.g., Friday Night Hockey"
            placeholderTextColor={colors.text.muted}
            returnKeyType="next"
            onSubmitEditing={() => descriptionRef.current?.focus()}
            inputAccessoryViewID={inputAccessoryViewID}
          />
        </View>

        {/* Description */}
        <View style={styles.field}>
          <Text style={styles.label}>Description (optional)</Text>
          <TextInput
            ref={descriptionRef}
            style={[styles.input, styles.textArea]}
            value={description}
            onChangeText={setDescription}
            placeholder="Tell people what to expect..."
            placeholderTextColor={colors.text.muted}
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
            minimumDate={mode === 'create' ? new Date() : undefined}
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
          <Text style={styles.label}>Duration in minutes (optional)</Text>
          <TextInput
            style={styles.input}
            value={duration}
            onChangeText={setDuration}
            placeholder="60"
            placeholderTextColor={colors.text.muted}
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
            placeholder="e.g., Practice Rink"
            placeholderTextColor={colors.text.muted}
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
              placeholderTextColor={colors.text.muted}
              keyboardType="number-pad"
              returnKeyType="next"
              onSubmitEditing={() => costRef.current?.focus()}
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>
          <View style={[styles.field, { flex: 1, marginLeft: 8 }]}>
            <Text style={styles.label}>Cost ($) *</Text>
            <TextInput
              ref={costRef}
              style={styles.input}
              value={cost}
              onChangeText={setCost}
              placeholder="0"
              placeholderTextColor={colors.text.muted}
              keyboardType="decimal-pad"
              returnKeyType="done"
              onSubmitEditing={() => Keyboard.dismiss()}
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>
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
          style={[styles.submitButton, isSubmitting && styles.submitButtonDisabled]}
          onPress={handleSubmit}
          disabled={isSubmitting}
        >
          {isSubmitting ? (
            <ActivityIndicator color={colors.bg.darkest} />
          ) : (
            <Text style={styles.submitButtonText}>
              {mode === 'create' ? 'Create Event' : 'Save Changes'}
            </Text>
          )}
        </TouchableOpacity>

        {/* Optional footer (e.g., danger zone for edit mode) */}
        {footer}

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
                dropdownIconColor={colors.text.primary}
                {...(Platform.OS === 'android' ? { mode: 'dialog' as const } : {})}
                {...(Platform.OS === 'ios' ? { itemStyle: { color: colors.text.primary }, themeVariant: 'dark' as const } : {})}
              >
                <Picker.Item label="Myself (Pickup Game)" value="personal" color={Platform.OS === 'ios' ? colors.text.primary : undefined} />
                {organizations.map((org) => (
                  <Picker.Item key={org.id} label={org.name} value={org.id} color={Platform.OS === 'ios' ? colors.text.primary : undefined} />
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
                dropdownIconColor={colors.text.primary}
                {...(Platform.OS === 'android' ? { mode: 'dialog' as const } : {})}
                {...(Platform.OS === 'ios' ? { itemStyle: { color: colors.text.primary }, themeVariant: 'dark' as const } : {})}
              >
                <Picker.Item label="Public - Anyone can join" value="Public" color={Platform.OS === 'ios' ? colors.text.primary : undefined} />
                {hasOrganization && (
                  <Picker.Item label="Members Only - Organization subscribers" value="OrganizationMembers" color={Platform.OS === 'ios' ? colors.text.primary : undefined} />
                )}
                <Picker.Item label="Invite Only - Private event" value="InviteOnly" color={Platform.OS === 'ios' ? colors.text.primary : undefined} />
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
    backgroundColor: colors.bg.darkest,
    padding: spacing.md,
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
  orgNote: {
    fontSize: 12,
    color: colors.primary.teal,
    marginTop: spacing.sm,
  },
  visibilityNote: {
    fontSize: 12,
    color: colors.status.warning,
    marginTop: spacing.sm,
    fontStyle: 'italic',
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
