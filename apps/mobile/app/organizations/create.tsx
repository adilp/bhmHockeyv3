import { useState } from 'react';
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
import { useRouter, Stack } from 'expo-router';
import { Picker } from '@react-native-picker/picker';
import { useOrganizationStore } from '../../stores/organizationStore';
import { SKILL_LEVELS } from '@bhmhockey/shared';
import type { SkillLevel } from '@bhmhockey/shared';
import { colors, spacing, radius, typography } from '../../theme';

export default function CreateOrganizationScreen() {
  const router = useRouter();
  const { createOrganization, isLoading } = useOrganizationStore();

  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [skillLevel, setSkillLevel] = useState<SkillLevel | ''>('');

  const handleCreate = async () => {
    if (!name.trim()) {
      Alert.alert('Error', 'Organization name is required');
      return;
    }

    try {
      await createOrganization({
        name: name.trim(),
        description: description.trim() || undefined,
        skillLevel: skillLevel || undefined,
      });

      Alert.alert('Success', 'Organization created successfully!', [
        { text: 'OK', onPress: () => router.back() }
      ]);
    } catch (error) {
      Alert.alert('Error', 'Failed to create organization. Please try again.');
    }
  };

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Create Organization',
          headerBackTitle: 'Cancel',
          headerStyle: {
            backgroundColor: colors.bg.darkest,
          },
          headerTintColor: colors.primary.teal,
          headerTitleStyle: {
            color: colors.text.primary,
            fontWeight: '600',
          },
        }}
      />

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
              placeholderTextColor={colors.text.placeholder}
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
              placeholderTextColor={colors.text.placeholder}
              multiline
              numberOfLines={4}
              textAlignVertical="top"
            />
          </View>

          <View style={styles.inputGroup}>
            <Text style={styles.label}>Skill Level</Text>
            <View style={styles.pickerContainer}>
              <Picker
                selectedValue={skillLevel}
                onValueChange={(value) => setSkillLevel(value)}
                style={styles.picker}
                itemStyle={styles.pickerItem}
                dropdownIconColor={colors.text.muted}
              >
                <Picker.Item label="Any Skill Level" value="" />
                {SKILL_LEVELS.map((level) => (
                  <Picker.Item key={level} label={level} value={level} />
                ))}
              </Picker>
            </View>
          </View>

          <TouchableOpacity
            style={[styles.createButton, isLoading && styles.createButtonDisabled]}
            onPress={handleCreate}
            disabled={isLoading}
          >
            {isLoading ? (
              <ActivityIndicator color={colors.bg.darkest} />
            ) : (
              <Text style={styles.createButtonText}>Create Organization</Text>
            )}
          </TouchableOpacity>

          <Text style={styles.hint}>
            As the creator, you'll be able to manage this organization and create events.
          </Text>
        </ScrollView>
      </KeyboardAvoidingView>
    </>
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
  pickerContainer: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    overflow: 'hidden',
  },
  picker: {
    height: Platform.OS === 'ios' ? 150 : 50,
    color: colors.text.primary,
  },
  pickerItem: {
    fontSize: 16,
    color: colors.text.primary,
  },
  createButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  createButtonDisabled: {
    opacity: 0.7,
  },
  createButtonText: {
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
