import { useState, useEffect, useCallback } from 'react';
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
  KeyboardAvoidingView,
  Keyboard,
  InputAccessoryView,
  Switch,
  Modal,
} from 'react-native';
import { Picker } from '@react-native-picker/picker';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { useTournamentTeamStore } from '../../../../stores/tournamentTeamStore';
import { colors, spacing, radius } from '../../../../theme';

// Custom question types
interface CustomQuestion {
  id: string;
  type: 'text' | 'yesno' | 'dropdown';
  label: string;
  required: boolean;
  options?: string[];
}

// Platform-specific Picker props for dark theme
const pickerProps = Platform.select({
  ios: { itemStyle: { color: colors.text.primary }, themeVariant: 'dark' as const },
  android: { mode: 'dialog' as const },
}) ?? {};

const getPickerItemColor = (): string | undefined =>
  Platform.OS === 'ios' ? colors.text.primary : undefined;

export default function CreateTeamScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { currentTournament, fetchTournamentById } = useTournamentStore();
  const { createTeam, isProcessing } = useTournamentTeamStore();

  // Form state
  const [teamName, setTeamName] = useState('');
  const [position, setPosition] = useState<string>('Skater');
  const [customResponses, setCustomResponses] = useState<Record<string, any>>({});
  const [showPositionPicker, setShowPositionPicker] = useState(false);
  const [showDropdownPicker, setShowDropdownPicker] = useState<string | null>(null);

  // Keyboard accessory ID for iOS
  const inputAccessoryViewID = 'createTeamFormAccessory';

  // Fetch tournament if not already loaded
  useEffect(() => {
    if (id && (!currentTournament || currentTournament.id !== id)) {
      fetchTournamentById(id);
    }
  }, [id, currentTournament]);

  // Parse custom questions from JSON string
  const customQuestions: CustomQuestion[] = (() => {
    if (!currentTournament?.customQuestions) return [];
    try {
      return JSON.parse(currentTournament.customQuestions);
    } catch (e) {
      console.error('Failed to parse custom questions:', e);
      return [];
    }
  })();

  // Initialize custom responses with defaults
  useEffect(() => {
    if (customQuestions.length > 0) {
      const initial: Record<string, any> = {};
      customQuestions.forEach((q) => {
        if (q.type === 'yesno') {
          initial[q.id] = false;
        } else if (q.type === 'dropdown' && q.options && q.options.length > 0) {
          initial[q.id] = q.options[0];
        } else {
          initial[q.id] = '';
        }
      });
      setCustomResponses(initial);
    }
  }, [customQuestions.length]);

  // Form validation
  const validateForm = (): boolean => {
    // Validate team name
    if (!teamName.trim() || teamName.trim().length < 2) {
      Alert.alert('Invalid Team Name', 'Team name must be at least 2 characters long.');
      return false;
    }

    // Validate required custom questions
    for (const question of customQuestions) {
      if (question.required) {
        const response = customResponses[question.id];
        if (
          response === undefined ||
          response === null ||
          (typeof response === 'string' && response.trim() === '')
        ) {
          Alert.alert('Required Field', `Please answer: ${question.label}`);
          return false;
        }
      }
    }
    return true;
  };

  // Handle create team button
  const handleCreateTeam = async () => {
    if (!validateForm() || !id) return;

    // Note: In the current backend implementation, createTeam only accepts team name.
    // Captain registration will need to be handled separately or the backend needs to be updated
    // to accept captain registration data (position, customResponses) in the createTeam request.

    // For now, we'll just create the team with the name.
    // TODO: After team creation, automatically register the captain with position and custom responses
    const newTeam = await createTeam(id, teamName.trim());

    if (newTeam) {
      Alert.alert(
        'Team Created',
        `${teamName} has been created successfully! You are now the team captain.`,
        [
          {
            text: 'OK',
            onPress: () => {
              // Navigate to team detail screen (when it exists)
              // For now, navigate back to teams list
              router.back();
            },
          },
        ]
      );
    }
  };

  // Render custom question input
  const renderQuestionInput = (question: CustomQuestion) => {
    const value = customResponses[question.id];

    switch (question.type) {
      case 'text':
        return (
          <TextInput
            style={styles.input}
            value={value || ''}
            onChangeText={(text) => setCustomResponses({ ...customResponses, [question.id]: text })}
            placeholder={question.required ? 'Required' : 'Optional'}
            placeholderTextColor={colors.text.muted}
            returnKeyType="next"
            inputAccessoryViewID={inputAccessoryViewID}
          />
        );

      case 'yesno':
        return (
          <View style={styles.switchRow}>
            <Text style={styles.switchLabel}>{value ? 'Yes' : 'No'}</Text>
            <Switch
              value={value || false}
              onValueChange={(val) => setCustomResponses({ ...customResponses, [question.id]: val })}
              trackColor={{ false: colors.border.muted, true: colors.primary.teal }}
              thumbColor={Platform.OS === 'ios' ? undefined : colors.bg.elevated}
            />
          </View>
        );

      case 'dropdown':
        return (
          <TouchableOpacity
            style={styles.pickerButton}
            onPress={() => setShowDropdownPicker(question.id)}
          >
            <Text style={styles.pickerButtonText}>{value || 'Select an option'}</Text>
            <Text style={styles.pickerArrow}>&#9660;</Text>
          </TouchableOpacity>
        );

      default:
        return null;
    }
  };

  // Loading state
  if (!currentTournament) {
    return (
      <View style={styles.loadingContainer}>
        <Stack.Screen
          options={{
            title: 'Create Team',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.darkest },
            headerTintColor: colors.primary.teal,
            headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
          }}
        />
        <ActivityIndicator size="large" color={colors.primary.teal} />
        <Text style={styles.loadingText}>Loading tournament...</Text>
      </View>
    );
  }

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Create Team',
          headerBackTitle: 'Cancel',
          headerStyle: { backgroundColor: colors.bg.darkest },
          headerTintColor: colors.primary.teal,
          headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
        }}
      />

      <KeyboardAvoidingView
        style={styles.keyboardView}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        keyboardVerticalOffset={Platform.OS === 'ios' ? 90 : 0}
      >
        <ScrollView
          style={styles.container}
          contentContainerStyle={styles.scrollContent}
          keyboardShouldPersistTaps="handled"
          keyboardDismissMode={Platform.OS === 'ios' ? 'interactive' : 'on-drag'}
        >
          <Text style={styles.pageTitle}>Create Your Team</Text>
          <Text style={styles.pageDescription}>
            Enter a team name and complete your registration as team captain.
          </Text>

          {/* Team Name */}
          <View style={styles.field}>
            <Text style={styles.label}>Team Name *</Text>
            <TextInput
              style={styles.input}
              value={teamName}
              onChangeText={setTeamName}
              placeholder="Enter team name"
              placeholderTextColor={colors.text.muted}
              returnKeyType="next"
              inputAccessoryViewID={inputAccessoryViewID}
              autoFocus
            />
          </View>

          {/* Divider */}
          <View style={styles.divider} />
          <Text style={styles.sectionTitle}>Captain Registration</Text>

          {/* Position Selector */}
          <View style={styles.field}>
            <Text style={styles.label}>Position *</Text>
            <TouchableOpacity
              style={styles.pickerButton}
              onPress={() => setShowPositionPicker(true)}
            >
              <Text style={styles.pickerButtonText}>{position}</Text>
              <Text style={styles.pickerArrow}>&#9660;</Text>
            </TouchableOpacity>
          </View>

          {/* Custom Questions */}
          {customQuestions.map((question) => (
            <View key={question.id} style={styles.field}>
              <Text style={styles.label}>
                {question.label} {question.required && '*'}
              </Text>
              {renderQuestionInput(question)}
            </View>
          ))}

          {/* Create Team Button */}
          <TouchableOpacity
            style={[styles.primaryButton, isProcessing && styles.primaryButtonDisabled]}
            onPress={handleCreateTeam}
            disabled={isProcessing}
          >
            {isProcessing ? (
              <ActivityIndicator color={colors.bg.darkest} />
            ) : (
              <Text style={styles.primaryButtonText}>Create Team</Text>
            )}
          </TouchableOpacity>

          {/* Bottom padding */}
          <View style={{ height: 40 }} />
        </ScrollView>

        {/* Position Picker Modal */}
        <Modal visible={showPositionPicker} transparent animationType="slide">
          <View style={styles.modalOverlay}>
            <View style={styles.modalContent}>
              <View style={styles.modalHeader}>
                <Text style={styles.modalTitle}>Select Position</Text>
                <TouchableOpacity onPress={() => setShowPositionPicker(false)}>
                  <Text style={styles.modalDone}>Done</Text>
                </TouchableOpacity>
              </View>
              <Picker
                selectedValue={position}
                onValueChange={setPosition}
                style={styles.modalPicker}
                dropdownIconColor={colors.text.primary}
                {...pickerProps}
              >
                <Picker.Item label="Goalie" value="Goalie" color={getPickerItemColor()} />
                <Picker.Item label="Skater" value="Skater" color={getPickerItemColor()} />
              </Picker>
            </View>
          </View>
        </Modal>

        {/* Dropdown Question Picker Modal */}
        {showDropdownPicker && (
          <Modal visible={true} transparent animationType="slide">
            <View style={styles.modalOverlay}>
              <View style={styles.modalContent}>
                <View style={styles.modalHeader}>
                  <Text style={styles.modalTitle}>
                    {customQuestions.find((q) => q.id === showDropdownPicker)?.label}
                  </Text>
                  <TouchableOpacity onPress={() => setShowDropdownPicker(null)}>
                    <Text style={styles.modalDone}>Done</Text>
                  </TouchableOpacity>
                </View>
                <Picker
                  selectedValue={customResponses[showDropdownPicker] || ''}
                  onValueChange={(value) => {
                    setCustomResponses({ ...customResponses, [showDropdownPicker]: value });
                  }}
                  style={styles.modalPicker}
                  dropdownIconColor={colors.text.primary}
                  {...pickerProps}
                >
                  {customQuestions
                    .find((q) => q.id === showDropdownPicker)
                    ?.options?.map((option) => (
                      <Picker.Item
                        key={option}
                        label={option}
                        value={option}
                        color={getPickerItemColor()}
                      />
                    ))}
                </Picker>
              </View>
            </View>
          </Modal>
        )}

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
  },
  scrollContent: {
    padding: spacing.md,
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

  // Page Header
  pageTitle: {
    fontSize: 28,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  pageDescription: {
    fontSize: 15,
    color: colors.text.secondary,
    lineHeight: 22,
    marginBottom: spacing.lg,
  },

  // Section Divider
  divider: {
    height: 1,
    backgroundColor: colors.border.default,
    marginVertical: spacing.lg,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: spacing.md,
  },

  // Form Fields
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
  pickerArrow: {
    fontSize: 12,
    color: colors.text.muted,
    marginLeft: spacing.sm,
  },
  switchRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  switchLabel: {
    fontSize: 15,
    color: colors.text.primary,
  },

  // Buttons
  primaryButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  primaryButtonDisabled: {
    opacity: 0.5,
  },
  primaryButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
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
