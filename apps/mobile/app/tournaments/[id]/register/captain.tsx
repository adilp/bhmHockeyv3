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
  KeyboardAvoidingView,
  Keyboard,
  InputAccessoryView,
  Switch,
  Modal,
  Linking,
} from 'react-native';
import { Picker } from '@react-native-picker/picker';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { useTournamentTeamStore } from '../../../../stores/tournamentTeamStore';
import { colors, spacing, radius } from '../../../../theme';
import type { CreateTournamentRegistrationRequest } from '@bhmhockey/shared';

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

export default function CaptainRegistrationScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { currentTournament, fetchTournamentById, registerForTournament, isRegistering } = useTournamentStore();
  const { createTeam, isProcessing } = useTournamentTeamStore();

  // Wizard state
  const [currentStep, setCurrentStep] = useState(1);

  // Form state
  const [teamName, setTeamName] = useState('');
  const [position, setPosition] = useState<string>('Skater');
  const [customResponses, setCustomResponses] = useState<Record<string, any>>({});
  const [waiverAccepted, setWaiverAccepted] = useState(false);

  // Modal state
  const [showPositionPicker, setShowPositionPicker] = useState(false);
  const [showDropdownPicker, setShowDropdownPicker] = useState<string | null>(null);

  // Keyboard accessory ID for iOS
  const inputAccessoryViewID = 'captainRegFormAccessory';

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

  // Step 1 validation (Team Name)
  const validateStep1 = (): boolean => {
    if (!teamName.trim() || teamName.trim().length < 2) {
      Alert.alert('Invalid Team Name', 'Team name must be at least 2 characters long.');
      return false;
    }
    return true;
  };

  // Step 2 validation (Position & Custom Questions)
  const validateStep2 = (): boolean => {
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

  // Step 3 validation (Waiver)
  const validateStep3 = (): boolean => {
    if (currentTournament?.waiverUrl && !waiverAccepted) {
      Alert.alert('Waiver Required', 'You must accept the waiver to continue.');
      return false;
    }
    return true;
  };

  // Navigate to next step
  const handleNext = () => {
    if (currentStep === 1 && validateStep1()) {
      setCurrentStep(2);
    } else if (currentStep === 2 && validateStep2()) {
      setCurrentStep(3);
    }
  };

  // Navigate to previous step
  const handleBack = () => {
    if (currentStep > 1) {
      setCurrentStep(currentStep - 1);
    }
  };

  // Handle final submission
  const handleSubmit = async () => {
    if (!validateStep3() || !id) return;

    try {
      // Step 1: Create the team
      const newTeam = await createTeam(id, teamName.trim());

      if (!newTeam) {
        Alert.alert('Error', 'Failed to create team. Please try again.');
        return;
      }

      // Step 2: Register as captain for the tournament
      // Note: The team creation already made this user the captain
      // The registration is separate - it registers the user for the tournament
      const registrationRequest: CreateTournamentRegistrationRequest = {
        position,
        customResponses: JSON.stringify(customResponses),
        waiverAccepted: currentTournament?.waiverUrl ? waiverAccepted : true,
      };

      const result = await registerForTournament(id, registrationRequest);

      if (!result) {
        Alert.alert('Error', 'Team created but failed to register as captain. Please contact support.');
        return;
      }

      // Success - navigate to team detail page
      Alert.alert(
        'Success',
        `${teamName} has been created and you are now registered as team captain!`,
        [
          {
            text: 'OK',
            onPress: () => {
              router.replace(`/tournaments/${id}/teams/${newTeam.id}`);
            },
          },
        ]
      );
    } catch (error) {
      console.error('Failed to create team and register:', error);
      Alert.alert('Error', 'An unexpected error occurred. Please try again.');
    }
  };

  // Open waiver URL
  const handleViewWaiver = () => {
    if (currentTournament?.waiverUrl) {
      Linking.openURL(currentTournament.waiverUrl);
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

  // Render step indicator dots
  const renderStepIndicator = () => {
    return (
      <View style={styles.stepIndicator}>
        {[1, 2, 3].map((step, index) => (
          <View key={step} style={styles.stepRow}>
            <View style={[styles.stepDot, step <= currentStep && styles.stepDotActive]}>
              <Text style={[styles.stepDotText, step <= currentStep && styles.stepDotTextActive]}>
                {step}
              </Text>
            </View>
            {index < 2 && <View style={[styles.stepLine, step < currentStep && styles.stepLineActive]} />}
          </View>
        ))}
      </View>
    );
  };

  // Loading state
  if (!currentTournament) {
    return (
      <View style={styles.loadingContainer}>
        <Stack.Screen
          options={{
            title: 'Register as Captain',
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

  const isLastStep = currentStep === 3;
  const isSubmitting = isProcessing || isRegistering;

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Register as Captain',
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
          {/* Step Indicator */}
          {renderStepIndicator()}

          {/* Step 1: Team Name */}
          {currentStep === 1 && (
            <>
              <Text style={styles.pageTitle}>Create Your Team</Text>
              <Text style={styles.pageDescription}>
                Choose a name for your team. You'll be automatically registered as the team captain.
              </Text>

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
            </>
          )}

          {/* Step 2: Position & Custom Questions */}
          {currentStep === 2 && (
            <>
              <Text style={styles.pageTitle}>Captain Registration</Text>
              <Text style={styles.pageDescription}>
                Complete your registration as team captain.
              </Text>

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
            </>
          )}

          {/* Step 3: Waiver & Summary */}
          {currentStep === 3 && (
            <>
              <Text style={styles.pageTitle}>Review & Submit</Text>
              <Text style={styles.pageDescription}>
                Review your information before submitting.
              </Text>

              {/* Summary */}
              <View style={styles.summaryCard}>
                <View style={styles.summaryRow}>
                  <Text style={styles.summaryLabel}>Team Name:</Text>
                  <Text style={styles.summaryValue}>{teamName}</Text>
                </View>
                <View style={styles.summaryRow}>
                  <Text style={styles.summaryLabel}>Your Position:</Text>
                  <Text style={styles.summaryValue}>{position}</Text>
                </View>
                <View style={styles.summaryRow}>
                  <Text style={styles.summaryLabel}>Role:</Text>
                  <Text style={styles.summaryValue}>Team Captain</Text>
                </View>
              </View>

              {/* Waiver */}
              {currentTournament.waiverUrl && (
                <View style={styles.field}>
                  <View style={styles.waiverCard}>
                    <TouchableOpacity onPress={handleViewWaiver}>
                      <Text style={styles.waiverLink}>View Tournament Waiver</Text>
                    </TouchableOpacity>
                    <View style={styles.switchRow}>
                      <Text style={styles.waiverText}>I accept the waiver</Text>
                      <Switch
                        value={waiverAccepted}
                        onValueChange={setWaiverAccepted}
                        trackColor={{ false: colors.border.muted, true: colors.primary.teal }}
                        thumbColor={Platform.OS === 'ios' ? undefined : colors.bg.elevated}
                      />
                    </View>
                  </View>
                </View>
              )}
            </>
          )}

          {/* Navigation Buttons */}
          <View style={styles.buttonContainer}>
            {currentStep > 1 && (
              <TouchableOpacity
                style={[styles.secondaryButton, { flex: 1, marginRight: spacing.sm }]}
                onPress={handleBack}
                disabled={isSubmitting}
              >
                <Text style={styles.secondaryButtonText}>Back</Text>
              </TouchableOpacity>
            )}

            {!isLastStep ? (
              <TouchableOpacity
                style={[styles.primaryButton, currentStep === 1 && { flex: 1 }]}
                onPress={handleNext}
              >
                <Text style={styles.primaryButtonText}>Next</Text>
              </TouchableOpacity>
            ) : (
              <TouchableOpacity
                style={[styles.primaryButton, { flex: 1 }, isSubmitting && styles.primaryButtonDisabled]}
                onPress={handleSubmit}
                disabled={isSubmitting}
              >
                {isSubmitting ? (
                  <ActivityIndicator color={colors.bg.darkest} />
                ) : (
                  <Text style={styles.primaryButtonText}>Create Team & Register</Text>
                )}
              </TouchableOpacity>
            )}
          </View>

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

  // Step Indicator
  stepIndicator: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: spacing.lg,
  },
  stepRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  stepDot: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: colors.bg.elevated,
    borderWidth: 2,
    borderColor: colors.border.default,
    justifyContent: 'center',
    alignItems: 'center',
  },
  stepDotActive: {
    backgroundColor: colors.primary.teal,
    borderColor: colors.primary.teal,
  },
  stepDotText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.muted,
  },
  stepDotTextActive: {
    color: colors.bg.darkest,
  },
  stepLine: {
    width: 60,
    height: 2,
    backgroundColor: colors.border.default,
    marginHorizontal: spacing.xs,
  },
  stepLineActive: {
    backgroundColor: colors.primary.teal,
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

  // Summary Card
  summaryCard: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    marginBottom: spacing.lg,
  },
  summaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  summaryLabel: {
    fontSize: 14,
    color: colors.text.muted,
    fontWeight: '600',
  },
  summaryValue: {
    fontSize: 14,
    color: colors.text.primary,
    fontWeight: '500',
  },

  // Waiver Card
  waiverCard: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  waiverLink: {
    fontSize: 15,
    color: colors.primary.teal,
    fontWeight: '600',
    marginBottom: spacing.md,
    textDecorationLine: 'underline',
  },
  waiverText: {
    fontSize: 15,
    color: colors.text.primary,
  },

  // Buttons
  buttonContainer: {
    flexDirection: 'row',
    marginTop: spacing.sm,
  },
  primaryButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    flex: 1,
  },
  primaryButtonDisabled: {
    opacity: 0.5,
  },
  primaryButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },
  secondaryButton: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  secondaryButtonText: {
    color: colors.text.primary,
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
