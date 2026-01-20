import { useState, useCallback, useMemo } from 'react';
import {
  View,
  Text,
  StyleSheet,
  SectionList,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { useLocalSearchParams, useRouter, useFocusEffect, Stack } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useShallow } from 'zustand/react/shallow';

import { useTournamentStore } from '../../../stores/tournamentStore';
import { useAuthStore } from '../../../stores/authStore';
import { colors, spacing, radius } from '../../../theme';
import type { TournamentMatchDto } from '@bhmhockey/shared';

/**
 * ScheduleGameCard - Display a single match with teams, scores, time, and venue
 */
interface ScheduleGameCardProps {
  match: TournamentMatchDto;
  onPress?: () => void;
  canEdit?: boolean;
}

function ScheduleGameCard({ match, onPress, canEdit = false }: ScheduleGameCardProps) {
  const isCompleted = match.status === 'Completed' || match.status === 'Forfeit';
  const homeIsWinner = isCompleted && match.winnerTeamId === match.homeTeamId;
  const awayIsWinner = isCompleted && match.winnerTeamId === match.awayTeamId;

  // Format scheduled time
  const formattedTime = match.scheduledTime
    ? new Date(match.scheduledTime).toLocaleString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
      })
    : 'TBD';

  // Status indicator
  const getStatusColor = () => {
    switch (match.status) {
      case 'Completed':
        return colors.status.success;
      case 'InProgress':
        return colors.status.error;
      case 'Forfeit':
        return colors.status.warning;
      default:
        return colors.text.muted;
    }
  };

  const getStatusLabel = () => {
    switch (match.status) {
      case 'Completed':
        return 'Final';
      case 'InProgress':
        return 'Live';
      case 'Forfeit':
        return 'Forfeit';
      default:
        return 'Scheduled';
    }
  };

  const content = (
    <View style={styles.gameCard}>
      {/* Header: Match number + Status */}
      <View style={styles.gameHeader}>
        <Text style={styles.matchNumber}>Match {match.matchNumber}</Text>
        <View style={[styles.statusBadge, { backgroundColor: getStatusColor() + '20' }]}>
          <Text style={[styles.statusText, { color: getStatusColor() }]}>
            {getStatusLabel()}
          </Text>
        </View>
      </View>

      {/* Teams and Scores */}
      <View style={styles.teamsContainer}>
        {/* Home Team */}
        <View style={[styles.teamRow, homeIsWinner && styles.winnerRow]}>
          <Text
            style={[
              styles.teamName,
              homeIsWinner && styles.winnerText,
              !match.homeTeamName && styles.tbdText,
            ]}
            numberOfLines={1}
          >
            {match.homeTeamName || 'TBD'}
          </Text>
          {isCompleted && (
            <Text style={[styles.score, homeIsWinner && styles.winnerScore]}>
              {match.homeScore ?? '-'}
            </Text>
          )}
        </View>

        {/* VS Divider */}
        <View style={styles.vsDivider}>
          <Text style={styles.vsText}>vs</Text>
        </View>

        {/* Away Team */}
        <View style={[styles.teamRow, awayIsWinner && styles.winnerRow]}>
          <Text
            style={[
              styles.teamName,
              awayIsWinner && styles.winnerText,
              !match.awayTeamName && styles.tbdText,
            ]}
            numberOfLines={1}
          >
            {match.awayTeamName || 'TBD'}
          </Text>
          {isCompleted && (
            <Text style={[styles.score, awayIsWinner && styles.winnerScore]}>
              {match.awayScore ?? '-'}
            </Text>
          )}
        </View>
      </View>

      {/* Time and Venue */}
      <View style={styles.scheduleInfo}>
        <View style={styles.infoRow}>
          <Ionicons name="time-outline" size={12} color={colors.text.muted} />
          <Text style={styles.infoText}>{formattedTime}</Text>
        </View>
        {match.venue && (
          <View style={styles.infoRow}>
            <Ionicons name="location-outline" size={12} color={colors.text.muted} />
            <Text style={styles.infoText} numberOfLines={1}>
              {match.venue}
            </Text>
          </View>
        )}
      </View>
    </View>
  );

  if (onPress && canEdit) {
    return (
      <TouchableOpacity onPress={onPress} activeOpacity={0.7}>
        {content}
      </TouchableOpacity>
    );
  }

  return content;
}

