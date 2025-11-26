import { useEffect, useState } from 'react';
import {
  View,
  Text,
  ScrollView,
  StyleSheet,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { organizationService } from '@bhmhockey/api-client';
import { useOrganizationStore } from '../../stores/organizationStore';
import { useAuthStore } from '../../stores/authStore';
import type { Organization } from '@bhmhockey/shared';

export default function OrganizationDetailScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { user } = useAuthStore();
  const { subscribe, unsubscribe } = useOrganizationStore();

  const [organization, setOrganization] = useState<Organization | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isProcessing, setIsProcessing] = useState(false);

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

  const handleSubscriptionToggle = async () => {
    if (!organization) return;

    if (!user) {
      Alert.alert('Login Required', 'Please log in to subscribe', [
        { text: 'Cancel', style: 'cancel' },
        { text: 'Login', onPress: () => router.push('/(auth)/login') },
      ]);
      return;
    }

    setIsProcessing(true);

    try {
      if (organization.isSubscribed) {
        await unsubscribe(organization.id);
        setOrganization({
          ...organization,
          isSubscribed: false,
          subscriberCount: Math.max(0, organization.subscriberCount - 1),
        });
      } else {
        await subscribe(organization.id);
        setOrganization({
          ...organization,
          isSubscribed: true,
          subscriberCount: organization.subscriberCount + 1,
        });
      }
    } catch (error) {
      Alert.alert('Error', 'Failed to update subscription');
    } finally {
      setIsProcessing(false);
    }
  };

  if (isLoading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#007AFF" />
      </View>
    );
  }

  if (!organization) {
    return (
      <View style={styles.errorContainer}>
        <Text>Organization not found</Text>
      </View>
    );
  }

  return (
    <>
      <Stack.Screen options={{ title: organization.name }} />

      <ScrollView style={styles.container}>
        <View style={styles.header}>
          <Text style={styles.title}>{organization.name}</Text>

          {organization.skillLevel && (
            <View style={[styles.skillBadge, getSkillBadgeStyle(organization.skillLevel)]}>
              <Text style={styles.skillText}>{organization.skillLevel}</Text>
            </View>
          )}
        </View>

        {organization.location && (
          <View style={styles.section}>
            <Text style={styles.sectionLabel}>Location</Text>
            <Text style={styles.location}>{organization.location}</Text>
          </View>
        )}

        {organization.description && (
          <View style={styles.section}>
            <Text style={styles.sectionLabel}>About</Text>
            <Text style={styles.description}>{organization.description}</Text>
          </View>
        )}

        <View style={styles.statsSection}>
          <View style={styles.stat}>
            <Text style={styles.statValue}>{organization.subscriberCount}</Text>
            <Text style={styles.statLabel}>
              {organization.subscriberCount === 1 ? 'Subscriber' : 'Subscribers'}
            </Text>
          </View>
        </View>

        {user && (
          <TouchableOpacity
            style={[
              styles.subscribeButton,
              organization.isSubscribed && styles.subscribedButton,
              isProcessing && styles.disabledButton,
            ]}
            onPress={handleSubscriptionToggle}
            disabled={isProcessing}
          >
            {isProcessing ? (
              <ActivityIndicator color={organization.isSubscribed ? '#007AFF' : '#FFFFFF'} />
            ) : (
              <Text
                style={[
                  styles.subscribeButtonText,
                  organization.isSubscribed && styles.subscribedButtonText,
                ]}
              >
                {organization.isSubscribed ? 'Subscribed' : 'Subscribe for Notifications'}
              </Text>
            )}
          </TouchableOpacity>
        )}

        <Text style={styles.hint}>
          {organization.isSubscribed
            ? "You'll be notified when new events are posted"
            : 'Subscribe to get notified about new events'}
        </Text>
      </ScrollView>
    </>
  );
}

function getSkillBadgeStyle(skillLevel: string) {
  const colors: Record<string, string> = {
    Gold: '#FFD700',
    Silver: '#C0C0C0',
    Bronze: '#CD7F32',
    'D-League': '#4A90D9',
  };
  return { backgroundColor: colors[skillLevel] || '#666' };
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#FFFFFF',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  header: {
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#EEE',
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    marginBottom: 12,
  },
  skillBadge: {
    alignSelf: 'flex-start',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
  },
  skillText: {
    fontSize: 14,
    fontWeight: '600',
  },
  section: {
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#EEE',
  },
  sectionLabel: {
    fontSize: 12,
    fontWeight: '600',
    color: '#888',
    textTransform: 'uppercase',
    marginBottom: 8,
  },
  location: {
    fontSize: 16,
    color: '#333',
  },
  description: {
    fontSize: 16,
    color: '#333',
    lineHeight: 24,
  },
  statsSection: {
    flexDirection: 'row',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#EEE',
  },
  stat: {
    alignItems: 'center',
    marginRight: 40,
  },
  statValue: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#007AFF',
  },
  statLabel: {
    fontSize: 14,
    color: '#666',
    marginTop: 4,
  },
  subscribeButton: {
    backgroundColor: '#007AFF',
    marginHorizontal: 20,
    marginTop: 24,
    paddingVertical: 16,
    borderRadius: 12,
    alignItems: 'center',
  },
  subscribedButton: {
    backgroundColor: '#E8F4FF',
    borderWidth: 2,
    borderColor: '#007AFF',
  },
  disabledButton: {
    opacity: 0.7,
  },
  subscribeButtonText: {
    color: '#FFFFFF',
    fontSize: 18,
    fontWeight: '600',
  },
  subscribedButtonText: {
    color: '#007AFF',
  },
  hint: {
    textAlign: 'center',
    color: '#888',
    fontSize: 14,
    marginTop: 12,
    marginBottom: 40,
    paddingHorizontal: 20,
  },
});
