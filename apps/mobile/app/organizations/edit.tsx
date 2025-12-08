import { useState, useEffect } from 'react';
import { View, Alert, ActivityIndicator, StyleSheet } from 'react-native';
import { useRouter, useLocalSearchParams, Stack } from 'expo-router';
import { organizationService } from '@bhmhockey/api-client';
import type { Organization } from '@bhmhockey/shared';
import { OrgForm, OrgFormData } from '../../components';
import { colors } from '../../theme';

export default function EditOrganizationScreen() {
  const router = useRouter();
  const { id } = useLocalSearchParams<{ id: string }>();

  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [organization, setOrganization] = useState<Organization | null>(null);

  useEffect(() => {
    loadOrganization();
  }, [id]);

  const loadOrganization = async () => {
    if (!id) return;

    setIsLoading(true);
    try {
      const org = await organizationService.getById(id);
      setOrganization(org);
    } catch (error) {
      Alert.alert('Error', 'Failed to load organization');
      router.back();
    } finally {
      setIsLoading(false);
    }
  };

  const handleSubmit = async (data: OrgFormData): Promise<boolean> => {
    if (!id) return false;

    setIsSaving(true);
    try {
      await organizationService.update(id, {
        name: data.name,
        description: data.description,
        skillLevels: data.skillLevels,
      });

      Alert.alert('Success', 'Organization updated successfully!', [
        { text: 'OK', onPress: () => router.back() }
      ]);
      return true;
    } catch (error) {
      Alert.alert('Error', 'Failed to update organization. Please try again.');
      return false;
    } finally {
      setIsSaving(false);
    }
  };

  if (isLoading) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Edit Organization',
            headerBackTitle: 'Cancel',
            headerStyle: { backgroundColor: colors.bg.darkest },
            headerTintColor: colors.primary.teal,
            headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      </>
    );
  }

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Edit Organization',
          headerBackTitle: 'Cancel',
          headerStyle: { backgroundColor: colors.bg.darkest },
          headerTintColor: colors.primary.teal,
          headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
        }}
      />

      <OrgForm
        mode="edit"
        initialData={organization || undefined}
        onSubmit={handleSubmit}
        isSubmitting={isSaving}
      />
    </>
  );
}

const styles = StyleSheet.create({
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
  },
});
