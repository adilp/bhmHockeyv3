import { useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
} from 'react-native';
import { useLocalSearchParams, useRouter, useFocusEffect, Stack } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useShallow } from 'zustand/react/shallow';

import { useTournamentStore } from '../../../../stores/tournamentStore';
import { colors, spacing, radius } from '../../../../theme';

/**
 * Match Detail Screen
 * Displays detailed information about a specific tournament match
 */
export default function MatchDetailScreen() {
  const { id, matchId } = useLocalSearchParams<{ id: string; matchId: string }>();
  const router = useRouter();

  const {
    currentTournament,
    matches,
    fetchTournamentById,
    fetchMatches,
    isLoading,
  } = useTournamentStore(useShallow((state) => ({
    currentTournament: state.currentTournament,
    matches: state.matches,
    fetchTournamentById: state.fetchTournamentById,
    fetchMatches: state.fetchMatches,
    isLoading: state.isLoading,
  })));

  // Find the specific match
  const match = matches.find((m) => m.id === matchId);

  // Fetch tournament and match data on focus
  useFocusEffect(
    useCallback(() => {
      if (id) {
        fetchTournamentById(id);
      }
    }, [id, fetchTournamentById])
  );

  // Handle refresh
  const handleRefresh = useCallback(async () => {
    if (!id) return;
    await Promise.all([fetchTournamentById(id), fetchMatches(id)]);
  }, [id, fetchTournamentById, fetchMatches]);

  // Handle navigation to bracket
  const handleViewBracket = () => {
    if (!id) return;
    router.push(`/tournaments/${id}/bracket`);
  };

  // Handle navigation to schedule
  const handleViewSchedule = () => {
    if (!id) return;
    router.push(`/tournaments/${id}/schedule`);
  };

  // Handle navigation to score entry (for admins)
  const handleEnterScore = () => {
    if (!id || !matchId) return;
    router.push(`/tournaments/${id}/manage/score?matchId=${matchId}`);
  };

  // Format scheduled time
  const formattedTime = match?.scheduledTime
    ? new Date(match.scheduledTime).toLocaleString('en-US', {
        weekday: 'long',
        month: 'long',
        day: 'numeric',
        year: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
      })
    : 'TBD';

  // Status indicator helpers
  const getStatusColor = () => {
    if (!match) return colors.text.muted;
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
    if (!match) return 'Unknown';
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

  // Check if match is completed
  const isCompleted = match?.status === 'Completed' || match?.status === 'Forfeit';
  const homeIsWinner = isCompleted && match?.winnerTeamId === match?.homeTeamId;
  const awayIsWinner = isCompleted && match?.winnerTeamId === match?.awayTeamId;

  // Show loading state
  if (isLoading && !match) {
    return (
      <View style={styles.container}>
        <Stack.Screen options={{ title: 'Match Details' }} />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      </View>
    );
  }

  // Show error if match not found
  if (!match) {
    return (
      <View style={styles.container}>
        <Stack.Screen options={{ title: 'Match Details' }} />
        <View style={styles.emptyContainer}>
          <Ionicons name="alert-circle-outline" size={64} color={colors.text.muted} />
          <Text style={styles.emptyTitle}>Match Not Found</Text>
          <Text style={styles.emptySubtitle}>
            This match could not be found or may have been removed.
          </Text>
          <TouchableOpacity style={styles.retryButton} onPress={() => router.back()}>
            <Text style={styles.retryButtonText}>Go Back</Text>
          </TouchableOpacity>
        </View>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Stack.Screen options={{ title: 'Match Details' }} />

      {/* Content */}
      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.scrollContent}
        showsVerticalScrollIndicator={false}
        refreshControl={
          <RefreshControl
            refreshing={false}
            onRefresh={handleRefresh}
            tintColor={colors.primary.teal}
            colors={[colors.primary.teal]}
          />
        }
      >
        {/* Match Card */}
        <View style={styles.matchCard}>
          {/* Header: Match number + Status */}
          <View style={styles.matchHeader}>
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
              <View style={styles.teamInfo}>
                <Text style={styles.teamLabel}>HOME</Text>
                <Text
                  style={[
                    styles.teamName,
                    homeIsWinner && styles.winnerText,
                    !match.homeTeamName && styles.tbdText,
                  ]}
                  numberOfLines={2}
                >
                  {match.homeTeamName || 'TBD'}
                </Text>
              </View>
              {isCompleted && (
                <Text style={[styles.score, homeIsWinner && styles.winnerScore]}>
                  {match.homeScore ?? '-'}
                </Text>
              )}
            </View>

            {/* VS Divider */}
            <View style={styles.vsDivider}>
              <View style={styles.vsLine} />
              <Text style={styles.vsText}>vs</Text>
              <View style={styles.vsLine} />
            </View>

            {/* Away Team */}
            <View style={[styles.teamRow, awayIsWinner && styles.winnerRow]}>
              <View style={styles.teamInfo}>
                <Text style={styles.teamLabel}>AWAY</Text>
                <Text
                  style={[
                    styles.teamName,
                    awayIsWinner && styles.winnerText,
                    !match.awayTeamName && styles.tbdText,
                  ]}
                  numberOfLines={2}
                >
                  {match.awayTeamName || 'TBD'}
                </Text>
              </View>
              {isCompleted && (
                <Text style={[styles.score, awayIsWinner && styles.winnerScore]}>
                  {match.awayScore ?? '-'}
                </Text>
              )}
            </View>
          </View>

          {/* Match Details */}
          <View style={styles.detailsContainer}>
            {/* Time */}
            <View style={styles.detailRow}>
              <View style={styles.detailIcon}>
                <Ionicons name="time-outline" size={20} color={colors.primary.teal} />
              </View>
              <View style={styles.detailContent}>
                <Text style={styles.detailLabel}>Scheduled Time</Text>
                <Text style={styles.detailValue}>{formattedTime}</Text>
              </View>
            </View>

            {/* Venue */}
            {match.venue && (
              <View style={styles.detailRow}>
                <View style={styles.detailIcon}>
                  <Ionicons name="location-outline" size={20} color={colors.primary.teal} />
                </View>
                <View style={styles.detailContent}>
                  <Text style={styles.detailLabel}>Venue</Text>
                  <Text style={styles.detailValue}>{match.venue}</Text>
                </View>
              </View>
            )}

            {/* Round */}
            <View style={styles.detailRow}>
              <View style={styles.detailIcon}>
                <Ionicons name="trophy-outline" size={20} color={colors.primary.teal} />
              </View>
              <View style={styles.detailContent}>
                <Text style={styles.detailLabel}>Round</Text>
                <Text style={styles.detailValue}>Round {match.round}</Text>
              </View>
            </View>

            {/* Bracket Type (for double elimination) */}
            {match.bracketType && (
              <View style={styles.detailRow}>
                <View style={styles.detailIcon}>
                  <Ionicons name="git-branch-outline" size={20} color={colors.primary.teal} />
                </View>
                <View style={styles.detailContent}>
                  <Text style={styles.detailLabel}>Bracket</Text>
                  <Text style={styles.detailValue}>
                    {match.bracketType === 'Winners'
                      ? 'Winners Bracket'
                      : match.bracketType === 'Losers'
                      ? 'Losers Bracket'
                      : 'Grand Final'}
                  </Text>
                </View>
              </View>
            )}
          </View>
        </View>

        {/* Tournament Context Links */}
        <View style={styles.linksSection}>
          <Text style={styles.sectionTitle}>Tournament</Text>

          {/* View Bracket */}
          <TouchableOpacity
            style={styles.linkButton}
            onPress={handleViewBracket}
            activeOpacity={0.7}
          >
            <View style={styles.linkContent}>
              <Ionicons name="git-network-outline" size={20} color={colors.text.secondary} />
              <Text style={styles.linkText}>View Full Bracket</Text>
            </View>
            <Ionicons name="chevron-forward" size={20} color={colors.text.muted} />
          </TouchableOpacity>

          {/* View Schedule */}
          <TouchableOpacity
            style={styles.linkButton}
            onPress={handleViewSchedule}
            activeOpacity={0.7}
          >
            <View style={styles.linkContent}>
              <Ionicons name="calendar-outline" size={20} color={colors.text.secondary} />
              <Text style={styles.linkText}>View Full Schedule</Text>
            </View>
            <Ionicons name="chevron-forward" size={20} color={colors.text.muted} />
          </TouchableOpacity>
        </View>

        {/* Admin Actions */}
        {currentTournament?.canManage && !isCompleted && (
          <View style={styles.actionsSection}>
            <Text style={styles.sectionTitle}>Admin Actions</Text>
            <TouchableOpacity
              style={styles.adminButton}
              onPress={handleEnterScore}
              activeOpacity={0.8}
            >
              <Ionicons name="create-outline" size={20} color={colors.bg.darkest} />
              <Text style={styles.adminButtonText}>
                {match.status === 'InProgress' ? 'Edit Score' : 'Enter Score'}
              </Text>
            </TouchableOpacity>
          </View>
        )}

        {/* Bottom spacing */}
        <View style={styles.bottomSpacer} />
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: colors.bg.darkest,
  },

  // Scroll view
  scrollView: {
    flex: 1,
  },
  scrollContent: {
    padding: spacing.md,
  },

  // Match card styles
  matchCard: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
    overflow: 'hidden',
    marginBottom: spacing.lg,
  },
  matchHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    backgroundColor: colors.bg.elevated,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  matchNumber: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
  },
  statusBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: 4,
    borderRadius: radius.sm,
  },
  statusText: {
    fontSize: 11,
    fontWeight: '700',
    textTransform: 'uppercase',
  },

  // Teams container
  teamsContainer: {
    padding: spacing.md,
    gap: spacing.sm,
  },
  teamRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.md,
    borderRadius: radius.md,
    backgroundColor: colors.bg.elevated,
  },
  winnerRow: {
    backgroundColor: colors.subtle.green,
  },
  teamInfo: {
    flex: 1,
    marginRight: spacing.md,
  },
  teamLabel: {
    fontSize: 10,
    fontWeight: '700',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.xs,
  },
  teamName: {
    fontSize: 18,
    fontWeight: '600',
    color: colors.text.secondary,
  },
  winnerText: {
    color: colors.primary.green,
    fontWeight: '700',
  },
  tbdText: {
    color: colors.text.subtle,
    fontStyle: 'italic',
    fontWeight: '500',
  },
  score: {
    fontSize: 32,
    fontWeight: '700',
    color: colors.text.secondary,
    minWidth: 48,
    textAlign: 'right',
  },
  winnerScore: {
    color: colors.primary.green,
  },

  // VS divider
  vsDivider: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    paddingVertical: spacing.xs,
  },
  vsLine: {
    flex: 1,
    height: 1,
    backgroundColor: colors.border.default,
  },
  vsText: {
    fontSize: 12,
    fontWeight: '700',
    color: colors.text.subtle,
    textTransform: 'uppercase',
  },

  // Details container
  detailsContainer: {
    padding: spacing.md,
    paddingTop: 0,
    gap: spacing.sm,
  },
  detailRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    padding: spacing.sm,
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
  },
  detailIcon: {
    width: 32,
    height: 32,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.sm,
  },
  detailContent: {
    flex: 1,
  },
  detailLabel: {
    fontSize: 11,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    marginBottom: spacing.xs,
  },
  detailValue: {
    fontSize: 15,
    fontWeight: '500',
    color: colors.text.primary,
  },

  // Links section
  linksSection: {
    marginBottom: spacing.lg,
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: colors.text.primary,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.sm,
    paddingHorizontal: spacing.xs,
  },
  linkButton: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: colors.bg.dark,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    padding: spacing.md,
    marginBottom: spacing.sm,
  },
  linkContent: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  linkText: {
    fontSize: 15,
    fontWeight: '500',
    color: colors.text.secondary,
  },

  // Admin actions section
  actionsSection: {
    marginBottom: spacing.lg,
  },
  adminButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    gap: spacing.sm,
  },
  adminButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.bg.darkest,
  },

  // Loading state
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },

  // Empty state
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
    marginBottom: spacing.lg,
  },
  retryButton: {
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
  },
  retryButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.bg.darkest,
  },

  // Bottom spacing
  bottomSpacer: {
    height: spacing.xl,
  },
});
