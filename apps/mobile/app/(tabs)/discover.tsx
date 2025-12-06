import { useEffect, useMemo } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { useRouter } from 'expo-router';
import { useOrganizationStore } from '../../stores/organizationStore';
import { useAuthStore } from '../../stores/authStore';
import { OrgCard, SectionHeader, EmptyState } from '../../components';
import { colors, spacing, radius } from '../../theme';
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

  // Split organizations into "My Organizations" (admin) and "Other Organizations"
  const { myOrganizations, otherOrganizations } = useMemo(() => {
    const myOrgs = organizations.filter(org => org.isAdmin);
    const otherOrgs = organizations.filter(org => !org.isAdmin);
    return { myOrganizations: myOrgs, otherOrganizations: otherOrgs };
  }, [organizations]);

  const handleSubscribeToggle = async (org: Organization) => {
    if (!isAuthenticated) return;

    if (org.isSubscribed) {
      await unsubscribe(org.id);
    } else {
      await subscribe(org.id);
    }
  };

  const handleOrgPress = (org: Organization) => {
    router.push(`/organizations/${org.id}`);
  };

  if (isLoading && organizations.length === 0) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
        <Text style={styles.loadingText}>Loading organizations...</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* Header */}
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

      {/* Error banner */}
      {error && (
        <View style={styles.errorBanner}>
          <Text style={styles.errorText}>{error}</Text>
        </View>
      )}

      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.scrollContent}
        refreshControl={
          <RefreshControl
            refreshing={isLoading}
            onRefresh={fetchOrganizations}
            tintColor={colors.primary.teal}
          />
        }
      >
        {/* My Organizations Section */}
        {myOrganizations.length > 0 && (
          <View style={styles.section}>
            <SectionHeader title="My Organizations" count={myOrganizations.length} />
            {myOrganizations.map(org => (
              <OrgCard
                key={org.id}
                organization={org}
                isAdmin
                onPress={() => handleOrgPress(org)}
              />
            ))}
          </View>
        )}

        {/* All Organizations Section */}
        <View style={styles.section}>
          {myOrganizations.length > 0 && (
            <SectionHeader title="All Organizations" count={otherOrganizations.length} />
          )}
          {otherOrganizations.length > 0 ? (
            otherOrganizations.map(org => (
              <OrgCard
                key={org.id}
                organization={org}
                onPress={() => handleOrgPress(org)}
                showJoinButton={isAuthenticated}
                onJoinPress={() => handleSubscribeToggle(org)}
              />
            ))
          ) : myOrganizations.length === 0 ? (
            <EmptyState
              icon="🏒"
              title="No Organizations"
              message="Check back later or create your own!"
              actionLabel={isAuthenticated ? "Create Organization" : undefined}
              onAction={isAuthenticated ? () => router.push('/organizations/create') : undefined}
            />
          ) : null}
        </View>

        {/* Bottom padding */}
        <View style={{ height: 40 }} />
      </ScrollView>
    </View>
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
  header: {
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.lg,
    paddingBottom: spacing.md,
    backgroundColor: colors.bg.darkest,
  },
  headerRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  title: {
    fontSize: 28,
    fontWeight: '700',
    color: colors.text.primary,
    letterSpacing: -0.5,
  },
  subtitle: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: 2,
  },
  createButton: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: colors.primary.teal,
    justifyContent: 'center',
    alignItems: 'center',
  },
  createButtonText: {
    color: colors.bg.darkest,
    fontSize: 28,
    fontWeight: '400',
    marginTop: -2,
  },
  errorBanner: {
    backgroundColor: colors.status.errorSubtle,
    padding: spacing.sm,
    marginHorizontal: spacing.lg,
    borderRadius: radius.md,
  },
  errorText: {
    color: colors.status.error,
    textAlign: 'center',
    fontSize: 14,
  },
  scrollView: {
    flex: 1,
  },
  scrollContent: {
    paddingHorizontal: spacing.lg,
  },
  section: {
    marginTop: spacing.lg,
  },
});
