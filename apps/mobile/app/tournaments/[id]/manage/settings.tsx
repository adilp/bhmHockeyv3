import { useState, useEffect, useCallback } from 'react';
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
  KeyboardAvoidingView,
  Keyboard,
  InputAccessoryView,
  Modal,
} from 'react-native';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import DateTimePicker from '@react-native-community/datetimepicker';
import { Picker } from '@react-native-picker/picker';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { tournamentService } from '@bhmhockey/api-client';
import { colors, spacing, radius } from '../../../../theme';
import type { TournamentStatus, TournamentFormat, UpdateTournamentRequest } from '@bhmhockey/shared';

// Platform-specific Picker props for dark theme
const pickerProps = Platform.select({
  ios: { itemStyle: { color: colors.text.primary }, themeVariant: 'dark' as const },
  android: { mode: 'dialog' as const },
}) ?? {};

const getPickerItemColor = (): string | undefined =>
  Platform.OS === 'ios' ? colors.text.primary : undefined;

// Tournament format options
const TOURNAMENT_FORMATS: { value: TournamentFormat; label: string }[] = [
  { value: 'SingleElimination', label: 'Single Elimination' },
  { value: 'DoubleElimination', label: 'Double Elimination' },
  { value: 'RoundRobin', label: 'Round Robin' },
];

