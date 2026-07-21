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
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { organizationService } from '@bhmhockey/api-client';
import { useOrganizationStore } from '../../../stores/organizationStore';
import { colors, spacing, radius } from '../../../theme';

export default function OrganizationWaiverScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();

  const fetchWaiver = useOrganizationStore((state) => state.fetchWaiver);
  const saveWaiver = useOrganizationStore((state) => state.saveWaiver);

  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [text, setText] = useState('');
  const [currentVersion, setCurrentVersion] = useState<number | null>(null);
  const [selection, setSelection] = useState({ start: 0, end: 0 });

  // Wraps the selected text in ** markers (rendered bold in the app and PDF),
  // or inserts a placeholder at the cursor when nothing is selected
  const handleBold = () => {
    const { start, end } = selection;
    if (end > start) {
      setText(`${text.slice(0, start)}**${text.slice(start, end)}**${text.slice(end)}`);
    } else {
      setText(`${text.slice(0, start)}**bold text**${text.slice(start)}`);
    }
  };

  useEffect(() => {
    load();
  }, [id]);

  const load = async () => {
    if (!id) return;

    setIsLoading(true);
    try {
      // Admin gate - same pattern as the Event Defaults screen
      const org = await organizationService.getById(id);
      if (!org.isAdmin) {
        Alert.alert('Access Denied', 'Only organization admins can edit the waiver');
        router.back();
        return;
      }

      const waiver = await fetchWaiver(id);
      setText(waiver?.text ?? '');
      setCurrentVersion(waiver?.version ?? null);
    } catch (error) {
      Alert.alert('Error', 'Failed to load organization');
      router.back();
    } finally {
      setIsLoading(false);
    }
  };

  const handleSave = () => {
    if (!id) return;

    const trimmed = text.trim();
    const isClearing = trimmed.length === 0;

    const title = isClearing ? 'Disable Waiver' : 'Save New Version';
    const message = isClearing
      ? 'Saving with no text disables the waiver. Members will no longer need to accept terms to register or play. Past versions and acceptance history are preserved.'
      : 'This creates a new version. All members must accept the new terms before registering or continuing in upcoming games.';

    Alert.alert(title, message, [
      { text: 'Cancel', style: 'cancel' },
      {
        text: isClearing ? 'Disable' : 'Save',
        style: isClearing ? 'destructive' : 'default',
        onPress: async () => {
          setIsSaving(true);
          try {
            const ok = await saveWaiver(id, trimmed);
            if (ok) {
              Alert.alert(
                'Saved',
                isClearing ? 'The waiver has been disabled.' : 'The new waiver version is now in effect.',
                [{ text: 'OK', onPress: () => router.back() }]
              );
            } else {
              Alert.alert(
                'Error',
                useOrganizationStore.getState().error || 'Failed to save waiver'
              );
            }
          } finally {
            setIsSaving(false);
          }
        },
      },
    ]);
  };

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Legal Waiver',
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
        }}
      />

      {isLoading ? (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      ) : (
        <KeyboardAvoidingView
          style={{ flex: 1, backgroundColor: colors.bg.darkest }}
          behavior={Platform.OS === 'ios' ? 'padding' : undefined}
          keyboardVerticalOffset={Platform.OS === 'ios' ? 80 : 0}
        >
          <ScrollView style={styles.container} keyboardShouldPersistTaps="handled">
            {/* Explanation */}
            <View style={styles.explanationBox}>
              <Text style={styles.explanationText} allowFontScaling={false}>
                Members must accept the current waiver before registering for this
                organization's games. Saving creates a new version that everyone must
                accept again. Clearing the text disables the waiver.
              </Text>
            </View>

            {currentVersion !== null && (
              <Text style={styles.versionText} allowFontScaling={false}>
                Current version: {currentVersion}
              </Text>
            )}

            {/* Waiver text editor */}
            <View style={styles.field}>
              <View style={styles.editorHeader}>
                <Text style={styles.label} allowFontScaling={false}>Waiver Text</Text>
                <TouchableOpacity
                  style={styles.boldButton}
                  onPress={handleBold}
                  accessibilityLabel="Bold selected text"
                >
                  <Text style={styles.boldButtonText} allowFontScaling={false}>B</Text>
                </TouchableOpacity>
              </View>
              <TextInput
                style={styles.textArea}
                value={text}
                onChangeText={setText}
                onSelectionChange={(e) => setSelection(e.nativeEvent.selection)}
                placeholder="Enter the full waiver text members must accept…"
                placeholderTextColor={colors.text.muted}
                multiline
                textAlignVertical="top"
                allowFontScaling={false}
              />
              <Text style={styles.formatHint} allowFontScaling={false}>
                Select text and tap B — or wrap words in ** — to make them bold in
                the app and the PDF.
              </Text>
            </View>

            {/* Save Button */}
            <TouchableOpacity
              style={[styles.saveButton, isSaving && styles.saveButtonDisabled]}
              onPress={handleSave}
              disabled={isSaving}
            >
              {isSaving ? (
                <ActivityIndicator color={colors.bg.darkest} />
              ) : (
                <Text style={styles.saveButtonText} allowFontScaling={false}>Save Waiver</Text>
              )}
            </TouchableOpacity>

            {/* Bottom padding */}
            <View style={{ height: 40 }} />
          </ScrollView>
        </KeyboardAvoidingView>
      )}
    </>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
    padding: spacing.md,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
  explanationBox: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.md,
    marginBottom: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  explanationText: {
    fontSize: 14,
    color: colors.text.secondary,
    lineHeight: 20,
  },
  versionText: {
    fontSize: 12,
    color: colors.text.muted,
    marginBottom: spacing.md,
  },
  field: {
    marginBottom: spacing.lg,
  },
  editorHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.xs,
  },
  boldButton: {
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderRadius: radius.sm,
    paddingHorizontal: spacing.sm + 2,
    paddingVertical: 2,
  },
  boldButtonText: {
    color: colors.text.primary,
    fontSize: 15,
    fontWeight: '800',
  },
  formatHint: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: spacing.xs,
    lineHeight: 16,
  },
  label: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.xs,
  },
  textArea: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 15,
    lineHeight: 22,
    borderWidth: 1,
    borderColor: colors.border.default,
    color: colors.text.primary,
    minHeight: 280,
  },
  saveButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    alignItems: 'center',
    marginTop: spacing.sm,
  },
  saveButtonDisabled: {
    opacity: 0.7,
  },
  saveButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },
});
