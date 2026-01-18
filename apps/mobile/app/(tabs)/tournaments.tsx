import { useEffect, useState, useMemo, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
  ScrollView,
  Alert,
} from 'react-native';
import { useRouter, useFocusEffect } from 'expo-router';
import { useTournamentStore } from '../../stores/tournamentStore';
import { useTournamentTeamStore } from '../../stores/tournamentTeamStore';
import { useAuthStore } from '../../stores/authStore';
import { TournamentCard, EmptyState, PendingInvitationCard, SectionHeader } from '../../components';
import { colors, spacing, radius } from '../../theme';
import type { TournamentDto, TournamentStatus, PendingTeamInvitationDto } from '@bhmhockey/shared';

// Filter options
type FilterOption = 'all' | 'open' | 'inProgress' | 'completed';

const FILTER_OPTIONS: { key: FilterOption; label: string }[] = [
  { key: 'all', label: 'All' },
  { key: 'open', label: 'Open' },
  { key: 'inProgress', label: 'In Progress' },
  { key: 'completed', label: 'Completed' },
];

// Map filter keys to tournament status values
const filterToStatus: Record<Exclude<FilterOption, 'all'>, TournamentStatus> = {
  open: 'Open',
  inProgress: 'InProgress',
  completed: 'Completed',
};

// Statuses hidden from regular users (only visible to those who can manage)
const hiddenStatuses: TournamentStatus[] = ['Draft', 'Cancelled'];

