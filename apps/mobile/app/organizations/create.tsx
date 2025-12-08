import { Alert } from 'react-native';
import { useRouter, Stack } from 'expo-router';
import { useOrganizationStore } from '../../stores/organizationStore';
import { OrgForm, OrgFormData } from '../../components';
import { colors } from '../../theme';

export default function CreateOrganizationScreen() {
  const router = useRouter();
  const { createOrganization, isLoading } = useOrganizationStore();

  const handleSubmit = async (data: OrgFormData): Promise<boolean> => {
    try {
      await createOrganization({
        name: data.name,
        description: data.description,
        skillLevels: data.skillLevels,
      });

      Alert.alert('Success', 'Organization created successfully!', [
        { text: 'OK', onPress: () => router.back() }
      ]);
      return true;
    } catch (error) {
      Alert.alert('Error', 'Failed to create organization. Please try again.');
      return false;
    }
  };

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Create Organization',
          headerBackTitle: 'Cancel',
          headerStyle: { backgroundColor: colors.bg.darkest },
          headerTintColor: colors.primary.teal,
          headerTitleStyle: { color: colors.text.primary, fontWeight: '600' },
        }}
      />

      <OrgForm
        mode="create"
        onSubmit={handleSubmit}
        isSubmitting={isLoading}
      />
    </>
  );
}