export default function TournamentSettingsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { currentTournament, fetchTournamentById } = useTournamentStore();

  // Form state
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [venue, setVenue] = useState('');
  const [startDate, setStartDate] = useState(new Date());
  const [endDate, setEndDate] = useState(new Date());
  const [registrationDeadline, setRegistrationDeadline] = useState(new Date());
  const [maxTeams, setMaxTeams] = useState('');
  const [entryFee, setEntryFee] = useState('');
  const [minPlayersPerTeam, setMinPlayersPerTeam] = useState('');
  const [maxPlayersPerTeam, setMaxPlayersPerTeam] = useState('');
  const [waiverUrl, setWaiverUrl] = useState('');
  const [eligibilityRequirements, setEligibilityRequirements] = useState('');
  const [format, setFormat] = useState<TournamentFormat>('SingleElimination');

  // UI state
  const [isSaving, setIsSaving] = useState(false);
  const [showStartDatePicker, setShowStartDatePicker] = useState(false);
  const [showEndDatePicker, setShowEndDatePicker] = useState(false);
  const [showRegistrationDeadlinePicker, setShowRegistrationDeadlinePicker] = useState(false);
  const [showFormatPicker, setShowFormatPicker] = useState(false);

  // Keyboard accessory ID for iOS
  const inputAccessoryViewID = 'settingsFormAccessory';

  // Fetch tournament if not already loaded
  useEffect(() => {
    if (id && (!currentTournament || currentTournament.id !== id)) {
      fetchTournamentById(id);
    }
  }, [id, currentTournament]);

  // Populate form when tournament loads
  useEffect(() => {
    if (currentTournament) {
      setName(currentTournament.name);
      setDescription(currentTournament.description || '');
      setVenue(currentTournament.venue || '');
      setStartDate(new Date(currentTournament.startDate));
      setEndDate(new Date(currentTournament.endDate));
      setRegistrationDeadline(new Date(currentTournament.registrationDeadline));
      setMaxTeams(currentTournament.maxTeams.toString());
      setEntryFee(currentTournament.entryFee.toString());
      setMinPlayersPerTeam(currentTournament.minPlayersPerTeam?.toString() || '');
      setMaxPlayersPerTeam(currentTournament.maxPlayersPerTeam?.toString() || '');
      setWaiverUrl(currentTournament.waiverUrl || '');
      setEligibilityRequirements(currentTournament.eligibilityRequirements || '');
      setFormat(currentTournament.format);
    }
  }, [currentTournament]);

  // Check if field is locked
  const isLocked = (field: string): boolean => {
    const lockedStatuses: TournamentStatus[] = ['InProgress', 'Completed', 'Cancelled'];
    if (!currentTournament || !lockedStatuses.includes(currentTournament.status)) {
      return false;
    }
    const lockedFields = ['format', 'maxTeams', 'teamFormation'];
    return lockedFields.includes(field);
  };

  // Format date for display
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

  // Get format display name
  const getFormatDisplayName = () => {
    const selectedFormat = TOURNAMENT_FORMATS.find(f => f.value === format);
    return selectedFormat?.label || format;
  };

  // Validation
  const validateForm = (): boolean => {
    if (!name.trim()) {
      Alert.alert('Error', 'Tournament name is required');
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

    if (minPlayersPerTeam.trim()) {
      const minPlayers = parseInt(minPlayersPerTeam, 10);
      if (isNaN(minPlayers) || minPlayers < 1) {
        Alert.alert('Error', 'Min players per team must be 1 or greater');
        return false;
      }
    }

    if (maxPlayersPerTeam.trim()) {
      const maxPlayers = parseInt(maxPlayersPerTeam, 10);
      if (isNaN(maxPlayers) || maxPlayers < 1) {
        Alert.alert('Error', 'Max players per team must be 1 or greater');
        return false;
      }

      // Validate min <= max
      if (minPlayersPerTeam.trim()) {
        const minPlayers = parseInt(minPlayersPerTeam, 10);
        if (maxPlayers < minPlayers) {
          Alert.alert('Error', 'Max players per team must be greater than or equal to min players');
          return false;
        }
      }
    }

    return true;
  };

  // Save handler
  const handleSave = async () => {
    if (!id || !validateForm()) return;

    setIsSaving(true);
    try {
      const request: UpdateTournamentRequest = {
        name: name.trim(),
        description: description.trim() || undefined,
        venue: venue.trim() || undefined,
        startDate: startDate.toISOString(),
        endDate: endDate.toISOString(),
        registrationDeadline: registrationDeadline.toISOString(),
        entryFee: parseFloat(entryFee),
        waiverUrl: waiverUrl.trim() || undefined,
        eligibilityRequirements: eligibilityRequirements.trim() || undefined,
      };

      // Only include locked fields if they're not locked
      if (!isLocked('format')) {
        request.format = format;
      }
      if (!isLocked('maxTeams')) {
        request.maxTeams = parseInt(maxTeams, 10);
      }

      // Include player limits if specified
      if (minPlayersPerTeam.trim()) {
        request.minPlayersPerTeam = parseInt(minPlayersPerTeam, 10);
      }
      if (maxPlayersPerTeam.trim()) {
        request.maxPlayersPerTeam = parseInt(maxPlayersPerTeam, 10);
      }

      await tournamentService.update(id, request);

      // Refresh tournament data
      await fetchTournamentById(id);

      Alert.alert('Success', 'Tournament settings updated successfully!', [
        { text: 'OK', onPress: () => router.back() }
      ]);
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to update tournament settings';
      Alert.alert('Error', errorMessage);
    } finally {
      setIsSaving(false);
    }
  };

  // Loading state
  if (!currentTournament) {
    return (
      <View style={styles.loadingContainer}>
        <Stack.Screen
          options={{
            title: 'Tournament Settings',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.darkest },
            headerTintColor: colors.primary.teal,
            headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
          }}
        />
        <ActivityIndicator size="large" color={colors.primary.teal} />
        <Text style={styles.loadingText} allowFontScaling={false}>Loading tournament...</Text>
      </View>
    );
  }

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Tournament Settings',
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.darkest },
          headerTintColor: colors.primary.teal,
          headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
          headerRight: () => (
            <TouchableOpacity
              onPress={handleSave}
              disabled={isSaving}
              style={styles.headerButton}
            >
              {isSaving ? (
                <ActivityIndicator size="small" color={colors.primary.teal} />
              ) : (
                <Text style={styles.headerButtonText} allowFontScaling={false}>Save</Text>
              )}
            </TouchableOpacity>
          ),
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
          {/* Basic Info Section */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle} allowFontScaling={false}>BASIC INFO</Text>

            {/* Tournament Name */}
            <View style={styles.field}>
              <Text style={styles.label} allowFontScaling={false}>Tournament Name *</Text>
              <TextInput
                style={styles.input}
                value={name}
                onChangeText={setName}
                placeholder="e.g., Spring Championship 2025"
                placeholderTextColor={colors.text.muted}
                returnKeyType="next"
                inputAccessoryViewID={inputAccessoryViewID}
                allowFontScaling={false}
              />
            </View>

            {/* Description */}
            <View style={styles.field}>
              <Text style={styles.label} allowFontScaling={false}>Description</Text>
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
                allowFontScaling={false}
              />
            </View>

            {/* Venue */}
            <View style={styles.field}>
              <Text style={styles.label} allowFontScaling={false}>Venue</Text>
              <TextInput
                style={styles.input}
                value={venue}
                onChangeText={setVenue}
                placeholder="e.g., Main Arena"
                placeholderTextColor={colors.text.muted}
                returnKeyType="next"
                inputAccessoryViewID={inputAccessoryViewID}
                allowFontScaling={false}
              />
            </View>
          </View>

          {/* Schedule Section */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle} allowFontScaling={false}>SCHEDULE</Text>

            {/* Start Date */}
            <View style={styles.field}>
              <Text style={styles.label} allowFontScaling={false}>Start Date *</Text>
              <TouchableOpacity
                style={styles.pickerButton}
                onPress={() => setShowStartDatePicker(true)}
              >
                <Text style={styles.pickerButtonText} allowFontScaling={false}>
                  {formatDate(startDate)}
                </Text>
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
              <Text style={styles.label} allowFontScaling={false}>End Date *</Text>
              <TouchableOpacity
                style={styles.pickerButton}
                onPress={() => setShowEndDatePicker(true)}
              >
                <Text style={styles.pickerButtonText} allowFontScaling={false}>
                  {formatDate(endDate)}
                </Text>
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
              <Text style={styles.label} allowFontScaling={false}>Registration Deadline *</Text>
              <TouchableOpacity
                style={styles.pickerButton}
                onPress={() => setShowRegistrationDeadlinePicker(true)}
              >
                <Text style={styles.pickerButtonText} allowFontScaling={false}>
                  {formatDate(registrationDeadline)}
                </Text>
              </TouchableOpacity>
              <Text style={styles.fieldNote} allowFontScaling={false}>
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
                maximumDate={new Date(startDate.getTime() - 24 * 60 * 60 * 1000)}
              />
            )}
          </View>

          {/* Team Configuration Section */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle} allowFontScaling={false}>TEAM CONFIGURATION</Text>

            {/* Format */}
            <View style={styles.field}>
              <Text style={styles.label} allowFontScaling={false}>
                Format *
                {isLocked('format') && ' (Locked)'}
              </Text>
              {isLocked('format') ? (
                <View style={styles.lockedField}>
                  <Text style={styles.lockedFieldText} allowFontScaling={false}>
                    {getFormatDisplayName()}
                  </Text>
                  <LockedFieldIndicator />
                </View>
              ) : (
                <TouchableOpacity
                  style={styles.pickerButton}
                  onPress={() => setShowFormatPicker(true)}
                >
                  <Text style={styles.pickerButtonText} allowFontScaling={false}>
                    {getFormatDisplayName()}
                  </Text>
                  <Text style={styles.pickerArrow}>&#9660;</Text>
                </TouchableOpacity>
              )}
            </View>

            {/* Max Teams */}
            <View style={styles.field}>
              <Text style={styles.label} allowFontScaling={false}>
                Max Teams *
                {isLocked('maxTeams') && ' (Locked)'}
              </Text>
              {isLocked('maxTeams') ? (
                <View style={styles.lockedField}>
                  <Text style={styles.lockedFieldText} allowFontScaling={false}>
                    {maxTeams}
                  </Text>
                  <LockedFieldIndicator />
                </View>
              ) : (
                <TextInput
                  style={styles.input}
                  value={maxTeams}
                  onChangeText={setMaxTeams}
                  placeholder="8"
                  placeholderTextColor={colors.text.muted}
                  keyboardType="number-pad"
                  returnKeyType="next"
                  inputAccessoryViewID={inputAccessoryViewID}
                  allowFontScaling={false}
                />
              )}
            </View>

            {/* Min/Max Players Per Team */}
            <View style={styles.row}>
              <View style={[styles.field, { flex: 1, marginRight: spacing.sm }]}>
                <Text style={styles.label} allowFontScaling={false}>Min Players/Team</Text>
                <TextInput
                  style={styles.input}
                  value={minPlayersPerTeam}
                  onChangeText={setMinPlayersPerTeam}
                  placeholder="Optional"
                  placeholderTextColor={colors.text.muted}
                  keyboardType="number-pad"
                  returnKeyType="next"
                  inputAccessoryViewID={inputAccessoryViewID}
                  allowFontScaling={false}
                />
              </View>
              <View style={[styles.field, { flex: 1, marginLeft: spacing.sm }]}>
                <Text style={styles.label} allowFontScaling={false}>Max Players/Team</Text>
                <TextInput
                  style={styles.input}
                  value={maxPlayersPerTeam}
                  onChangeText={setMaxPlayersPerTeam}
                  placeholder="Optional"
                  placeholderTextColor={colors.text.muted}
                  keyboardType="number-pad"
                  returnKeyType="next"
                  inputAccessoryViewID={inputAccessoryViewID}
                  allowFontScaling={false}
                />
              </View>
            </View>
          </View>

          {/* Payment Section */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle} allowFontScaling={false}>PAYMENT</Text>

            {/* Entry Fee */}
            <View style={styles.field}>
              <Text style={styles.label} allowFontScaling={false}>Entry Fee ($)</Text>
              <TextInput
                style={styles.input}
                value={entryFee}
                onChangeText={setEntryFee}
                placeholder="0"
                placeholderTextColor={colors.text.muted}
                keyboardType="decimal-pad"
                returnKeyType="next"
                inputAccessoryViewID={inputAccessoryViewID}
                allowFontScaling={false}
              />
            </View>
          </View>

          {/* Additional Info Section */}
          <View style={styles.section}>
            <Text style={styles.sectionTitle} allowFontScaling={false}>ADDITIONAL INFO</Text>

            {/* Waiver URL */}
            <View style={styles.field}>
              <Text style={styles.label} allowFontScaling={false}>Waiver URL</Text>
              <TextInput
                style={styles.input}
                value={waiverUrl}
                onChangeText={setWaiverUrl}
                placeholder="https://example.com/waiver.pdf"
                placeholderTextColor={colors.text.muted}
                keyboardType="url"
                autoCapitalize="none"
                returnKeyType="next"
                inputAccessoryViewID={inputAccessoryViewID}
                allowFontScaling={false}
              />
            </View>

            {/* Eligibility Requirements */}
            <View style={styles.field}>
              <Text style={styles.label} allowFontScaling={false}>Eligibility Requirements</Text>
              <TextInput
                style={[styles.input, styles.textArea]}
                value={eligibilityRequirements}
                onChangeText={setEligibilityRequirements}
                placeholder="Players must be 18+ and have valid USA Hockey registration..."
                placeholderTextColor={colors.text.muted}
                multiline
                numberOfLines={4}
                returnKeyType="done"
                blurOnSubmit
                inputAccessoryViewID={inputAccessoryViewID}
                allowFontScaling={false}
              />
            </View>
          </View>

          {/* Bottom padding */}
          <View style={{ height: 40 }} />
        </ScrollView>
      </KeyboardAvoidingView>

      {/* Format Picker Modal */}
      <Modal
        visible={showFormatPicker}
        transparent
        animationType="slide"
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle} allowFontScaling={false}>Tournament Format</Text>
              <TouchableOpacity onPress={() => setShowFormatPicker(false)}>
                <Text style={styles.modalDone} allowFontScaling={false}>Done</Text>
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
              <Text style={styles.modalDone} allowFontScaling={false}>Done</Text>
            </TouchableOpacity>
          </View>
        </InputAccessoryView>
      )}
    </>
  );
}

// Locked field indicator component
const LockedFieldIndicator = () => (
  <View style={styles.lockedIndicator}>
    <Ionicons name="lock-closed" size={14} color={colors.text.muted} />
    <Text style={styles.lockedText} allowFontScaling={false}>Locked after start</Text>
  </View>
);

const styles = StyleSheet.create({
  keyboardView: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
  loadingText: {
    marginTop: spacing.sm,
    fontSize: 16,
    color: colors.text.muted,
  },

  // Section
  section: {
    padding: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  sectionTitle: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.md,
  },

  // Header button
  headerButton: {
    marginRight: spacing.sm,
  },
  headerButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.primary.teal,
  },

  // Form fields
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

  // Locked fields
  lockedField: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    opacity: 0.6,
  },
  lockedFieldText: {
    fontSize: 15,
    color: colors.text.secondary,
  },
  lockedIndicator: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: spacing.xs,
    gap: spacing.xs,
  },
  lockedText: {
    fontSize: 11,
    color: colors.text.muted,
    fontStyle: 'italic',
  },

  // Modal
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

  // iOS keyboard accessory
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
