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
} from 'react-native';
import { Picker } from '@react-native-picker/picker';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { useAuthStore } from '../../../../stores/authStore';
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

export default function TournamentRegisterScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { currentTournament, isRegistering, registerForTournament, fetchTournamentById } = useTournamentStore();
  const { user } = useAuthStore();

  // Wizard state - start at step 0 if eligibility requirements exist, otherwise step 1
  const hasEligibilityRequirements = !!currentTournament?.eligibilityRequirements;
  const [step, setStep] = useState<0 | 1 | 2>(hasEligibilityRequirements ? 0 : 1);
  const [eligibilityAccepted, setEligibilityAccepted] = useState(false);

  // Step 1: Position & Custom Questions
  const [position, setPosition] = useState<string>('Skater');
  const [customResponses, setCustomResponses] = useState<Record<string, any>>({});
  const [showPositionPicker, setShowPositionPicker] = useState(false);
  const [showDropdownPicker, setShowDropdownPicker] = useState<string | null>(null);

  // Step 2: Payment & Waiver
  const [waiverAccepted, setWaiverAccepted] = useState(false);

  // Keyboard accessory ID for iOS
  const inputAccessoryViewID = 'registerFormAccessory';

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

  // Validation for step 1
  const validateStep1 = (): boolean => {
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

  // Validation for step 2
  const validateStep2 = (): boolean => {
    if (currentTournament?.waiverUrl && !waiverAccepted) {
      Alert.alert('Waiver Required', 'Please accept the waiver to continue.');
      return false;
    }
    return true;
  };

  // Handle next button (step 1 -> step 2)
  const handleNext = () => {
    if (!validateStep1()) return;
    setStep(2);
  };

  // Handle back button (step 2 -> step 1)
  const handleBack = () => {
    setStep(1);
  };

  // Handle register button
  const handleRegister = async () => {
    if (!validateStep2() || !id) return;

    const request: CreateTournamentRegistrationRequest = {
      position,
      customResponses: customQuestions.length > 0 ? JSON.stringify(customResponses) : undefined,
      waiverAccepted: currentTournament?.waiverUrl ? waiverAccepted : true,
    };

    const result = await registerForTournament(id, request);

    if (result) {
      // Show success message - different for PreFormed tournaments (free agent)
      if (result.status === 'Waitlisted') {
        Alert.alert(
          'Added to Waitlist',
          `You're #${result.waitlistPosition} on the waitlist. We'll notify you when a spot opens up!`,
          [{ text: 'OK', onPress: () => router.back() }]
        );
      } else if (currentTournament?.teamFormation === 'PreFormed') {
        // PreFormed tournament - user is joining as free agent
        Alert.alert(
          'Registration Successful',
          "You've registered as a free agent! The free agent pool feature is coming soon. In the meantime, you can browse teams and contact captains directly to join a team.",
          [{ text: 'OK', onPress: () => router.back() }]
        );
      } else {
        Alert.alert(
          'Registration Successful',
          result.message || 'You have been registered for the tournament!',
          [{ text: 'OK', onPress: () => router.back() }]
        );
      }
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
            title: 'Register',
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
          title: step === 0
            ? 'Register - Eligibility'
            : step === 1
              ? `Register - Step ${hasEligibilityRequirements ? '1 of 3' : '1 of 2'}`
              : `Register - Step ${hasEligibilityRequirements ? '2 of 3' : '2 of 2'}`,
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
          <View style={styles.stepIndicator}>
            {hasEligibilityRequirements && (
              <>
                <View style={[styles.stepDot, step === 0 && styles.stepDotActive]}>
                  <Text style={[styles.stepDotText, step === 0 && styles.stepDotTextActive]}>0</Text>
                </View>
                <View style={styles.stepLine} />
              </>
            )}
            <View style={[styles.stepDot, step === 1 && styles.stepDotActive]}>
              <Text style={[styles.stepDotText, step === 1 && styles.stepDotTextActive]}>1</Text>
            </View>
            <View style={styles.stepLine} />
            <View style={[styles.stepDot, step === 2 && styles.stepDotActive]}>
              <Text style={[styles.stepDotText, step === 2 && styles.stepDotTextActive]}>2</Text>
            </View>
          </View>

          {step === 0 ? (
            // STEP 0: Eligibility Requirements
            <>
              <Text style={styles.stepTitle}>Eligibility Requirements</Text>

              <View style={styles.eligibilityBox}>
                <Text style={styles.eligibilityText}>
                  {currentTournament?.eligibilityRequirements}
                </Text>
              </View>

              <View style={styles.field}>
                <View style={styles.waiverRow}>
                  <Switch
                    value={eligibilityAccepted}
                    onValueChange={setEligibilityAccepted}
                    trackColor={{ false: colors.border.muted, true: colors.primary.teal }}
                    thumbColor={Platform.OS === 'ios' ? undefined : colors.bg.elevated}
                  />
                  <View style={styles.waiverTextContainer}>
                    <Text style={styles.waiverText}>
                      I have read and meet the eligibility requirements above
                    </Text>
                  </View>
                </View>
              </View>

              <TouchableOpacity
                style={[styles.primaryButton, !eligibilityAccepted && styles.primaryButtonDisabled]}
                onPress={() => setStep(1)}
                disabled={!eligibilityAccepted}
              >
                <Text style={styles.primaryButtonText}>Continue</Text>
              </TouchableOpacity>
            </>
          ) : step === 1 ? (
            // STEP 1: Position & Custom Questions
            <>
              <Text style={styles.stepTitle}>Position & Questions</Text>

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

              {/* Next Button */}
              <TouchableOpacity style={styles.primaryButton} onPress={handleNext}>
                <Text style={styles.primaryButtonText}>Next</Text>
              </TouchableOpacity>
            </>
          ) : (
            // STEP 2: Payment & Waiver
            <>
              <Text style={styles.stepTitle}>Payment & Waiver</Text>

              {/* Payment Info */}
              {currentTournament.entryFee > 0 && (
                <View style={styles.infoBox}>
                  <Text style={styles.infoBoxTitle}>Entry Fee</Text>
                  <Text style={styles.infoBoxAmount}>${currentTournament.entryFee.toFixed(2)}</Text>
                  <Text style={styles.infoBoxDescription}>
                    Payment will be collected via Venmo after registration. You'll receive instructions
                    with the organizer's Venmo handle.
                  </Text>
                </View>
              )}

              {/* Waiver */}
              {currentTournament.waiverUrl && (
                <View style={styles.field}>
                  <View style={styles.waiverRow}>
                    <Switch
                      value={waiverAccepted}
                      onValueChange={setWaiverAccepted}
                      trackColor={{ false: colors.border.muted, true: colors.primary.teal }}
                      thumbColor={Platform.OS === 'ios' ? undefined : colors.bg.elevated}
                    />
                    <View style={styles.waiverTextContainer}>
                      <Text style={styles.waiverText}>
                        I accept the{' '}
                        <Text
                          style={styles.waiverLink}
                          onPress={() => {
                            // In a real app, would open the waiver URL in browser
                            Alert.alert('Waiver', `View waiver at: ${currentTournament.waiverUrl}`);
                          }}
                        >
                          tournament waiver
                        </Text>
                      </Text>
                    </View>
                  </View>
                </View>
              )}

              {/* Back & Register Buttons */}
              <View style={styles.buttonRow}>
                <TouchableOpacity style={styles.secondaryButton} onPress={handleBack}>
                  <Text style={styles.secondaryButtonText}>Back</Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[
                    styles.primaryButton,
                    styles.primaryButtonFlex,
                    (isRegistering ||
                      (currentTournament.waiverUrl && !waiverAccepted)) &&
                      styles.primaryButtonDisabled,
                  ]}
                  onPress={handleRegister}
                  disabled={
                    isRegistering || !!(currentTournament.waiverUrl && !waiverAccepted)
                  }
                >
                  {isRegistering ? (
                    <ActivityIndicator color={colors.bg.darkest} />
                  ) : (
                    <Text style={styles.primaryButtonText}>Register</Text>
                  )}
                </TouchableOpacity>
              </View>
            </>
          )}

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
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: spacing.xl,
  },
  stepDot: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: colors.bg.elevated,
    borderWidth: 2,
    borderColor: colors.border.muted,
    justifyContent: 'center',
    alignItems: 'center',
  },
  stepDotActive: {
    backgroundColor: colors.primary.teal,
    borderColor: colors.primary.teal,
  },
  stepDotText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.muted,
  },
  stepDotTextActive: {
    color: colors.bg.darkest,
  },
  stepLine: {
    width: 60,
    height: 2,
    backgroundColor: colors.border.muted,
    marginHorizontal: spacing.sm,
  },

  // Step Title
  stepTitle: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text.primary,
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

  // Eligibility Box
  eligibilityBox: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
    marginBottom: spacing.lg,
  },
  eligibilityText: {
    fontSize: 15,
    color: colors.text.primary,
    lineHeight: 22,
  },

  // Info Box (Payment)
  infoBox: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.lg,
    padding: spacing.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
    marginBottom: spacing.lg,
  },
  infoBoxTitle: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.xs,
  },
  infoBoxAmount: {
    fontSize: 28,
    fontWeight: '700',
    color: colors.primary.teal,
    marginBottom: spacing.sm,
  },
  infoBoxDescription: {
    fontSize: 14,
    color: colors.text.secondary,
    lineHeight: 20,
  },

  // Waiver
  waiverRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  waiverTextContainer: {
    flex: 1,
    marginLeft: spacing.md,
  },
  waiverText: {
    fontSize: 14,
    color: colors.text.secondary,
    lineHeight: 20,
  },
  waiverLink: {
    color: colors.primary.teal,
    textDecorationLine: 'underline',
  },

  // Buttons
  primaryButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  primaryButtonFlex: {
    flex: 1,
    marginLeft: spacing.sm,
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
    marginTop: spacing.sm,
    borderWidth: 1,
    borderColor: colors.border.muted,
    flex: 1,
    marginRight: spacing.sm,
  },
  secondaryButtonText: {
    color: colors.text.primary,
    fontSize: 16,
    fontWeight: '600',
  },
  buttonRow: {
    flexDirection: 'row',
    alignItems: 'center',
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