/**
 * Get round name based on format and round number
 */
function getRoundName(round: number, totalRounds: number, isElimination: boolean): string {
  if (!isElimination) {
    return `Round ${round}`;
  }

  // For elimination brackets, use traditional names
  const fromFinal = totalRounds - round;
  if (fromFinal === 0) return 'Final';
  if (fromFinal === 1) return 'Semifinals';
  if (fromFinal === 2) return 'Quarterfinals';
  return `Round ${round}`;
}

interface RoundSection {
  title: string;
  round: number;
  data: TournamentMatchDto[];
}

export default function ScheduleScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const [refreshing, setRefreshing] = useState(false);
  const [showMyGamesOnly, setShowMyGamesOnly] = useState(false);

  const user = useAuthStore((state) => state.user);

  const {
    matches,
    currentTournament,
    myRegistration,
    fetchMatches,
    isLoading,
  } = useTournamentStore(useShallow((state) => ({
    matches: state.matches,
    currentTournament: state.currentTournament,
    myRegistration: state.myRegistration,
    fetchMatches: state.fetchMatches,
    isLoading: state.isLoading,
  })));

  // Fetch matches on focus
  useFocusEffect(
    useCallback(() => {
      if (id) {
        fetchMatches(id);
      }
    }, [id, fetchMatches])
  );

  // Determine user's team ID for "My Games" filter
  // Uses myRegistration.assignedTeamId which is set when user is on a team
  const userTeamId = useMemo(() => {
    if (!user) return null;

    // Use assignedTeamId from registration - this is populated when user is on a team
    if (myRegistration?.assignedTeamId) {
      return myRegistration.assignedTeamId;
    }

    return null;
  }, [user, myRegistration]);

  // Calculate stats
  const stats = useMemo(() => {
    const total = matches.length;
    const completed = matches.filter(
      (m) => m.status === 'Completed' || m.status === 'Forfeit'
    ).length;
    const remaining = total - completed;

    return { total, completed, remaining };
  }, [matches]);

  // Calculate total rounds for naming
  const totalRounds = useMemo(() => {
    if (matches.length === 0) return 0;
    return Math.max(...matches.map((m) => m.round));
  }, [matches]);

  // Determine if elimination format
  const isElimination = useMemo(() => {
    return (
      currentTournament?.format === 'SingleElimination' ||
      currentTournament?.format === 'DoubleElimination'
    );
  }, [currentTournament?.format]);

  // Filter matches based on "My Games" toggle
  const filteredMatches = useMemo(() => {
    if (!showMyGamesOnly || !userTeamId) {
      return matches;
    }

    return matches.filter(
      (m) => m.homeTeamId === userTeamId || m.awayTeamId === userTeamId
    );
  }, [matches, showMyGamesOnly, userTeamId]);

  // Group matches by round into sections
  const sections: RoundSection[] = useMemo(() => {
    const grouped = filteredMatches.reduce((acc, match) => {
      const key = match.round;
      if (!acc[key]) {
        acc[key] = [];
      }
      acc[key].push(match);
      return acc;
    }, {} as Record<number, TournamentMatchDto[]>);

    return Object.entries(grouped)
      .map(([round, roundMatches]) => ({
        title: getRoundName(Number(round), totalRounds, isElimination),
        round: Number(round),
        data: roundMatches.sort((a, b) => {
          // Sort by scheduled time if available, otherwise by match number
          if (a.scheduledTime && b.scheduledTime) {
            return (
              new Date(a.scheduledTime).getTime() - new Date(b.scheduledTime).getTime()
            );
          }
          return a.matchNumber - b.matchNumber;
        }),
      }))
      .sort((a, b) => a.round - b.round);
  }, [filteredMatches, totalRounds, isElimination]);

  // Handle pull-to-refresh
  const handleRefresh = useCallback(async () => {
    if (!id) return;
    setRefreshing(true);
    await fetchMatches(id);
    setRefreshing(false);
  }, [id, fetchMatches]);

  // Handle match press (for score entry)
  const handleMatchPress = useCallback(
    (match: TournamentMatchDto) => {
      if (!currentTournament?.canManage) return;
      // Navigate to score entry screen
      router.push(`/tournaments/${id}/match/${match.id}`);
    },
    [currentTournament?.canManage, router, id]
  );

  // Render section header
  const renderSectionHeader = useCallback(
    ({ section }: { section: RoundSection }) => (
      <View style={styles.sectionHeader}>
        <Text style={styles.sectionTitle}>{section.title}</Text>
        <Text style={styles.matchCount}>
          {section.data.length} {section.data.length === 1 ? 'game' : 'games'}
        </Text>
      </View>
    ),
    []
  );

  // Render match item
  const renderMatchItem = useCallback(
    ({ item }: { item: TournamentMatchDto }) => (
      <View style={styles.matchContainer}>
        <ScheduleGameCard
          match={item}
          onPress={() => handleMatchPress(item)}
          canEdit={currentTournament?.canManage ?? false}
        />
      </View>
    ),
    [handleMatchPress, currentTournament?.canManage]
  );

  // Render list header with stats and filter
  const renderListHeader = useCallback(() => {
    if (matches.length === 0) return null;

    return (
      <View style={styles.listHeader}>
        {/* Stats Card */}
        <View style={styles.statsCard}>
          <View style={styles.statItem}>
            <Text style={styles.statValue}>{stats.total}</Text>
            <Text style={styles.statLabel}>Total Games</Text>
          </View>
          <View style={styles.statDivider} />
          <View style={styles.statItem}>
            <Text style={styles.statValue}>{stats.completed}</Text>
            <Text style={styles.statLabel}>Completed</Text>
          </View>
          <View style={styles.statDivider} />
          <View style={styles.statItem}>
            <Text style={[styles.statValue, { color: colors.primary.teal }]}>
              {stats.remaining}
            </Text>
            <Text style={styles.statLabel}>Remaining</Text>
          </View>
        </View>

        {/* My Games Filter Toggle (only show if user has a team) */}
        {userTeamId && (
          <TouchableOpacity
            style={[styles.filterToggle, showMyGamesOnly && styles.filterToggleActive]}
            onPress={() => setShowMyGamesOnly(!showMyGamesOnly)}
            activeOpacity={0.7}
          >
            <Ionicons
              name={showMyGamesOnly ? 'checkbox' : 'square-outline'}
              size={20}
              color={showMyGamesOnly ? colors.primary.teal : colors.text.muted}
            />
            <Text
              style={[
                styles.filterToggleText,
                showMyGamesOnly && styles.filterToggleTextActive,
              ]}
            >
              Show My Games Only
            </Text>
          </TouchableOpacity>
        )}
      </View>
    );
  }, [matches.length, stats, userTeamId, showMyGamesOnly]);

  // Render empty state
  const renderEmptyState = () => {
    if (showMyGamesOnly && userTeamId) {
      return (
        <View style={styles.emptyContainer}>
          <Ionicons name="calendar-outline" size={64} color={colors.text.muted} />
          <Text style={styles.emptyTitle}>No Games Found</Text>
          <Text style={styles.emptySubtitle}>
            Your team doesn't have any games scheduled yet.
          </Text>
        </View>
      );
    }

    return (
      <View style={styles.emptyContainer}>
        <Ionicons name="calendar-outline" size={64} color={colors.text.muted} />
        <Text style={styles.emptyTitle}>No Schedule Yet</Text>
        <Text style={styles.emptySubtitle}>
          The schedule will appear here once games have been scheduled.
        </Text>
      </View>
    );
  };

  // Show loading only on initial load
  const showLoading = isLoading && matches.length === 0;

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: 'Schedule' }} />

      {/* Content */}
      {showLoading ? (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      ) : filteredMatches.length === 0 ? (
        <View style={styles.emptyWrapper}>
          {renderListHeader()}
          {renderEmptyState()}
        </View>
      ) : (
        <SectionList
          sections={sections}
          keyExtractor={(item) => item.id}
          renderItem={renderMatchItem}
          renderSectionHeader={renderSectionHeader}
          ListHeaderComponent={renderListHeader}
          stickySectionHeadersEnabled={false}
          contentContainerStyle={styles.listContent}
          showsVerticalScrollIndicator={false}
          refreshControl={
            <RefreshControl
              refreshing={refreshing}
              onRefresh={handleRefresh}
              tintColor={colors.primary.teal}
              colors={[colors.primary.teal]}
            />
          }
          ListFooterComponent={<View style={styles.listFooter} />}
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },

  // List header styles
  listHeader: {
    padding: spacing.md,
    gap: spacing.md,
  },

  // Stats card styles
  statsCard: {
    flexDirection: 'row',
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
    padding: spacing.md,
    justifyContent: 'space-around',
  },
  statItem: {
    alignItems: 'center',
  },
  statValue: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text.primary,
  },
  statLabel: {
    fontSize: 11,
    color: colors.text.muted,
    marginTop: spacing.xs,
    textTransform: 'uppercase',
  },
  statDivider: {
    width: 1,
    backgroundColor: colors.border.default,
  },

  // Filter toggle styles
  filterToggle: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bg.dark,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    padding: spacing.sm,
    gap: spacing.sm,
  },
  filterToggleActive: {
    backgroundColor: colors.subtle.teal,
    borderColor: colors.primary.teal,
  },
  filterToggleText: {
    fontSize: 14,
    fontWeight: '500',
    color: colors.text.muted,
  },
  filterToggleTextActive: {
    color: colors.primary.teal,
    fontWeight: '600',
  },

  // Section styles
  sectionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.md,
    paddingTop: spacing.lg,
    paddingBottom: spacing.sm,
    backgroundColor: colors.bg.darkest,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '700',
    color: colors.text.primary,
  },
  matchCount: {
    fontSize: 12,
    color: colors.text.muted,
  },

  // Match list styles
  listContent: {
    paddingBottom: spacing.xl,
  },
  matchContainer: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
  listFooter: {
    height: spacing.xxl,
  },

  // Game card styles
  gameCard: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
    overflow: 'hidden',
  },
  gameHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    backgroundColor: colors.bg.elevated,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  matchNumber: {
    fontSize: 11,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
  },
  statusBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: 2,
    borderRadius: radius.sm,
  },
  statusText: {
    fontSize: 10,
    fontWeight: '700',
    textTransform: 'uppercase',
  },

  // Teams container
  teamsContainer: {
    padding: spacing.sm,
  },
  teamRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.sm,
    borderRadius: radius.sm,
  },
  winnerRow: {
    backgroundColor: colors.subtle.green,
  },
  teamName: {
    fontSize: 14,
    fontWeight: '500',
    color: colors.text.secondary,
    flex: 1,
    marginRight: spacing.sm,
  },
  winnerText: {
    color: colors.primary.green,
    fontWeight: '700',
  },
  tbdText: {
    color: colors.text.subtle,
    fontStyle: 'italic',
  },
  score: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.secondary,
    minWidth: 32,
    textAlign: 'right',
  },
  winnerScore: {
    color: colors.primary.green,
    fontWeight: '700',
  },

  // VS divider
  vsDivider: {
    alignItems: 'center',
    paddingVertical: spacing.xs,
  },
  vsText: {
    fontSize: 10,
    fontWeight: '600',
    color: colors.text.subtle,
    textTransform: 'uppercase',
  },

  // Schedule info
  scheduleInfo: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    backgroundColor: colors.bg.elevated,
    borderTopWidth: 1,
    borderTopColor: colors.border.default,
    gap: spacing.xs,
  },
  infoRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.xs,
  },
  infoText: {
    fontSize: 11,
    color: colors.text.muted,
    flex: 1,
  },

  // Loading styles
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },

  // Empty state styles
  emptyWrapper: {
    flex: 1,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: spacing.xl,
  },
  emptyTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.primary,
    marginTop: spacing.md,
    marginBottom: spacing.sm,
  },
  emptySubtitle: {
    fontSize: 14,
    color: colors.text.muted,
    textAlign: 'center',
    lineHeight: 20,
  },
});
