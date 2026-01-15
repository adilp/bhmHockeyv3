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
import { Picker } from '@react-native-picker/picker';
import DateTimePicker from '@react-native-community/datetimepicker';
import { useRouter, Stack } from 'expo-router';
import { useTournamentStore } from '../../stores/tournamentStore';
import { useOrganizationStore } from '../../stores/organizationStore';
import type { TournamentFormat, CreateTournamentRequest } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../theme';

// Platform-specific Picker props for dark theme
const pickerProps = Platform.select({
  ios: { itemStyle: { color: colors.text.primary }, themeVariant: 'dark' as const },
  android: { mode: 'dialog' as const },
}) ?? {};

const getPickerItemColor = (): string | undefined =>
  Platform.OS === 'ios' ? colors.text.primary : undefined;

// Tournament format options
const TOURNAMENT_FORMATS: { value: TournamentFormat; label: string; description: string }[] = [
  {
    value: 'SingleElimination',
    label: 'Single Elimination',
    description: 'Lose once and you are out',
  },
  {
    value: 'DoubleElimination',
    label: 'Double Elimination',
    description: 'Must lose twice to be eliminated',
  },
  {
    value: 'RoundRobin',
    label: 'Round Robin',
    description: 'Everyone plays everyone',
  },
];

export default function CreateTournamentScreen() {
  const router = useRouter();
  const { createTournament, isCreating } = useTournamentStore();
  const { myOrganizations, fetchMyOrganizations } = useOrganizationStore();

  // Form state
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [selectedOrgId, setSelectedOrgId] = useState<string | null>(null);
  const [format, setFormat] = useState<TournamentFormat>('SingleElimination');
  const [startDate, setStartDate] = useState(() => {
    const date = new Date();
    date.setDate(date.getDate() + 14); // Default to 2 weeks from now
    return date;
  });
  const [endDate, setEndDate] = useState(() => {
    const date = new Date();
    date.setDate(date.getDate() + 15); // Default to 1 day after start
    return date;
  });
  const [registrationDeadline, setRegistrationDeadline] = useState(() => {
    const date = new Date();
    date.setDate(date.getDate() + 7); // Default to 1 week from now
    return date;
  });
  const [maxTeams, setMaxTeams] = useState('8');
  const [entryFee, setEntryFee] = useState('0');
  const [venue, setVenue] = useState('');

  // Date picker visibility states
  const [showStartDatePicker, setShowStartDatePicker] = useState(false);
  const [showEndDatePicker, setShowEndDatePicker] = useState(false);
  const [showRegistrationDeadlinePicker, setShowRegistrationDeadlinePicker] = useState(false);

  // Picker modal states
  const [showOrgPicker, setShowOrgPicker] = useState(false);
  const [showFormatPicker, setShowFormatPicker] = useState(false);

  // Keyboard accessory ID for iOS
  const inputAccessoryViewID = 'tournamentFormAccessory';

  // Fetch user's organizations on mount
  useEffect(() => {
    fetchMyOrganizations();
  }, []);

  // Auto-select first organization if available
  useEffect(() => {
    if (myOrganizations.length > 0 && !selectedOrgId) {
      setSelectedOrgId(myOrganizations[0].id);
    }
  }, [myOrganizations, selectedOrgId]);

  // Update end date when start date changes (ensure end >= start)
  useEffect(() => {
    if (endDate < startDate) {
      const newEndDate = new Date(startDate);
      newEndDate.setDate(newEndDate.getDate() + 1);
      setEndDate(newEndDate);
    }
  }, [startDate, endDate]);

  // Update registration deadline when it passes start date
  useEffect(() => {
    if (registrationDeadline >= startDate) {
      const newDeadline = new Date(startDate);
      newDeadline.setDate(newDeadline.getDate() - 1);
      setRegistrationDeadline(newDeadline);
    }
  }, [startDate, registrationDeadline]);

  // Helper functions
  const getOrgDisplayName = () => {
    if (!selectedOrgId) return 'Select Organization';
    const org = myOrganizations.find(o => o.id === selectedOrgId);
    return org?.name || 'Unknown';
  };

  const getFormatDisplayName = () => {
    const selectedFormat = TOURNAMENT_FORMATS.find(f => f.value === format);
    return selectedFormat?.label || format;
  };

  const formatDate = (date: Date) => {
    return date.toLocaleDateString('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  };

  // Date change handlers
  const handleStartDateChange = (_: any, selectedDate?: Date) => {
    setShowStartDatePicker(false);
    if (selectedDate) {
      setStartDate(selectedDate);
    }
  };

  const handleEndDateChange = (_: any, selectedDate?: Date) => {
    setShowEndDatePicker(false);
    if (selectedDate) {
      setEndDate(selectedDate);
    }
  };

  const handleRegistrationDeadlineChange = (_: any, selectedDate?: Date) => {
    setShowRegistrationDeadlinePicker(false);
    if (selectedDate) {
      setRegistrationDeadline(selectedDate);
    }
  };

  // Validation
  const validateForm = (): boolean => {
    if (!name.trim()) {
      Alert.alert('Error', 'Tournament name is required');
      return false;
    }

    if (startDate <= new Date()) {
      Alert.alert('Error', 'Start date must be in the future');
      return false;
    }

    if (endDate < startDate) {
      Alert.alert('Error', 'End date must be on or after start date');
      return false;
    }

    if (registrationDeadline >= startDate) {
      Alert.alert('Error', 'Registration deadline must be before start date');
      return false;
    }

    const maxTeamsNum = parseInt(maxTeams, 10);
    if (isNaN(maxTeamsNum) || maxTeamsNum < 2 || maxTeamsNum > 64) {
      Alert.alert('Error', 'Max teams must be between 2 and 64');
      return false;
    }

    const entryFeeNum = parseFloat(entryFee);
    if (isNaN(entryFeeNum) || entryFeeNum < 0) {
      Alert.alert('Error', 'Entry fee must be 0 or greater');
      return false;
    }

    return true;
  };

  // Submit handler
  const handleCreate = async () => {
    if (!validateForm()) return;

    const request: CreateTournamentRequest = {
      organizationId: selectedOrgId || undefined,
      name: name.trim(),
      description: description.trim() || undefined,
      format,
      teamFormation: 'OrganizerAssigned', // Default for MVP
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString(),
      registrationDeadline: registrationDeadline.toISOString(),
      maxTeams: parseInt(maxTeams, 10),
      entryFee: parseFloat(entryFee) || 0,
      venue: venue.trim() || undefined,
    };

    const tournament = await createTournament(request);
    if (tournament) {
      Alert.alert('Success', 'Tournament created successfully!', [
        { text: 'OK', onPress: () => router.replace(`/tournaments/${tournament.id}`) }
      ]);
    }
  };

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Create Tournament',
          headerBackTitle: 'Cancel',
          headerStyle: { backgroundColor: colors.bg.darkest },
          headerTintColor: colors.primary.teal,
          headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
        }}
      />

      <KeyboardAvoidingView
        style={styles.keyboardView}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        keyboardVerticalOffset={Platform.OS === 'ios' ? 80 : 0}
      >
        <ScrollView
          style={styles.container}
          keyboardShouldPersistTaps="handled"
          keyboardDismissMode={Platform.OS === 'ios' ? 'interactive' : 'on-drag'}
          contentInsetAdjustmentBehavior="automatic"
        >
          {/* Tournament Name */}
          <View style={styles.field}>
            <Text style={styles.label}>Tournament Name *</Text>
            <TextInput
              style={styles.input}
              value={name}
              onChangeText={setName}
              placeholder="e.g., Spring Championship 2025"
              placeholderTextColor={colors.text.muted}
              returnKeyType="next"
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>

          {/* Organization Selector */}
          {myOrganizations.length > 0 && (
            <View style={styles.field}>
              <Text style={styles.label}>Organization (optional)</Text>
              <TouchableOpacity
                style={styles.pickerButton}
                onPress={() => setShowOrgPicker(true)}
              >
                <Text style={styles.pickerButtonText}>{getOrgDisplayName()}</Text>
                <Text style={styles.pickerArrow}>&#9660;</Text>
              </TouchableOpacity>
            </View>
          )}

          {/* Format Selector */}
          <View style={styles.field}>
            <Text style={styles.label}>Format *</Text>
            <TouchableOpacity
              style={styles.pickerButton}
              onPress={() => setShowFormatPicker(true)}
            >
              <Text style={styles.pickerButtonText}>{getFormatDisplayName()}</Text>
              <Text style={styles.pickerArrow}>&#9660;</Text>
            </TouchableOpacity>

            {/* Format chips for quick selection */}
            <View style={styles.formatChipsContainer}>
              {TOURNAMENT_FORMATS.map((f) => (
                <TouchableOpacity
                  key={f.value}
                  style={[
                    styles.formatChip,
                    format === f.value && styles.formatChipSelected,
                  ]}
                  onPress={() => setFormat(f.value)}
                >
                  <Text
                    style={[
                      styles.formatChipText,
                      format === f.value && styles.formatChipTextSelected,
                    ]}
                  >
                    {f.label}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
            <Text style={styles.formatDescription}>
              {TOURNAMENT_FORMATS.find(f => f.value === format)?.description}
            </Text>
          </View>

          {/* Start Date */}
          <View style={styles.field}>
            <Text style={styles.label}>Start Date *</Text>
            <TouchableOpacity
              style={styles.pickerButton}
              onPress={() => setShowStartDatePicker(true)}
            >
              <Text style={styles.pickerButtonText}>{formatDate(startDate)}</Text>
            </TouchableOpacity>
          </View>

          {showStartDatePicker && (
            <DateTimePicker
              value={startDate}
              mode="date"
              display={Platform.OS === 'ios' ? 'spinner' : 'default'}
              onChange={handleStartDateChange}
              minimumDate={new Date()}
            />
          )}

          {/* End Date */}
          <View style={styles.field}>
            <Text style={styles.label}>End Date *</Text>
            <TouchableOpacity
              style={styles.pickerButton}
              onPress={() => setShowEndDatePicker(true)}
            >
              <Text style={styles.pickerButtonText}>{formatDate(endDate)}</Text>
            </TouchableOpacity>
          </View>

          {showEndDatePicker && (
            <DateTimePicker
              value={endDate}
              mode="date"
              display={Platform.OS === 'ios' ? 'spinner' : 'default'}
              onChange={handleEndDateChange}
              minimumDate={startDate}
            />
          )}

          {/* Registration Deadline */}
          <View style={styles.field}>
            <Text style={styles.label}>Registration Deadline *</Text>
            <TouchableOpacity
              style={styles.pickerButton}
              onPress={() => setShowRegistrationDeadlinePicker(true)}
            >
              <Text style={styles.pickerButtonText}>{formatDate(registrationDeadline)}</Text>
            </TouchableOpacity>
            <Text style={styles.fieldNote}>
              Teams must register before this date
            </Text>
          </View>

          {showRegistrationDeadlinePicker && (
            <DateTimePicker
              value={registrationDeadline}
              mode="date"
              display={Platform.OS === 'ios' ? 'spinner' : 'default'}
              onChange={handleRegistrationDeadlineChange}
              minimumDate={new Date()}
              maximumDate={new Date(startDate.getTime() - 24 * 60 * 60 * 1000)} // Day before start
            />
          )}

          {/* Max Teams & Entry Fee */}
          <View style={styles.row}>
            <View style={[styles.field, { flex: 1, marginRight: spacing.sm }]}>
              <Text style={styles.label}>Max Teams *</Text>
              <TextInput
                style={styles.input}
                value={maxTeams}
                onChangeText={setMaxTeams}
                placeholder="8"
                placeholderTextColor={colors.text.muted}
                keyboardType="number-pad"
                returnKeyType="next"
                inputAccessoryViewID={inputAccessoryViewID}
              />
            </View>
            <View style={[styles.field, { flex: 1, marginLeft: spacing.sm }]}>
              <Text style={styles.label}>Entry Fee ($)</Text>
              <TextInput
                style={styles.input}
                value={entryFee}
                onChangeText={setEntryFee}
                placeholder="0"
                placeholderTextColor={colors.text.muted}
                keyboardType="decimal-pad"
                returnKeyType="next"
                inputAccessoryViewID={inputAccessoryViewID}
              />
            </View>
          </View>

          {/* Venue */}
          <View style={styles.field}>
            <Text style={styles.label}>Venue (optional)</Text>
            <TextInput
              style={styles.input}
              value={venue}
              onChangeText={setVenue}
              placeholder="e.g., Main Arena"
              placeholderTextColor={colors.text.muted}
              returnKeyType="next"
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>

          {/* Description */}
          <View style={styles.field}>
            <Text style={styles.label}>Description (optional)</Text>
            <TextInput
              style={[styles.input, styles.textArea]}
              value={description}
              onChangeText={setDescription}
              placeholder="Tell teams what to expect..."
              placeholderTextColor={colors.text.muted}
              multiline
              numberOfLines={4}
              returnKeyType="done"
              blurOnSubmit
              inputAccessoryViewID={inputAccessoryViewID}
            />
          </View>

          {/* Create Button */}
          <TouchableOpacity
            style={[styles.submitButton, isCreating && styles.submitButtonDisabled]}
            onPress={handleCreate}
            disabled={isCreating}
          >
            {isCreating ? (
              <ActivityIndicator color={colors.bg.darkest} />
            ) : (
              <Text style={styles.submitButtonText}>Create Tournament</Text>
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
                  <Text style={styles.modalTitle}>Select Organization</Text>
                  <TouchableOpacity onPress={() => setShowOrgPicker(false)}>
                    <Text style={styles.modalDone}>Done</Text>
                  </TouchableOpacity>
                </View>
                <Picker
                  selectedValue={selectedOrgId ?? 'none'}
                  onValueChange={(value) => setSelectedOrgId(value === 'none' ? null : value)}
                  style={styles.modalPicker}
                  dropdownIconColor={colors.text.primary}
                  {...pickerProps}
                >
                  <Picker.Item label="No Organization" value="none" color={getPickerItemColor()} />
                  {myOrganizations.map((org) => (
                    <Picker.Item key={org.id} label={org.name} value={org.id} color={getPickerItemColor()} />
                  ))}
                </Picker>
              </View>
            </View>
          </Modal>

          {/* Format Picker Modal */}
          <Modal
            visible={showFormatPicker}
            transparent
            animationType="slide"
          >
            <View style={styles.modalOverlay}>
              <View style={styles.modalContent}>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>Tournament Format</Text>
                  <TouchableOpacity onPress={() => setShowFormatPicker(false)}>
                    <Text style={styles.modalDone}>Done</Text>
                  </TouchableOpacity>
                </View>
                <Picker
                  selectedValue={format}
                  onValueChange={setFormat}
                  style={styles.modalPicker}
                  dropdownIconColor={colors.text.primary}
                  {...pickerProps}
                >
                  {TOURNAMENT_FORMATS.map((f) => (
                    <Picker.Item key={f.value} label={f.label} value={f.value} color={getPickerItemColor()} />
                  ))}
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
  keyboardView: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
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
    height: 100,
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
  fieldNote: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: spacing.xs,
  },
  formatChipsContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    marginTop: spacing.sm,
    gap: spacing.sm,
  },
  formatChip: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderRadius: radius.round,
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  formatChipSelected: {
    backgroundColor: colors.subtle.teal,
    borderColor: colors.primary.teal,
  },
  formatChipText: {
    fontSize: 13,
    fontWeight: '500',
    color: colors.text.secondary,
  },
  formatChipTextSelected: {
    color: colors.primary.teal,
  },
  formatDescription: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: spacing.sm,
    fontStyle: 'italic',
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
