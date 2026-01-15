import { useCallback, useMemo, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  ActivityIndicator,
  RefreshControl,
  TouchableOpacity,
} from 'react-native';
import { useLocalSearchParams, Stack, useFocusEffect } from 'expo-router';
import { useTournamentStore } from '../../../stores/tournamentStore';
import { Badge, EmptyState } from '../../../components';
import { colors, spacing, radius } from '../../../theme';
import type { TournamentTeamDto, TournamentTeamStatus } from '@bhmhockey/shared';

type SortMode = 'seed' | 'standings';

// Map team status to badge variant
const getStatusBadgeVariant = (status: TournamentTeamStatus) => {
  switch (status) {
    case 'Active':
      return 'green';
    case 'Winner':
      return 'teal';
    case 'Eliminated':
      return 'error';
    case 'Registered':
    case 'Waitlisted':
    default:
      return 'default';
  }
};

// Format goal differential with sign
const formatGoalDiff = (diff: number): string => {
  if (diff > 0) return `+${diff}`;
  return String(diff);
};

export default function TeamsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [sortMode, setSortMode] = useState<SortMode>('seed');
  const [isRefreshing, setIsRefreshing] = useState(false);

  const { teams, currentTournament, fetchTeams, isLoading } = useTournamentStore();

  useFocusEffect(
    useCallback(() => {
      if (id) fetchTeams(id);
    }, [id])
  );

  const handleRefresh = async () => {
    if (!id) return;
    setIsRefreshing(true);
    try {
      await fetchTeams(id);
    } finally {
      setIsRefreshing(false);
    }
  };

  const toggleSortMode = () => {
    setSortMode((prev) => (prev === 'seed' ? 'standings' : 'seed'));
  };

  // Sort teams by seed or standings
  const sortedTeams = useMemo(() => {
    return [...teams].sort((a, b) => {
      if (sortMode === 'seed') {
        // Sort by seed (teams without seed go last)
        if (a.seed === undefined || a.seed === null) return 1;
        if (b.seed === undefined || b.seed === null) return -1;
        return a.seed - b.seed;
      } else {
        // Sort by standings: points first, then wins, then goal differential
        if (a.points !== b.points) return b.points - a.points;
        if (a.wins !== b.wins) return b.wins - a.wins;
        return b.goalDifferential - a.goalDifferential;
      }
    });
  }, [teams, sortMode]);

  const renderTeam = ({ item, index }: { item: TournamentTeamDto; index: number }) => (
    <View style={styles.teamRow}>
      {/* Left section: Seed/Rank and Team Name */}
      <View style={styles.teamLeft}>
        {/* Seed or Rank */}
        <View style={styles.seedContainer}>
          <Text style={styles.seedText}>
            {sortMode === 'seed'
              ? item.seed != null
                ? `#${item.seed}`
                : '-'
              : `#${index + 1}`}
          </Text>
        </View>

        {/* Team Name */}
        <View style={styles.teamInfo}>
          <Text style={styles.teamName} numberOfLines={1}>
            {item.name}
          </Text>
          {item.captainName && (
            <Text style={styles.captainName} numberOfLines={1}>
              Captain: {item.captainName}
            </Text>
          )}
        </View>
      </View>

      {/* Right section: Status, Record, Stats */}
      <View style={styles.teamRight}>
        {/* Status Badge */}
        <Badge variant={getStatusBadgeVariant(item.status)} style={styles.statusBadge}>
          {item.status}
        </Badge>

        {/* Record: W-L-T */}
        <Text style={styles.recordText}>
          {item.wins}-{item.losses}-{item.ties}
        </Text>

        {/* Points (for round robin) */}
        {currentTournament?.format === 'RoundRobin' && (
          <View style={styles.pointsContainer}>
            <Text style={styles.pointsValue}>{item.points}</Text>
            <Text style={styles.pointsLabel}>pts</Text>
          </View>
        )}

        {/* Goal Differential */}
        <Text
          style={[
            styles.goalDiff,
            item.goalDifferential > 0 && styles.goalDiffPositive,
            item.goalDifferential < 0 && styles.goalDiffNegative,
          ]}
        >
          {formatGoalDiff(item.goalDifferential)}
        </Text>
      </View>
    </View>
  );

  const ListHeaderComponent = () => (
    <View style={styles.listHeader}>
      <View style={styles.headerLeft}>
        <Text style={styles.headerText}>Team</Text>
      </View>
      <View style={styles.headerRight}>
        <Text style={styles.headerText}>Status</Text>
        <Text style={[styles.headerText, styles.recordHeader]}>W-L-T</Text>
        {currentTournament?.format === 'RoundRobin' && (
          <Text style={[styles.headerText, styles.pointsHeader]}>Pts</Text>
        )}
        <Text style={[styles.headerText, styles.goalDiffHeader]}>+/-</Text>
      </View>
    </View>
  );

  const ListEmptyComponent = () => (
    <View style={styles.emptyContainer}>
      <EmptyState
        title="No Teams Yet"
        message="No teams have registered for this tournament yet."
      />
    </View>
  );

  const headerTitle = `Teams (${teams.length})`;

  // Show loading spinner only on initial load (not during refresh)
  if (isLoading && teams.length === 0) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Teams',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.dark },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={styles.loadingText}>Loading teams...</Text>
        </View>
      </>
    );
  }

  return (
    <View style={styles.container}>
      <Stack.Screen
        options={{
          title: headerTitle,
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
          headerRight: () => (
            <TouchableOpacity onPress={toggleSortMode} style={styles.sortButton}>
              <Text style={styles.sortButtonText}>
                {sortMode === 'seed' ? 'By Standings' : 'By Seed'}
              </Text>
            </TouchableOpacity>
          ),
        }}
      />

      <FlatList
        data={sortedTeams}
        keyExtractor={(item) => item.id}
        renderItem={renderTeam}
        ListHeaderComponent={teams.length > 0 ? ListHeaderComponent : null}
        ListEmptyComponent={ListEmptyComponent}
        contentContainerStyle={[
          styles.listContent,
          teams.length === 0 && styles.emptyListContent,
        ]}
        refreshControl={
          <RefreshControl
            refreshing={isRefreshing}
            onRefresh={handleRefresh}
            tintColor={colors.primary.teal}
            colors={[colors.primary.teal]}
          />
        }
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
    justifyContent: 'center',
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  sortButton: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
  },
  sortButtonText: {
    color: colors.primary.teal,
    fontSize: 14,
    fontWeight: '600',
  },

  // List header
  listHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.sm,
    marginBottom: spacing.xs,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  headerLeft: {
    flex: 1,
  },
  headerRight: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
  },
  headerText: {
    fontSize: 11,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  recordHeader: {
    width: 40,
    textAlign: 'center',
  },
  pointsHeader: {
    width: 30,
    textAlign: 'center',
  },
  goalDiffHeader: {
    width: 32,
    textAlign: 'center',
  },

  // Team row
  teamRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  teamLeft: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    marginRight: spacing.sm,
  },
  seedContainer: {
    width: 32,
    height: 32,
    borderRadius: radius.md,
    backgroundColor: colors.bg.elevated,
    justifyContent: 'center',
    alignItems: 'center',
  },
  seedText: {
    fontSize: 12,
    fontWeight: '700',
    color: colors.text.secondary,
  },
  teamInfo: {
    flex: 1,
  },
  teamName: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.text.primary,
  },
  captainName: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: 2,
  },
  teamRight: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
  },
  statusBadge: {
    minWidth: 70,
  },
  recordText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
    width: 40,
    textAlign: 'center',
  },
  pointsContainer: {
    flexDirection: 'row',
    alignItems: 'baseline',
    width: 30,
    justifyContent: 'center',
  },
  pointsValue: {
    fontSize: 14,
    fontWeight: '700',
    color: colors.primary.teal,
  },
  pointsLabel: {
    fontSize: 10,
    color: colors.text.muted,
    marginLeft: 2,
  },
  goalDiff: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
    width: 32,
    textAlign: 'center',
  },
  goalDiffPositive: {
    color: colors.status.success,
  },
  goalDiffNegative: {
    color: colors.status.error,
  },
  separator: {
    height: spacing.sm,
  },
});
