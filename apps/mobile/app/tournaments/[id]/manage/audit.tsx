import { useState, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  RefreshControl,
  ActivityIndicator,
  Platform,
  ActionSheetIOS,
  Alert,
} from 'react-native';
import { useLocalSearchParams, Stack, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { EmptyState } from '../../../../components';
import { colors, spacing, radius } from '../../../../theme';
import type { TournamentAuditLogDto } from '@bhmhockey/shared';

// Filter options for audit log actions
const ACTION_FILTERS = [
  { label: 'All Activity', value: null },
  { label: 'Status Changes', value: 'STATUS_CHANGE' },
  { label: 'Score Updates', value: 'SCORE_ENTERED' },
  { label: 'Team Changes', value: 'TEAM' },
  { label: 'Admin Changes', value: 'ADMIN' },
  { label: 'Registration', value: 'REGISTRATION' },
];

// Helper to format timestamp nicely
const formatTimestamp = (iso: string) => {
  const date = new Date(iso);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);

  // Less than 1 minute ago
  if (diffMins < 1) return 'Just now';
  // Less than 1 hour ago
  if (diffMins < 60) return `${diffMins}m ago`;
  // Less than 24 hours ago
  if (diffHours < 24) return `${diffHours}h ago`;
  // Less than 7 days ago
  if (diffDays < 7) return `${diffDays}d ago`;

  // Otherwise, show full date
  return date.toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
};

// Component to render a single audit log item
const AuditLogItem = ({ item }: { item: TournamentAuditLogDto }) => {
  return (
    <View style={styles.logItem}>
      <View style={styles.logContent}>
        <Text style={styles.logDescription}>{item.actionDescription}</Text>
        <View style={styles.logMetaRow}>
          <Text style={styles.logMeta}>
            {item.userName} • {formatTimestamp(item.timestamp)}
          </Text>
        </View>
        {item.fromStatus && item.toStatus && (
          <View style={styles.statusTransition}>
            <Text style={styles.statusFrom}>{item.fromStatus}</Text>
            <Ionicons name="arrow-forward" size={12} color={colors.text.muted} />
            <Text style={styles.statusTo}>{item.toStatus}</Text>
          </View>
        )}
      </View>
    </View>
  );
};