export default function TournamentsScreen() {
  const router = useRouter();
  const { isAuthenticated } = useAuthStore();
  const {
    tournaments,
    isLoading,
    error,
    fetchTournaments,
  } = useTournamentStore();
  const {
    myInvitations,
    fetchMyInvitations,
    respondToInvite,
    isProcessing,
  } = useTournamentTeamStore();

  const [activeFilter, setActiveFilter] = useState<FilterOption>('all');

  useEffect(() => {
    fetchTournaments();
  }, []);

  // Fetch invitations on screen focus
  useFocusEffect(
    useCallback(() => {
      if (isAuthenticated) {
        fetchMyInvitations();
      }
    }, [isAuthenticated])
  );

  // Filter tournaments based on selected filter
  const filteredTournaments = useMemo(() => {
    let filtered: TournamentDto[];

    // First, filter out Draft and Cancelled for non-managers
    const visibleTournaments = tournaments.filter(t => {
      // If user can manage this tournament, show all statuses
      if (t.canManage) return true;
      // Otherwise, hide Draft and Cancelled
      return !hiddenStatuses.includes(t.status);
    });

    // Then apply the selected filter
    if (activeFilter === 'all') {
      filtered = visibleTournaments;
    } else {
      const targetStatus = filterToStatus[activeFilter];
      filtered = visibleTournaments.filter(t => t.status === targetStatus);
    }

    // Sort by start date ascending (earliest first)
    return filtered.sort(
      (a, b) => new Date(a.startDate).getTime() - new Date(b.startDate).getTime()
    );
  }, [tournaments, activeFilter]);

  const handleTournamentPress = (tournamentId: string) => {
    router.push(`/tournaments/${tournamentId}`);
  };

  const handleCreatePress = () => {
    router.push('/tournaments/create');
  };

  // Handle accepting an invitation with position selection
  const handleAcceptInvitation = (invitation: PendingTeamInvitationDto) => {
    if (isProcessing) return;

    Alert.alert(
      'Select Your Position',
      `Accept invitation to join ${invitation.teamName}?`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Goalie',
          onPress: async () => {
            const success = await respondToInvite(
              invitation.tournamentId,
              invitation.teamId,
              true,
              'Goalie'
            );
            if (success) {
              Alert.alert('Success', `You've joined ${invitation.teamName}!`);
              fetchMyInvitations(); // Refresh the list
            } else {
              Alert.alert('Error', 'Failed to accept invitation. Please try again.');
            }
          },
        },
        {
          text: 'Skater',
          onPress: async () => {
            const success = await respondToInvite(
              invitation.tournamentId,
              invitation.teamId,
              true,
              'Skater'
            );
            if (success) {
              Alert.alert('Success', `You've joined ${invitation.teamName}!`);
              fetchMyInvitations(); // Refresh the list
            } else {
              Alert.alert('Error', 'Failed to accept invitation. Please try again.');
            }
          },
        },
      ]
    );
  };

  // Handle declining an invitation
  const handleDeclineInvitation = (invitation: PendingTeamInvitationDto) => {
    if (isProcessing) return;

    Alert.alert(
      'Decline Invitation',
      `Are you sure you want to decline the invitation to join ${invitation.teamName}?`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Decline',
          style: 'destructive',
          onPress: async () => {
            const success = await respondToInvite(
              invitation.tournamentId,
              invitation.teamId,
              false
            );
            if (success) {
              fetchMyInvitations(); // Refresh the list
            } else {
              Alert.alert('Error', 'Failed to decline invitation. Please try again.');
            }
          },
        },
      ]
    );
  };

  const renderTournament = ({ item }: { item: TournamentDto }) => (
    <TournamentCard
      tournament={item}
      onPress={() => handleTournamentPress(item.id)}
    />
  );

  if (isLoading && tournaments.length === 0) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
        <Text style={styles.loadingText}>Loading tournaments...</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <View style={styles.headerRow}>
          <View>
            <Text style={styles.title}>Tournaments</Text>
            <Text style={styles.subtitle}>Compete and win</Text>
          </View>
          {isAuthenticated && (
            <TouchableOpacity
              style={styles.createButton}
              onPress={handleCreatePress}
            >
              <Text style={styles.createButtonText}>+</Text>
            </TouchableOpacity>
          )}
        </View>
      </View>

      {/* Filter Pills */}
      <View style={styles.filterContainer}>
        <ScrollView
          horizontal
          showsHorizontalScrollIndicator={false}
          contentContainerStyle={styles.filterContent}
        >
          {FILTER_OPTIONS.map((filter) => (
            <TouchableOpacity
              key={filter.key}
              style={[
                styles.filterPill,
                activeFilter === filter.key && styles.filterPillActive,
              ]}
              onPress={() => setActiveFilter(filter.key)}
            >
              <Text
                style={[
                  styles.filterPillText,
                  activeFilter === filter.key && styles.filterPillTextActive,
                ]}
              >
                {filter.label}
              </Text>
            </TouchableOpacity>
          ))}
        </ScrollView>
      </View>

      {/* Error banner */}
      {error && (
        <View style={styles.errorBanner}>
          <Text style={styles.errorText}>{error}</Text>
        </View>
      )}

      <FlatList
        data={filteredTournaments}
        renderItem={renderTournament}
        keyExtractor={(item) => item.id}
        contentContainerStyle={styles.list}
        refreshControl={
          <RefreshControl
            refreshing={isLoading}
            onRefresh={fetchTournaments}
            tintColor={colors.primary.teal}
            colors={[colors.primary.teal]}
            progressBackgroundColor={colors.bg.dark}
          />
        }
        ListHeaderComponent={
          isAuthenticated && myInvitations.length > 0 ? (
            <View style={styles.invitationsSection}>
              <View style={styles.invitationsSectionHeader}>
                <SectionHeader
                  title={`MY INVITATIONS (${myInvitations.length})`}
                />
              </View>
              {myInvitations.map((invitation) => (
                <PendingInvitationCard
                  key={`${invitation.tournamentId}-${invitation.teamId}`}
                  invitation={invitation}
                  onAccept={handleAcceptInvitation}
                  onDecline={handleDeclineInvitation}
                  isProcessing={isProcessing}
                />
              ))}
            </View>
          ) : null
        }
        ListEmptyComponent={
          <EmptyState
            icon="🏆"
            title="No Tournaments"
            message="No tournaments match your filter. Check back later or create your own!"
            actionLabel={isAuthenticated ? "Create Tournament" : undefined}
            onAction={isAuthenticated ? handleCreatePress : undefined}
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
  list: {
    padding: spacing.lg,
  },
  filterContainer: {
    backgroundColor: colors.bg.darkest,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
    flexGrow: 0,
    flexShrink: 0,
  },
  filterContent: {
    paddingHorizontal: spacing.md,
    paddingVertical: 12,
    flexDirection: 'row',
    alignItems: 'center',
  },
  filterPill: {
    paddingHorizontal: 16,
    paddingVertical: 10,
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.muted,
    borderRadius: 9999,
    marginRight: spacing.sm,
  },
  filterPillActive: {
    backgroundColor: colors.primary.teal,
    borderColor: colors.primary.teal,
  },
  filterPillText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.secondary,
    lineHeight: 18,
  },
  filterPillTextActive: {
    color: colors.bg.darkest,
  },
  invitationsSection: {
    marginBottom: spacing.lg,
  },
  invitationsSectionHeader: {
    marginBottom: spacing.md,
  },
});
