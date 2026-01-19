import { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  TextInput,
  Modal,
  Alert,
  ActivityIndicator,
  Switch,
  Platform,
  KeyboardAvoidingView,
  Keyboard,
  InputAccessoryView,
} from 'react-native';
import { Picker } from '@react-native-picker/picker';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { tournamentService } from '@bhmhockey/api-client';
import { colors, spacing, radius } from '../../../../theme';
import { Ionicons } from '@expo/vector-icons';

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

export default function TournamentQuestionsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { currentTournament, fetchTournamentById } = useTournamentStore();

  // State
  const [questions, setQuestions] = useState<CustomQuestion[]>([]);
  const [isEditing, setIsEditing] = useState<string | null>(null);
  const [showAddModal, setShowAddModal] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  // Add/Edit question form state
  const [editingQuestion, setEditingQuestion] = useState<CustomQuestion | null>(null);
  const [newType, setNewType] = useState<'text' | 'yesno' | 'dropdown'>('text');
  const [newLabel, setNewLabel] = useState('');
  const [newRequired, setNewRequired] = useState(false);
  const [newOptions, setNewOptions] = useState(''); // comma-separated for dropdowns

  // Picker modal state
  const [showTypePicker, setShowTypePicker] = useState(false);

  // Keyboard accessory ID for iOS
  const inputAccessoryViewID = 'questionsFormAccessory';

  // Fetch tournament if not already loaded
  useEffect(() => {
    if (id && (!currentTournament || currentTournament.id !== id)) {
      fetchTournamentById(id);
    }
  }, [id, currentTournament]);

  // Parse custom questions from JSON string
  useEffect(() => {
    if (currentTournament?.customQuestions) {
      try {
        const parsed = JSON.parse(currentTournament.customQuestions);
        setQuestions(parsed);
      } catch (e) {
        console.error('Failed to parse custom questions:', e);
        setQuestions([]);
      }
    } else {
      setQuestions([]);
    }
  }, [currentTournament?.customQuestions]);

  // Reset form state
  const resetForm = () => {
    setEditingQuestion(null);
    setNewType('text');
    setNewLabel('');
    setNewRequired(false);
    setNewOptions('');
  };

  // Open add modal
  const handleAddQuestion = () => {
    resetForm();
    setShowAddModal(true);
  };

  // Open edit modal
  const handleEditQuestion = (question: CustomQuestion) => {
    setEditingQuestion(question);
    setNewType(question.type);
    setNewLabel(question.label);
    setNewRequired(question.required);
    setNewOptions(question.options?.join(', ') || '');
    setShowAddModal(true);
  };

  // Save question (add or edit)
  const handleSaveQuestion = () => {
    if (!newLabel.trim()) {
      Alert.alert('Error', 'Question label is required');
      return;
    }

    if (newType === 'dropdown') {
      const optionsArray = newOptions
        .split(',')
        .map((opt) => opt.trim())
        .filter((opt) => opt.length > 0);

      if (optionsArray.length === 0) {
        Alert.alert('Error', 'Dropdown questions must have at least one option');
        return;
      }
    }

    const questionData: CustomQuestion = {
      id: editingQuestion?.id || `q_${Date.now()}`,
      type: newType,
      label: newLabel.trim(),
      required: newRequired,
      options:
        newType === 'dropdown'
          ? newOptions
              .split(',')
              .map((opt) => opt.trim())
              .filter((opt) => opt.length > 0)
          : undefined,
    };

    if (editingQuestion) {
      // Edit existing question
      setQuestions(questions.map((q) => (q.id === editingQuestion.id ? questionData : q)));
    } else {
      // Add new question
      setQuestions([...questions, questionData]);
    }

    setShowAddModal(false);
    resetForm();
  };

  // Delete question
  const handleDeleteQuestion = (questionId: string) => {
    Alert.alert(
      'Delete Question',
      'Are you sure you want to delete this question?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Delete',
          style: 'destructive',
          onPress: () => {
            setQuestions(questions.filter((q) => q.id !== questionId));
          },
        },
      ]
    );
  };

  // Move question up
  const handleMoveUp = (index: number) => {
    if (index === 0) return;
    const newQuestions = [...questions];
    [newQuestions[index - 1], newQuestions[index]] = [newQuestions[index], newQuestions[index - 1]];
    setQuestions(newQuestions);
  };

  // Move question down
  const handleMoveDown = (index: number) => {
    if (index === questions.length - 1) return;
    const newQuestions = [...questions];
    [newQuestions[index], newQuestions[index + 1]] = [newQuestions[index + 1], newQuestions[index]];
    setQuestions(newQuestions);
  };

  // Save questions to tournament
  const handleSave = async () => {
    if (!id) return;

    setIsSaving(true);
    try {
      const customQuestionsJson = questions.length > 0 ? JSON.stringify(questions) : undefined;
      await tournamentService.update(id, { customQuestions: customQuestionsJson });

      // Refresh tournament data
      await fetchTournamentById(id);

      Alert.alert('Success', 'Questions saved successfully!', [
        { text: 'OK', onPress: () => router.back() }
      ]);
    } catch (error: any) {
      const errorMessage = error?.response?.data?.message || error?.message || 'Failed to save questions';
      Alert.alert('Error', errorMessage);
    } finally {
      setIsSaving(false);
    }
  };

  // Get type display name
  const getTypeDisplayName = (type: 'text' | 'yesno' | 'dropdown') => {
    switch (type) {
      case 'text':
        return 'Text';
      case 'yesno':
        return 'Yes/No';
      case 'dropdown':
        return 'Dropdown';
      default:
        return type;
    }
  };

  // Get type badge color
  const getTypeBadgeColor = (type: 'text' | 'yesno' | 'dropdown') => {
    switch (type) {
      case 'text':
        return colors.primary.blue;
      case 'yesno':
        return colors.primary.green;
      case 'dropdown':
        return colors.primary.purple;
      default:
        return colors.text.muted;
    }
  };

  // Loading state
  if (!currentTournament) {
    return (
      <View style={styles.loadingContainer}>
        <Stack.Screen
          options={{
            title: 'Registration Questions',
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
          title: 'Registration Questions',
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
                <Text style={styles.headerButtonText}>Save</Text>
              )}
            </TouchableOpacity>
          ),
        }}
      />

      <View style={styles.container}>
        <ScrollView
          style={styles.scrollView}
          contentContainerStyle={styles.scrollContent}
          keyboardShouldPersistTaps="handled"
        >
          {/* Header */}
          <Text style={styles.subtitle}>
            Add custom questions to your tournament registration form. Players will answer these when registering.
          </Text>

          {/* Empty state */}
          {questions.length === 0 ? (
            <View style={styles.emptyState}>
              <Ionicons name="help-circle-outline" size={64} color={colors.text.muted} />
              <Text style={styles.emptyStateTitle}>No custom questions yet</Text>
              <Text style={styles.emptyStateSubtitle}>
                Tap the + button below to add your first question
              </Text>
            </View>
          ) : (
            // Questions list
            <View style={styles.questionsList}>
              {questions.map((question, index) => (
                <View key={question.id} style={styles.questionCard}>
                  {/* Type badge */}
                  <View
                    style={[
                      styles.typeBadge,
                      { backgroundColor: getTypeBadgeColor(question.type) + '20' },
                    ]}
                  >
                    <Text
                      style={[
                        styles.typeBadgeText,
                        { color: getTypeBadgeColor(question.type) },
                      ]}
                    >
                      {getTypeDisplayName(question.type)}
                    </Text>
                  </View>

                  {/* Label */}
                  <Text style={styles.questionLabel}>
                    {question.label}
                    {question.required && <Text style={styles.requiredIndicator}> *</Text>}
                  </Text>

                  {/* Options preview for dropdown */}
                  {question.type === 'dropdown' && question.options && (
                    <Text style={styles.questionOptions}>
                      Options: {question.options.join(', ')}
                    </Text>
                  )}

                  {/* Action buttons */}
                  <View style={styles.questionActions}>
                    {/* Move up */}
                    <TouchableOpacity
                      style={[
                        styles.actionButton,
                        index === 0 && styles.actionButtonDisabled,
                      ]}
                      onPress={() => handleMoveUp(index)}
                      disabled={index === 0}
                    >
                      <Ionicons
                        name="chevron-up"
                        size={20}
                        color={index === 0 ? colors.text.muted : colors.text.secondary}
                      />
                    </TouchableOpacity>

                    {/* Move down */}
                    <TouchableOpacity
                      style={[
                        styles.actionButton,
                        index === questions.length - 1 && styles.actionButtonDisabled,
                      ]}
                      onPress={() => handleMoveDown(index)}
                      disabled={index === questions.length - 1}
                    >
                      <Ionicons
                        name="chevron-down"
                        size={20}
                        color={
                          index === questions.length - 1
                            ? colors.text.muted
                            : colors.text.secondary
                        }
                      />
                    </TouchableOpacity>

                    {/* Edit */}
                    <TouchableOpacity
                      style={styles.actionButton}
                      onPress={() => handleEditQuestion(question)}
                    >
                      <Ionicons name="pencil" size={20} color={colors.primary.teal} />
                    </TouchableOpacity>

                    {/* Delete */}
                    <TouchableOpacity
                      style={styles.actionButton}
                      onPress={() => handleDeleteQuestion(question.id)}
                    >
                      <Ionicons name="trash" size={20} color={colors.status.error} />
                    </TouchableOpacity>
                  </View>
                </View>
              ))}
            </View>
          )}

          {/* Bottom padding for FAB */}
          <View style={{ height: 100 }} />
        </ScrollView>

        {/* Floating Action Button */}
        <TouchableOpacity style={styles.fab} onPress={handleAddQuestion}>
          <Ionicons name="add-circle" size={56} color={colors.primary.teal} />
        </TouchableOpacity>
      </View>

      {/* Add/Edit Question Modal */}
      <Modal
        visible={showAddModal}
        transparent
        animationType="slide"
        onRequestClose={() => {
          setShowAddModal(false);
          resetForm();
        }}
      >
        <KeyboardAvoidingView
          style={styles.modalOverlay}
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        >
          <View style={styles.modalContent}>
            {/* Modal Header */}
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>
                {editingQuestion ? 'Edit Question' : 'Add Question'}
              </Text>
              <TouchableOpacity
                onPress={() => {
                  setShowAddModal(false);
                  resetForm();
                }}
              >
                <Text style={styles.modalCancel}>Cancel</Text>
              </TouchableOpacity>
            </View>

            <ScrollView
              style={styles.modalScrollView}
              keyboardShouldPersistTaps="handled"
              keyboardDismissMode={Platform.OS === 'ios' ? 'interactive' : 'on-drag'}
            >
              {/* Question Type */}
              <View style={styles.formField}>
                <Text style={styles.formLabel}>Question Type *</Text>
                <TouchableOpacity
                  style={styles.pickerButton}
                  onPress={() => setShowTypePicker(true)}
                >
                  <Text style={styles.pickerButtonText}>
                    {getTypeDisplayName(newType)}
                  </Text>
                  <Text style={styles.pickerArrow}>&#9660;</Text>
                </TouchableOpacity>
              </View>

              {/* Question Label */}
              <View style={styles.formField}>
                <Text style={styles.formLabel}>Question Label *</Text>
                <TextInput
                  style={styles.input}
                  value={newLabel}
                  onChangeText={setNewLabel}
                  placeholder="e.g., What is your jersey size?"
                  placeholderTextColor={colors.text.muted}
                  returnKeyType="next"
                  inputAccessoryViewID={inputAccessoryViewID}
                />
              </View>

              {/* Options for Dropdown */}
              {newType === 'dropdown' && (
                <View style={styles.formField}>
                  <Text style={styles.formLabel}>Options (comma-separated) *</Text>
                  <TextInput
                    style={styles.input}
                    value={newOptions}
                    onChangeText={setNewOptions}
                    placeholder="e.g., Small, Medium, Large, XL"
                    placeholderTextColor={colors.text.muted}
                    returnKeyType="done"
                    inputAccessoryViewID={inputAccessoryViewID}
                  />
                  <Text style={styles.fieldNote}>
                    Enter options separated by commas
                  </Text>
                </View>
              )}

              {/* Required Toggle */}
              <View style={styles.formField}>
                <View style={styles.switchRow}>
                  <Text style={styles.switchLabel}>Required</Text>
                  <Switch
                    value={newRequired}
                    onValueChange={setNewRequired}
                    trackColor={{ false: colors.border.muted, true: colors.primary.teal }}
                    thumbColor={Platform.OS === 'ios' ? undefined : colors.bg.elevated}
                  />
                </View>
                <Text style={styles.fieldNote}>
                  Players must answer required questions
                </Text>
              </View>

              {/* Save Button */}
              <TouchableOpacity style={styles.saveButton} onPress={handleSaveQuestion}>
                <Text style={styles.saveButtonText}>
                  {editingQuestion ? 'Update Question' : 'Add Question'}
                </Text>
              </TouchableOpacity>

              {/* Bottom padding */}
              <View style={{ height: 40 }} />
            </ScrollView>
          </View>
        </KeyboardAvoidingView>

        {/* Type Picker Modal */}
        <Modal visible={showTypePicker} transparent animationType="slide">
          <View style={styles.pickerModalOverlay}>
            <View style={styles.pickerModalContent}>
              <View style={styles.pickerModalHeader}>
                <Text style={styles.pickerModalTitle}>Question Type</Text>
                <TouchableOpacity onPress={() => setShowTypePicker(false)}>
                  <Text style={styles.pickerModalDone}>Done</Text>
                </TouchableOpacity>
              </View>
              <Picker
                selectedValue={newType}
                onValueChange={(value) => {
                  setNewType(value);
                  setShowTypePicker(false);
                }}
                style={styles.picker}
                dropdownIconColor={colors.text.primary}
                {...pickerProps}
              >
                <Picker.Item label="Text" value="text" color={getPickerItemColor()} />
                <Picker.Item label="Yes/No" value="yesno" color={getPickerItemColor()} />
                <Picker.Item label="Dropdown" value="dropdown" color={getPickerItemColor()} />
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
                <Text style={styles.pickerModalDone}>Done</Text>
              </TouchableOpacity>
            </View>
          </InputAccessoryView>
        )}
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
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
  scrollView: {
    flex: 1,
  },
  scrollContent: {
    padding: spacing.md,
  },
  subtitle: {
    fontSize: 14,
    color: colors.text.secondary,
    lineHeight: 20,
    marginBottom: spacing.lg,
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

  // Empty state
  emptyState: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: spacing.xxl * 2,
  },
  emptyStateTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
    marginTop: spacing.md,
  },
  emptyStateSubtitle: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: spacing.xs,
    textAlign: 'center',
  },

  // Questions list
  questionsList: {
    gap: spacing.md,
  },
  questionCard: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.lg,
    padding: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  typeBadge: {
    alignSelf: 'flex-start',
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs / 2,
    borderRadius: radius.sm,
    marginBottom: spacing.sm,
  },
  typeBadgeText: {
    fontSize: 11,
    fontWeight: '600',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  questionLabel: {
    fontSize: 15,
    fontWeight: '500',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  requiredIndicator: {
    color: colors.status.error,
  },
  questionOptions: {
    fontSize: 13,
    color: colors.text.muted,
    marginBottom: spacing.sm,
  },
  questionActions: {
    flexDirection: 'row',
    gap: spacing.sm,
    marginTop: spacing.sm,
    paddingTop: spacing.sm,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  actionButton: {
    padding: spacing.xs,
    borderRadius: radius.sm,
    backgroundColor: colors.bg.hover,
    alignItems: 'center',
    justifyContent: 'center',
  },
  actionButtonDisabled: {
    opacity: 0.3,
  },

  // Floating Action Button
  fab: {
    position: 'absolute',
    bottom: spacing.lg,
    right: spacing.lg,
    borderRadius: radius.round,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
    elevation: 8,
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
    maxHeight: '80%',
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
  modalCancel: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.muted,
  },
  modalScrollView: {
    flex: 1,
  },

  // Form fields
  formField: {
    marginTop: spacing.lg,
    marginHorizontal: spacing.md,
  },
  formLabel: {
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
  fieldNote: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: spacing.xs,
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

  // Picker button
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

  // Save button
  saveButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    marginHorizontal: spacing.md,
    marginTop: spacing.lg,
  },
  saveButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },

  // Type picker modal
  pickerModalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
    justifyContent: 'flex-end',
  },
  pickerModalContent: {
    backgroundColor: colors.bg.dark,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    paddingBottom: 34,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderBottomWidth: 0,
  },
  pickerModalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  pickerModalTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
  },
  pickerModalDone: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.primary.teal,
  },
  picker: {
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