export default function AuditLogScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);

  const auditLogs = useTournamentStore((state) => state.auditLogs);
  const auditLogHasMore = useTournamentStore((state) => state.auditLogHasMore);
  const isLoadingAuditLogs = useTournamentStore((state) => state.isLoadingAuditLogs);
  const auditLogFilter = useTournamentStore((state) => state.auditLogFilter);
  const fetchAuditLogs = useTournamentStore((state) => state.fetchAuditLogs);
  const setAuditLogFilter = useTournamentStore((state) => state.setAuditLogFilter);
  const clearAuditLogs = useTournamentStore((state) => state.clearAuditLogs);

  // Load audit logs when screen is focused, clear on unmount
  useFocusEffect(
    useCallback(() => {
      if (id) fetchAuditLogs(id, true);
      return () => {
        clearAuditLogs();
      };
    }, [id, auditLogFilter])
  );

  const handleRefresh = async () => {
    if (!id) return;
    setIsRefreshing(true);
    try {
      await fetchAuditLogs(id, true);
    } finally {
      setIsRefreshing(false);
    }
  };

  const handleLoadMore = async () => {
    if (!id || isLoadingMore || !auditLogHasMore || isLoadingAuditLogs) return;

    setIsLoadingMore(true);
    try {
      await fetchAuditLogs(id, false);
    } finally {
      setIsLoadingMore(false);
    }
  };

  const handleFilterPress = () => {
    const currentFilterLabel =
      ACTION_FILTERS.find((f) => f.value === auditLogFilter)?.label || 'All Activity';

    if (Platform.OS === 'ios') {
      ActionSheetIOS.showActionSheetWithOptions(
        {
          options: ['Cancel', ...ACTION_FILTERS.map((f) => f.label)],
          cancelButtonIndex: 0,
          title: 'Filter Activity',
        },
        (buttonIndex) => {
          if (buttonIndex === 0) return; // Cancel
          const selectedFilter = ACTION_FILTERS[buttonIndex - 1];
          setAuditLogFilter(selectedFilter.value ? { action: selectedFilter.value } : null);
        }
      );
    } else {
      // Android - use Alert.alert with buttons
      Alert.alert(
        'Filter Activity',
        `Current: ${currentFilterLabel}`,
        [
          { text: 'Cancel', style: 'cancel' },
          ...ACTION_FILTERS.map((filter) => ({
            text: filter.label,
            onPress: () => {
              setAuditLogFilter(filter.value ? { action: filter.value } : null);
            },
          })),
        ]
      );
    }
  };

  const renderAuditLog = ({ item }: { item: TournamentAuditLogDto }) => (
    <AuditLogItem item={item} />
  );

  const ListEmptyComponent = () => {
    return (
      <View style={styles.emptyContainer}>
        <EmptyState
          title="No Activity Yet"
          message="No activity has been recorded for this tournament yet."
        />
      </View>
    );
  };

  const ListFooterComponent = () => {
    if (!isLoadingMore) return null;

    return (
      <View style={styles.footerLoading}>
        <ActivityIndicator size="small" color={colors.primary.teal} />
      </View>
    );
  };

  // Show loading spinner only on initial load
  if (isLoadingAuditLogs && auditLogs.length === 0 && !isRefreshing) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Activity Log',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.dark },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={styles.loadingText}>Loading activity...</Text>
        </View>
      </>
    );
  }

  const isFilterActive = auditLogFilter !== null;
  const filterLabel = ACTION_FILTERS.find((f) => f.value === auditLogFilter?.action)?.label || 'All';

  return (
    <View style={styles.container}>
      <Stack.Screen
        options={{
          title: 'Activity Log',
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
          headerRight: () => (
            <TouchableOpacity onPress={handleFilterPress} style={styles.filterButton}>
              <Ionicons
                name="funnel"
                size={20}
                color={isFilterActive ? colors.primary.teal : colors.text.muted}
              />
              {isFilterActive && <View style={styles.filterIndicator} />}
            </TouchableOpacity>
          ),
        }}
      />

      {isFilterActive && (
        <View style={styles.filterBanner}>
          <Text style={styles.filterBannerText}>
            Filtered by: {filterLabel}
          </Text>
          <TouchableOpacity onPress={() => setAuditLogFilter(null)} style={styles.clearFilterButton}>
            <Text style={styles.clearFilterText}>Clear</Text>
          </TouchableOpacity>
        </View>
      )}

      <FlatList
        data={auditLogs}
        keyExtractor={(item) => item.id}
        renderItem={renderAuditLog}
        ListEmptyComponent={ListEmptyComponent}
        ListFooterComponent={ListFooterComponent}
        contentContainerStyle={[
          styles.listContent,
          auditLogs.length === 0 && styles.emptyListContent,
        ]}
        refreshControl={
          <RefreshControl
            refreshing={isRefreshing}
            onRefresh={handleRefresh}
            tintColor={colors.primary.teal}
            colors={[colors.primary.teal]}
          />
        }
        onEndReached={handleLoadMore}
        onEndReachedThreshold={0.5}
        ItemSeparatorComponent={() => <View style={styles.separator} />}
      />
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
  listContent: {
    padding: spacing.md,
  },
  emptyListContent: {
    flex: 1,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingTop: spacing.xl,
  },

  // Filter button in header
  filterButton: {
    marginRight: spacing.sm,
    position: 'relative',
  },
  filterIndicator: {
    position: 'absolute',
    top: -2,
    right: -2,
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: colors.primary.teal,
  },

  // Filter banner
  filterBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.bg.elevated,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  filterBannerText: {
    fontSize: 13,
    color: colors.text.secondary,
    fontWeight: '500',
  },
  clearFilterButton: {
    paddingVertical: spacing.xs,
    paddingHorizontal: spacing.sm,
  },
  clearFilterText: {
    fontSize: 13,
    color: colors.primary.teal,
    fontWeight: '600',
  },

  // Audit log item
  logItem: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  logContent: {
    gap: spacing.xs,
  },
  logDescription: {
    fontSize: 14,
    fontWeight: '500',
    color: colors.text.primary,
    lineHeight: 20,
  },
  logMetaRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  logMeta: {
    fontSize: 12,
    color: colors.text.muted,
  },
  statusTransition: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.xs,
    marginTop: spacing.xs,
    paddingTop: spacing.xs,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },
  statusFrom: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
  },
  statusTo: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.primary.teal,
  },

  // Footer loading
  footerLoading: {
    paddingVertical: spacing.md,
    alignItems: 'center',
  },

  // Separator
  separator: {
    height: spacing.sm,
  },
});
