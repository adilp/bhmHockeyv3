import { useCallback, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ActivityIndicator,
  ScrollView,
  TouchableOpacity,
  RefreshControl,
} from 'react-native';
import { useLocalSearchParams, useRouter, useFocusEffect, Stack } from 'expo-router';
import { useTournamentTeamStore } from '../../../../../stores/tournamentTeamStore';
import { useTournamentStore } from '../../../../../stores/tournamentStore';
import { useAuthStore } from '../../../../../stores/authStore';
import { TeamRosterList, Badge } from '../../../../../components';
import { colors, spacing, radius } from '../../../../../theme';
import type { TournamentMatchDto, TournamentTeamStatus } from '@bhmhockey/shared';

// Map team status to badge variant
const getStatusBadgeVariant = (status: TournamentTeamStatus): 'green' | 'teal' | 'error' | 'default' => {
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

// Format match date/time
function formatMatchDateTime(dateTimeString: string): string {
  const date = new Date(dateTimeString);
  return date.toLocaleDateString('en-US', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}

export default function TeamDetailScreen() {
  const { id: tournamentId, teamId } = useLocalSearchParams<{ id: string; teamId: string }>();
  const router = useRouter();
  const [isRefreshing, setIsRefreshing] = useState(false);

  const { user } = useAuthStore();
  const {
    currentTeam,
    teamMembers,
    isLoading,
    error,
    fetchTeamById,
    fetchTeamMembers,
    clearTeam,
    clearError,
  } = useTournamentTeamStore();

  const {
    matches,
    currentTournament,
    fetchMatches,
  } = useTournamentStore();

  useFocusEffect(
    useCallback(() => {
      if (tournamentId && teamId) {
        fetchTeamById(tournamentId, teamId);
        fetchTeamMembers(tournamentId, teamId);
        fetchMatches(tournamentId);
      }
      return () => {
        clearTeam();
        clearError();
      };
    }, [tournamentId, teamId])
  );

  const handleRefresh = async () => {
    if (!tournamentId || !teamId) return;
    setIsRefreshing(true);
    try {
      await Promise.all([
        fetchTeamById(tournamentId, teamId),
        fetchTeamMembers(tournamentId, teamId),
        fetchMatches(tournamentId),
      ]);
    } finally {
      setIsRefreshing(false);
    }
  };

  // Check if current user is captain
  const isCaptain = currentTeam?.captainUserId === user?.id;

  // Filter matches for this team (upcoming games only)
  const teamMatches = matches.filter((match) => {
    const isTeamInMatch = match.homeTeamId === teamId || match.awayTeamId === teamId;
    const isUpcoming = match.status === 'Scheduled' || match.status === 'InProgress';
    return isTeamInMatch && isUpcoming;
  });

  const renderMatchItem = (match: TournamentMatchDto) => {
    const isHomeTeam = match.homeTeamId === teamId;
    const opponentName = isHomeTeam ? match.awayTeamName : match.homeTeamName;

    return (
      <View key={match.id} style={styles.matchCard}>
        <View style={styles.matchHeader}>
          <Text style={styles.matchRound}>Round {match.round}</Text>
          {match.scheduledTime && (
            <Text style={styles.matchTime}>{formatMatchDateTime(match.scheduledTime)}</Text>
          )}
        </View>

        <View style={styles.matchInfo}>
          <Text style={styles.matchLabel}>
            {isHomeTeam ? 'vs' : '@'} {opponentName || 'TBD'}
          </Text>
          {match.venue && (
            <Text style={styles.matchVenue}>{match.venue}</Text>
          )}
        </View>

        {match.status === 'InProgress' && (
          <Badge variant="warning">In Progress</Badge>
        )}
      </View>
    );
  };

  if (isLoading && !currentTeam) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Team',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.dark },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={styles.loadingText}>Loading team...</Text>
        </View>
      </>
    );
  }

  if (error && !currentTeam) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Team',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.dark },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.errorContainer}>
          <Text style={styles.errorText}>{error}</Text>
          <TouchableOpacity style={styles.retryButton} onPress={handleRefresh}>
            <Text style={styles.retryButtonText}>Retry</Text>
          </TouchableOpacity>
        </View>
      </>
    );
  }

  if (!currentTeam) {
    return null;
  }

  const headerTitle = currentTeam.name.length > 20
    ? `${currentTeam.name.substring(0, 20)}...`
    : currentTeam.name;

  return (
    <View style={styles.container}>
      <Stack.Screen
        options={{
          title: headerTitle,
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
          headerRight: isCaptain
            ? () => (
                <TouchableOpacity
                  onPress={() => router.push(`/tournaments/${tournamentId}/teams/${teamId}/manage`)}
                  style={styles.headerButton}
                >
                  <Text style={styles.headerButtonText}>Manage</Text>
                </TouchableOpacity>
              )
            : undefined,
        }}
      />

      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.scrollContent}
        refreshControl={
          <RefreshControl
            refreshing={isRefreshing}
            onRefresh={handleRefresh}
            tintColor={colors.primary.teal}
          />
        }
      >
        {/* Team Header */}
        <View style={styles.headerCard}>
          <Text style={styles.teamName}>{currentTeam.name}</Text>

          {currentTournament && (
            <View style={styles.infoRow}>
              <Text style={styles.infoLabel}>Tournament:</Text>
              <Text style={styles.infoValue}>{currentTournament.name}</Text>
            </View>
          )}

          <View style={styles.infoRow}>
            <Text style={styles.infoLabel}>Status:</Text>
            <Badge variant={getStatusBadgeVariant(currentTeam.status)}>
              {currentTeam.status}
            </Badge>
          </View>

          {currentTeam.seed !== null && currentTeam.seed !== undefined && (
            <View style={styles.infoRow}>
              <Text style={styles.infoLabel}>Seed:</Text>
              <Text style={styles.infoValue}>#{currentTeam.seed}</Text>
            </View>
          )}
        </View>

        {/* Roster Section */}
        <View style={styles.section}>
          <Text style={styles.sectionHeader}>ROSTER ({teamMembers.length} players)</Text>
          <TeamRosterList
            members={teamMembers}
            captainUserId={currentTeam.captainUserId}
            currentUserId={user?.id}
            isCaptain={isCaptain}
            isLoading={isLoading}
          />
        </View>

        {/* Upcoming Games Section */}
        <View style={styles.section}>
          <Text style={styles.sectionHeader}>UPCOMING GAMES</Text>
          {teamMatches.length > 0 ? (
            teamMatches.map(renderMatchItem)
          ) : (
            <View style={styles.emptyGames}>
              <Text style={styles.emptyGamesText}>No games scheduled yet</Text>
            </View>
          )}
        </View>

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
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.lg,
    backgroundColor: colors.bg.darkest,
  },
  errorText: {
    fontSize: 16,
    color: colors.status.error,
    textAlign: 'center',
    marginBottom: spacing.md,
  },
  retryButton: {
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
  },
  retryButtonText: {
    color: colors.bg.darkest,
    fontSize: 16,
    fontWeight: '600',
  },
  headerButton: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
  },
  headerButtonText: {
    color: colors.primary.teal,
    fontSize: 16,
    fontWeight: '600',
  },
  scrollView: {
    flex: 1,
  },
  scrollContent: {
    padding: spacing.md,
  },

  // Header Card
  headerCard: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
    marginBottom: spacing.lg,
  },
  teamName: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.md,
  },
  infoRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: spacing.xs,
    borderTopWidth: 1,
    borderTopColor: colors.border.muted,
    marginTop: spacing.xs,
  },
  infoLabel: {
    fontSize: 14,
    color: colors.text.secondary,
    width: 80,
  },
  infoValue: {
    flex: 1,
    fontSize: 14,
    color: colors.text.primary,
    fontWeight: '500',
  },

  // Section
  section: {
    marginBottom: spacing.lg,
  },
  sectionHeader: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    letterSpacing: 0.5,
    marginBottom: spacing.sm,
  },

  // Match Card
  matchCard: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    marginBottom: spacing.sm,
  },
  matchHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.xs,
  },
  matchRound: {
    fontSize: 12,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
  },
  matchTime: {
    fontSize: 12,
    color: colors.text.secondary,
  },
  matchInfo: {
    marginBottom: spacing.xs,
  },
  matchLabel: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: 2,
  },
  matchVenue: {
    fontSize: 13,
    color: colors.text.muted,
  },

  // Empty State
  emptyGames: {
    backgroundColor: colors.bg.dark,
    padding: spacing.xl,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
    alignItems: 'center',
  },
  emptyGamesText: {
    fontSize: 14,
    color: colors.text.muted,
  },

  bottomSpacer: {
    height: spacing.xl,
  },
});
