import { useState, useEffect } from 'react';
import {
  View,
  Text,
  TextInput,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import type { SkillLevel, Organization } from '@bhmhockey/shared';
import { colors, spacing, radius, typography } from '../theme';
import { SkillLevelSelector } from './SkillLevelSelector';

export interface OrgFormData {
  name: string;
  description?: string;
  skillLevels?: SkillLevel[];
}

interface OrgFormProps {
  mode: 'create' | 'edit';
  initialData?: Organization;
  onSubmit: (data: OrgFormData) => Promise<boolean>;
  isSubmitting: boolean;
}

export function OrgForm({
  mode,
  initialData,
  onSubmit,
  isSubmitting,
}: OrgFormProps) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [skillLevels, setSkillLevels] = useState<SkillLevel[]>([]);

  // Populate form with initial data (for edit mode)
  useEffect(() => {
    if (initialData) {
      setName(initialData.name);
      setDescription(initialData.description || '');
      setSkillLevels(initialData.skillLevels || []);
    }
  }, [initialData]);

  const validateForm = (): boolean => {
    if (!name.trim()) {
      Alert.alert('Error', 'Organization name is required');
      return false;
    }
    return true;
  };

  const handleSubmit = async () => {
    if (!validateForm()) return;

    const formData: OrgFormData = {
      name: name.trim(),
      description: description.trim() || undefined,
      skillLevels: skillLevels.length > 0 ? skillLevels : undefined,
    };

    await onSubmit(formData);
  };

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
    >
      <ScrollView style={styles.scrollView} contentContainerStyle={styles.content}>
        <Text style={styles.sectionTitle}>Organization Details</Text>

        <View style={styles.inputGroup}>
          <Text style={styles.label}>Name *</Text>
          <TextInput
            style={styles.input}
            value={name}
            onChangeText={setName}
            placeholder="e.g., Boston Hockey Club"
            placeholderTextColor={colors.text.muted}
            autoCapitalize="words"
          />
        </View>

        <View style={styles.inputGroup}>
          <Text style={styles.label}>Description</Text>
          <TextInput
            style={[styles.input, styles.textArea]}
            value={description}
            onChangeText={setDescription}
            placeholder="Tell people about your organization..."
            placeholderTextColor={colors.text.muted}
            multiline
            numberOfLines={4}
            textAlignVertical="top"
          />
        </View>

        <SkillLevelSelector
          selected={skillLevels}
          onChange={setSkillLevels}
          label="Skill Levels"
        />

        <TouchableOpacity
          style={[styles.submitButton, isSubmitting && styles.submitButtonDisabled]}
          onPress={handleSubmit}
          disabled={isSubmitting}
        >
          {isSubmitting ? (
            <ActivityIndicator color={colors.bg.darkest} />
          ) : (
            <Text style={styles.submitButtonText}>
              {mode === 'create' ? 'Create Organization' : 'Save Changes'}
            </Text>
          )}
        </TouchableOpacity>

        {mode === 'create' && (
          <Text style={styles.hint}>
            As the creator, you'll be able to manage this organization and create events.
          </Text>
        )}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },
  scrollView: {
    flex: 1,
  },
  content: {
    padding: spacing.md,
  },
  sectionTitle: {
    ...typography.sectionTitle,
    marginBottom: spacing.lg,
  },
  inputGroup: {
    marginBottom: spacing.md,
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
    minHeight: 100,
    paddingTop: spacing.md,
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
  hint: {
    marginTop: spacing.md,
    fontSize: 14,
    color: colors.text.muted,
    textAlign: 'center',
    lineHeight: 20,
  },
});
