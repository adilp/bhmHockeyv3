import { useCallback, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { useLocalSearchParams, Stack, useFocusEffect } from 'expo-router';
import { useTournamentStore } from '../../../stores/tournamentStore';
import { Badge, EmptyState } from '../../../components';
import { colors, spacing, radius } from '../../../theme';
import type { TeamStandingDto } from '@bhmhockey/shared';
import { Ionicons } from '@expo/vector-icons';
import { useAuthStore } from '../../../stores/authStore';

// Format goal differential with sign
const formatGoalDiff = (diff: number): string => {
  if (diff > 0) return `+${diff}`;
  return String(diff);
};

export default function StandingsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [isRefreshing, setIsRefreshing] = useState(false);

  const {
    standings,
    playoffCutoff,
    tiedGroups,
    fetchStandings,
    isLoading,
    myRegistration,
  } = useTournamentStore();

  const user = useAuthStore((state) => state.user);

  // Fetch standings when screen focuses
  useFocusEffect(
    useCallback(() => {
      if (id) fetchStandings(id);
    }, [id])
  );

  const handleRefresh = async () => {
    if (!id) return;
    setIsRefreshing(true);
    try {
      await fetchStandings(id);
    } finally {
      setIsRefreshing(false);
    }
  };

  // Get user's team ID from registration or by checking team membership
  const getUserTeamId = (): string | null => {
    if (myRegistration?.assignedTeamId) {
      return myRegistration.assignedTeamId;
    }
    // Could also check teams list if needed
    return null;
  };

  const userTeamId = getUserTeamId();

  // Check if a team is part of tied groups
  const isTeamInTiedGroup = (teamId: string): boolean => {
    if (!tiedGroups || tiedGroups.length === 0) return false;
    return tiedGroups.some((group) => group.teamIds.includes(teamId));
  };

  // Get all teams with same rank as given team
  const getTeamsWithSameRank = (rank: number): TeamStandingDto[] => {
    return standings.filter((s) => s.rank === rank);
  };

  const renderStandingRow = ({
    item,
    index,
  }: {
    item: TeamStandingDto;
    index: number;
  }) => {
    const isUserTeam = item.teamId === userTeamId;
    const teamsWithSameRank = getTeamsWithSameRank(item.rank);
    const isTied = teamsWithSameRank.length > 1;
    const isInTiedGroup = isTeamInTiedGroup(item.teamId);

    // Show playoff cutoff line if:
    // 1. This is the first non-playoff team
    // 2. No tied groups exist (ambiguous if ties span cutoff)
    const showCutoffLine =
      playoffCutoff !== null &&
      index > 0 &&
      item.rank === playoffCutoff + 1 &&
      (!tiedGroups || tiedGroups.length === 0);

    return (
      <>
        {showCutoffLine && <View style={styles.playoffCutoffLine} />}
        <View
          style={[
            styles.standingRow,
            isUserTeam && styles.userTeamRow,
            isInTiedGroup && styles.tiedGroupRow,
          ]}
        >
          {/* Rank Column */}
          <View style={styles.rankContainer}>
            <View style={styles.rankInner}>
              <Text style={styles.rankText}>{item.rank}</Text>
              {isTied && (
                <Ionicons
                  name="link"
                  size={10}
                  color={colors.text.muted}
                  style={styles.tiedIcon}
                />
              )}
            </View>
          </View>

          {/* Team Name Column */}
          <View style={styles.teamNameContainer}>
            <Text
              style={[styles.teamNameText, isUserTeam && styles.userTeamText]}
              numberOfLines={1}
            >
              {item.teamName}
            </Text>
          </View>

          {/* Stats Columns */}
          <Text style={styles.statText}>{item.wins}</Text>
          <Text style={styles.statText}>{item.losses}</Text>
          <Text style={styles.statText}>{item.ties}</Text>
          <Text style={[styles.statText, styles.pointsText]}>{item.points}</Text>
          <Text
            style={[
              styles.statText,
              item.goalDifferential > 0 && styles.goalDiffPositive,
              item.goalDifferential < 0 && styles.goalDiffNegative,
            ]}
          >
            {formatGoalDiff(item.goalDifferential)}
          </Text>
          <Text style={styles.statText}>{item.gamesPlayed}</Text>
        </View>
      </>
    );
  };

  const ListHeaderComponent = () => (
    <>
      {/* Tied Groups Alert Banner */}
      {tiedGroups && tiedGroups.length > 0 && (
        <View style={styles.tiedGroupsAlert}>
          <Ionicons
            name="warning"
            size={18}
            color={colors.status.warning}
            style={styles.alertIcon}
          />
          <View style={styles.alertTextContainer}>
            <Text style={styles.alertTitle}>Unresolved Ties</Text>
            <Text style={styles.alertMessage}>
              Some teams are tied. Organizers may need to resolve manually.
            </Text>
          </View>
        </View>
      )}

      {/* Column Headers */}
      <View style={styles.headerRow}>
        <View style={styles.rankContainer}>
          <Text style={styles.headerText}>#</Text>
        </View>
        <View style={styles.teamNameContainer}>
          <Text style={styles.headerText}>Team</Text>
        </View>
        <Text style={styles.headerText}>W</Text>
        <Text style={styles.headerText}>L</Text>
        <Text style={styles.headerText}>T</Text>
        <Text style={styles.headerText}>PTS</Text>
        <Text style={styles.headerText}>+/-</Text>
        <Text style={styles.headerText}>GP</Text>
      </View>
    </>
  );

  const ListFooterComponent = () => {
    if (standings.length === 0) return null;

    return (
      <View style={styles.footer}>
        <View style={styles.legendContainer}>
          <Text style={styles.legendTitle}>Legend</Text>
          <View style={styles.legendRow}>
            <Ionicons
              name="link"
              size={12}
              color={colors.text.muted}
              style={styles.legendIcon}
            />
            <Text style={styles.legendText}>= Tied teams (same record)</Text>
          </View>
          {playoffCutoff !== null && (!tiedGroups || tiedGroups.length === 0) && (
            <View style={styles.legendRow}>
              <View style={styles.legendDashedLine} />
              <Text style={styles.legendText}>= Playoff cutoff line</Text>
            </View>
          )}
        </View>

        <View style={styles.tiebreakerContainer}>
          <Text style={styles.tiebreakerTitle}>Tiebreakers</Text>
          <Text style={styles.tiebreakerText}>1) Head-to-head record</Text>
          <Text style={styles.tiebreakerText}>2) Goal differential</Text>
          <Text style={styles.tiebreakerText}>3) Goals scored</Text>
        </View>
      </View>
    );
  };

  const ListEmptyComponent = () => (
    <View style={styles.emptyContainer}>
      <EmptyState
        title="No Standings Yet"
        message="Standings will appear once games have been played."
      />
    </View>
  );

  const headerTitle = `Standings${standings.length > 0 ? ` (${standings.length})` : ''}`;

  // Show loading spinner only on initial load (not during refresh)
  if (isLoading && standings.length === 0) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Standings',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.dark },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={styles.loadingText}>Loading standings...</Text>
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
        }}
      />

      <FlatList
        data={standings}
        keyExtractor={(item) => item.teamId}
        renderItem={renderStandingRow}
        ListHeaderComponent={standings.length > 0 ? ListHeaderComponent : null}
        ListFooterComponent={ListFooterComponent}
        ListEmptyComponent={ListEmptyComponent}
        contentContainerStyle={[
          styles.listContent,
          standings.length === 0 && styles.emptyListContent,
        ]}
        refreshControl={
          <RefreshControl
            refreshing={isRefreshing}
            onRefresh={handleRefresh}
            tintColor={colors.primary.teal}
            colors={[colors.primary.teal]}
          />
        }
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

  // Tied Groups Alert Banner
  tiedGroupsAlert: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    backgroundColor: colors.status.warningSubtle,
    borderWidth: 1,
    borderColor: colors.status.warning,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.md,
  },
  alertIcon: {
    marginTop: 2,
  },
  alertTextContainer: {
    flex: 1,
    marginLeft: spacing.sm,
  },
  alertTitle: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.status.warning,
    marginBottom: spacing.xs,
  },
  alertMessage: {
    fontSize: 13,
    color: colors.text.secondary,
    lineHeight: 18,
  },

  // Header Row
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.sm,
    marginBottom: spacing.xs,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  headerText: {
    fontSize: 11,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    width: 32,
    textAlign: 'center',
  },

  // Standing Row
  standingRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bg.dark,
    padding: spacing.sm,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    marginBottom: spacing.xs,
  },
  userTeamRow: {
    backgroundColor: colors.subtle.teal,
    borderColor: colors.primary.teal,
    borderWidth: 2,
  },
  tiedGroupRow: {
    // Could add special styling for tied groups if needed
  },

  // Rank Column
  rankContainer: {
    width: 40,
    marginRight: spacing.xs,
  },
  rankInner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
  },
  rankText: {
    fontSize: 14,
    fontWeight: '700',
    color: colors.text.secondary,
  },
  tiedIcon: {
    marginLeft: 2,
  },

  // Team Name Column
  teamNameContainer: {
    flex: 1,
    marginRight: spacing.xs,
  },
  teamNameText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.primary,
  },
  userTeamText: {
    color: colors.primary.teal,
    fontWeight: '700',
  },

  // Stats Columns
  statText: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.secondary,
    width: 32,
    textAlign: 'center',
  },
  pointsText: {
    color: colors.primary.teal,
    fontWeight: '700',
  },
  goalDiffPositive: {
    color: colors.status.success,
  },
  goalDiffNegative: {
    color: colors.status.error,
  },

  // Playoff Cutoff Line
  playoffCutoffLine: {
    height: 2,
    backgroundColor: colors.border.emphasis,
    marginVertical: spacing.sm,
    marginHorizontal: spacing.sm,
    borderRadius: 1,
    // Dashed effect using border
    borderStyle: 'dashed',
    borderWidth: 1,
    borderColor: colors.border.emphasis,
  },

  // Footer
  footer: {
    marginTop: spacing.lg,
    paddingTop: spacing.lg,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
  },

  // Legend
  legendContainer: {
    marginBottom: spacing.lg,
  },
  legendTitle: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.sm,
  },
  legendRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.xs,
  },
  legendIcon: {
    marginRight: spacing.xs,
  },
  legendDashedLine: {
    width: 16,
    height: 2,
    backgroundColor: colors.border.emphasis,
    borderStyle: 'dashed',
    borderWidth: 1,
    borderColor: colors.border.emphasis,
    borderRadius: 1,
    marginRight: spacing.xs,
  },
  legendText: {
    fontSize: 12,
    color: colors.text.muted,
  },

  // Tiebreaker Info
  tiebreakerContainer: {
    backgroundColor: colors.bg.elevated,
    padding: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  tiebreakerTitle: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.sm,
  },
  tiebreakerText: {
    fontSize: 13,
    color: colors.text.secondary,
    lineHeight: 20,
  },
});
