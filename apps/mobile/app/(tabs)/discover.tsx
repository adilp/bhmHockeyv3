import { useEffect, useMemo } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useOrganizationStore } from '../../stores/organizationStore';
import { useAuthStore } from '../../stores/authStore';
import type { Organization } from '@bhmhockey/shared';

export default function OrganizationsScreen() {
  const router = useRouter();
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

  // Split organizations into "My Organizations" (creator) and "Other Organizations"
  const { myOrganizations, otherOrganizations } = useMemo(() => {
    const myOrgs = organizations.filter(org => org.isCreator);
    const otherOrgs = organizations.filter(org => !org.isCreator);
    return { myOrganizations: myOrgs, otherOrganizations: otherOrgs };
  }, [organizations]);

  const handleSubscribeToggle = async (org: Organization, e: any) => {
    e.stopPropagation();
    if (!isAuthenticated) {
      return;
    }

    if (org.isSubscribed) {
      await unsubscribe(org.id);
    } else {
      await subscribe(org.id);
    }
  };

  const handleOrgPress = (org: Organization) => {
    router.push(`/organizations/${org.id}`);
  };

  const renderOrganization = ({ item, isMyOrg = false }: { item: Organization; isMyOrg?: boolean }) => (
    <TouchableOpacity style={styles.card} onPress={() => handleOrgPress(item)}>
      <View style={styles.cardHeader}>
        <View style={styles.cardTitleRow}>
          <Text style={styles.orgName}>{item.name}</Text>
          {isMyOrg && (
            <View style={styles.adminBadge}>
              <Text style={styles.adminBadgeText}>Admin</Text>
            </View>
          )}
        </View>
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
          {item.subscriberCount} {item.subscriberCount === 1 ? 'member' : 'members'}
        </Text>

        {isAuthenticated && !isMyOrg && (
          <TouchableOpacity
            style={[
              styles.subscribeButton,
              item.isSubscribed && styles.subscribedButton
            ]}
            onPress={(e) => handleSubscribeToggle(item, e)}
          >
            <Text style={[
              styles.subscribeButtonText,
              item.isSubscribed && styles.subscribedButtonText
            ]}>
              {item.isSubscribed ? 'Joined' : 'Join'}
            </Text>
          </TouchableOpacity>
        )}
      </View>
    </TouchableOpacity>
  );

  if (isLoading && organizations.length === 0) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color="#003366" />
        <Text style={styles.loadingText}>Loading organizations...</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <View>
            <Text style={styles.title}>Organizations</Text>
            <Text style={styles.subtitle}>Find hockey groups near you</Text>
          </View>
          {isAuthenticated && (
            <TouchableOpacity
              style={styles.createButton}
              onPress={() => router.push('/organizations/create')}
            >
              <Text style={styles.createButtonText}>+</Text>
            </TouchableOpacity>
          )}
        </View>
      </View>

      {error && (
        <View style={styles.errorBanner}>
          <Text style={styles.errorText}>{error}</Text>
        </View>
      )}

      <FlatList
        data={[]}
        renderItem={() => null}
        ListHeaderComponent={
          <>
            {/* My Organizations Section */}
            {myOrganizations.length > 0 && (
              <View style={styles.section}>
                <Text style={styles.sectionTitle}>My Organizations</Text>
                {myOrganizations.map(org => (
                  <View key={org.id}>
                    {renderOrganization({ item: org, isMyOrg: true })}
                  </View>
                ))}
              </View>
            )}

            {/* All Organizations Section */}
            <View style={styles.section}>
              {myOrganizations.length > 0 && (
                <Text style={styles.sectionTitle}>All Organizations</Text>
              )}
              {otherOrganizations.map(org => (
                <View key={org.id}>
                  {renderOrganization({ item: org })}
                </View>
              ))}
              {otherOrganizations.length === 0 && myOrganizations.length === 0 && (
                <View style={styles.emptyState}>
                  <Text style={styles.emptyTitle}>No Organizations</Text>
                  <Text style={styles.emptySubtitle}>
                    Check back later or create your own!
                  </Text>
                </View>
              )}
            </View>
          </>
        }
        refreshControl={
          <RefreshControl
            refreshing={isLoading}
            onRefresh={fetchOrganizations}
            colors={['#003366']}
          />
        }
        contentContainerStyle={styles.list}
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
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    color: '#003366',
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 16,
    color: '#666',
  },
  createButton: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: '#003366',
    justifyContent: 'center',
    alignItems: 'center',
  },
  createButtonText: {
    color: '#fff',
    fontSize: 28,
    fontWeight: '300',
    marginTop: -2,
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
  section: {
    marginBottom: 8,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
    marginBottom: 12,
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
    alignItems: 'flex-start',
    marginBottom: 8,
  },
  cardTitleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
    marginRight: 8,
  },
  orgName: {
    fontSize: 18,
    fontWeight: '600',
    marginRight: 8,
  },
  adminBadge: {
    backgroundColor: '#003366',
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 4,
  },
  adminBadgeText: {
    color: '#fff',
    fontSize: 11,
    fontWeight: '600',
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
    backgroundColor: '#003366',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 8,
  },
  subscribedButton: {
    backgroundColor: '#E8F4FF',
    borderWidth: 1,
    borderColor: '#003366',
  },
  subscribeButtonText: {
    color: '#fff',
    fontSize: 14,
    fontWeight: '600',
  },
  subscribedButtonText: {
    color: '#003366',
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
