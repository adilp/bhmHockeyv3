import { useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { useOrganizationStore } from '../../stores/organizationStore';
import { useAuthStore } from '../../stores/authStore';
import type { Organization } from '@bhmhockey/shared';

export default function DiscoverScreen() {
  const { isAuthenticated } = useAuthStore();
  const {
    organizations,
    isLoading,
    error,
    fetchOrganizations,
    subscribe,
    unsubscribe,
  } = useOrganizationStore();

  useEffect(() => {
    fetchOrganizations();
  }, []);

  const handleSubscribeToggle = async (org: Organization) => {
    if (!isAuthenticated) {
      return;
    }

    if (org.isSubscribed) {
      await unsubscribe(org.id);
    } else {
      await subscribe(org.id);
    }
  };

  const renderOrganization = ({ item }: { item: Organization }) => (
    <View style={styles.card}>
      <View style={styles.cardHeader}>
        <Text style={styles.orgName}>{item.name}</Text>
        {item.skillLevel && (
          <View style={[styles.badge, getBadgeStyle(item.skillLevel)]}>
            <Text style={styles.badgeText}>{item.skillLevel}</Text>
          </View>
        )}
      </View>

      {item.description && (
        <Text style={styles.description} numberOfLines={2}>
          {item.description}
        </Text>
      )}

      {item.location && (
        <Text style={styles.location}>{item.location}</Text>
      )}

      <View style={styles.cardFooter}>
        <Text style={styles.subscriberCount}>
          {item.subscriberCount} {item.subscriberCount === 1 ? 'subscriber' : 'subscribers'}
        </Text>

        {isAuthenticated && (
          <TouchableOpacity
            style={[
              styles.subscribeButton,
              item.isSubscribed && styles.subscribedButton
            ]}
            onPress={() => handleSubscribeToggle(item)}
          >
            <Text style={[
              styles.subscribeButtonText,
              item.isSubscribed && styles.subscribedButtonText
            ]}>
              {item.isSubscribed ? 'Subscribed' : 'Subscribe'}
            </Text>
          </TouchableOpacity>
        )}
      </View>
    </View>
  );

  if (isLoading && organizations.length === 0) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color="#007AFF" />
        <Text style={styles.loadingText}>Loading organizations...</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>Discover</Text>
        <Text style={styles.subtitle}>Find hockey organizations near you</Text>
      </View>

      {error && (
        <View style={styles.errorBanner}>
          <Text style={styles.errorText}>{error}</Text>
        </View>
      )}

      <FlatList
        data={organizations}
        renderItem={renderOrganization}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.list}
        refreshControl={
          <RefreshControl
            refreshing={isLoading}
            onRefresh={fetchOrganizations}
            colors={['#007AFF']}
          />
        }
        ListEmptyComponent={
          <View style={styles.emptyState}>
            <Text style={styles.emptyTitle}>No Organizations</Text>
            <Text style={styles.emptySubtitle}>
              Check back later for new hockey organizations
            </Text>
          </View>
        }
      />
    </View>
  );
}

const getBadgeStyle = (skillLevel: string) => {
  switch (skillLevel) {
    case 'Gold':
      return { backgroundColor: '#FFD700' };
    case 'Silver':
      return { backgroundColor: '#C0C0C0' };
    case 'Bronze':
      return { backgroundColor: '#CD7F32' };
    case 'D-League':
      return { backgroundColor: '#4A90D9' };
    default:
      return { backgroundColor: '#999' };
  }
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  centered: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 12,
    fontSize: 16,
    color: '#666',
  },
  header: {
    padding: 20,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 16,
    color: '#666',
  },
  errorBanner: {
    backgroundColor: '#FFE5E5',
    padding: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#FF3B30',
  },
  errorText: {
    color: '#FF3B30',
    textAlign: 'center',
  },
  list: {
    padding: 16,
  },
  card: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  cardHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  orgName: {
    fontSize: 18,
    fontWeight: '600',
    flex: 1,
    marginRight: 8,
  },
  badge: {
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: 12,
  },
  badgeText: {
    fontSize: 12,
    fontWeight: '600',
    color: '#fff',
  },
  description: {
    fontSize: 14,
    color: '#666',
    marginBottom: 8,
    lineHeight: 20,
  },
  location: {
    fontSize: 14,
    color: '#999',
    marginBottom: 12,
  },
  cardFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: '#f0f0f0',
  },
  subscriberCount: {
    fontSize: 14,
    color: '#666',
  },
  subscribeButton: {
    backgroundColor: '#007AFF',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 8,
  },
  subscribedButton: {
    backgroundColor: '#E8F4FF',
    borderWidth: 1,
    borderColor: '#007AFF',
  },
  subscribeButtonText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '600',
  },
  subscribedButtonText: {
    color: '#007AFF',
  },
  emptyState: {
    alignItems: 'center',
    paddingVertical: 40,
  },
  emptyTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
    marginBottom: 8,
  },
  emptySubtitle: {
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
  },
});
