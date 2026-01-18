import { useCallback, useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  RefreshControl,
  Alert,
} from 'react-native';
import { useLocalSearchParams, useRouter, Stack, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useTournamentTeamStore } from '../../../../../stores/tournamentTeamStore';
import { useAuthStore } from '../../../../../stores/authStore';
import { PlayerSearchModal } from '../../../../../components/tournaments/PlayerSearchModal';
import { TransferCaptainModal } from '../../../../../components/tournaments/TransferCaptainModal';
import { TeamRosterList } from '../../../../../components';
import { colors, spacing, radius } from '../../../../../theme';
import type { UserSearchResultDto } from '@bhmhockey/shared';

export default function ManageTeamScreen() {
  const { id: tournamentId, teamId } = useLocalSearchParams<{ id: string; teamId: string }>();
  const router = useRouter();

  // State
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [showPlayerSearch, setShowPlayerSearch] = useState(false);
  const [showTransferModal, setShowTransferModal] = useState(false);

  // Stores
  const user = useAuthStore(state => state.user);
  const {
    currentTeam,
    teamMembers,
    isLoading,
    isProcessing,
    fetchTeamById,
    fetchTeamMembers,
    addPlayer,
    removePlayer,
    transferCaptain,
  } = useTournamentTeamStore();

  // Verify user is captain
  const isCaptain = currentTeam?.captainUserId === user?.id;

  // Fetch data on mount and focus
  useFocusEffect(
    useCallback(() => {
      if (tournamentId && teamId) {
        fetchTeamById(tournamentId, teamId);
        fetchTeamMembers(tournamentId, teamId);
      }
    }, [tournamentId, teamId])
  );

  // Redirect if not captain
  useEffect(() => {
    if (!isLoading && currentTeam && !isCaptain) {
      Alert.alert(
        'Access Denied',
        'Only the team captain can manage the team.',
        [{ text: 'OK', onPress: () => router.back() }]
      );
    }
  }, [isLoading, currentTeam, isCaptain]);

  // Pull to refresh
  const handleRefresh = async () => {
    if (!tournamentId || !teamId) return;
    setIsRefreshing(true);
    try {
      await Promise.all([
        fetchTeamById(tournamentId, teamId),
        fetchTeamMembers(tournamentId, teamId),
      ]);
    } finally {
      setIsRefreshing(false);
    }
  };

  // Add player handler
  const handleSelectUser = async (selectedUser: UserSearchResultDto) => {
    if (!tournamentId || !teamId) return;

    setShowPlayerSearch(false);

    const success = await addPlayer(tournamentId, teamId, selectedUser.id);

    if (success) {
      Alert.alert(
        'Player Added',
        `${selectedUser.firstName} ${selectedUser.lastName} has been invited to the team.`
      );
    } else {
      Alert.alert(
        'Failed to Add Player',
        'Unable to add player to the team. Please try again.'
      );
    }
  };

  // Remove player handler
  const handleRemovePlayer = (userId: string) => {
    if (!tournamentId || !teamId) return;

    const member = teamMembers.find(m => m.userId === userId);
    if (!member) return;

    Alert.alert(
      'Remove Player',
      `Are you sure you want to remove ${member.userFirstName} ${member.userLastName} from the team?`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Remove',
          style: 'destructive',
          onPress: async () => {
            const success = await removePlayer(tournamentId, teamId, userId);

            if (success) {
              Alert.alert('Player Removed', 'Player has been removed from the team.');
            } else {
              Alert.alert('Failed', 'Unable to remove player. Please try again.');
            }
          },
        },
      ]
    );
  };

  // Transfer captain handler
  const handleTransferCaptain = async (newCaptainUserId: string) => {
    if (!tournamentId || !teamId) return;

    const newCaptain = teamMembers.find(m => m.userId === newCaptainUserId);
    if (!newCaptain) return;

    const success = await transferCaptain(tournamentId, teamId, newCaptainUserId);

    setShowTransferModal(false);

    if (success) {
      Alert.alert(
        'Captain Transferred',
        `${newCaptain.userFirstName} ${newCaptain.userLastName} is now the team captain.`,
        [
          {
            text: 'OK',
            onPress: () => {
              // Navigate back since user is no longer captain
              router.back();
            },
          },
        ]
      );
    } else {
      Alert.alert('Failed', 'Unable to transfer captain role. Please try again.');
    }
  };

  // Get existing member IDs for filtering in search
  const existingMemberIds = teamMembers.map(m => m.userId);

  // Loading state
  if (isLoading && !currentTeam) {
    return (
      <View style={styles.container}>
        <Stack.Screen
          options={{
            title: 'Manage Team',
            headerStyle: { backgroundColor: colors.bg.darkest },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={styles.loadingText}>Loading team...</Text>
        </View>
      </View>
    );
  }

  // No team or not captain (while loading redirect)
  if (!currentTeam || !isCaptain) {
    return (
      <View style={styles.container}>
        <Stack.Screen
          options={{
            title: 'Manage Team',
            headerStyle: { backgroundColor: colors.bg.darkest },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <Stack.Screen
        options={{
          title: 'Manage Team',
          headerStyle: { backgroundColor: colors.bg.darkest },
          headerTintColor: colors.text.primary,
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
        {/* Team Name */}
        <View style={styles.teamHeader}>
          <Text style={styles.teamName}>{currentTeam.name}</Text>
          <Text style={styles.teamSubtitle}>Team Management</Text>
        </View>

        {/* Add Player Button */}
        <TouchableOpacity
          style={styles.addButton}
          onPress={() => setShowPlayerSearch(true)}
          disabled={isProcessing}
        >
          <Ionicons name="person-add" size={20} color={colors.text.primary} />
          <Text style={styles.addButtonText}>Add Player</Text>
        </TouchableOpacity>

        {/* Roster Section */}
        <View style={styles.section}>
          <View style={styles.sectionHeader}>
            <Text style={styles.sectionTitle}>ROSTER</Text>
            <Text style={styles.sectionCount}>
              {teamMembers.length} {teamMembers.length === 1 ? 'member' : 'members'}
            </Text>
          </View>

          <TeamRosterList
            members={teamMembers}
            captainUserId={currentTeam.captainUserId}
            currentUserId={user?.id}
            isCaptain={true}
            onRemovePlayer={handleRemovePlayer}
            isLoading={isLoading}
          />
        </View>

        {/* Captain Settings Section */}
        <View style={styles.section}>
          <View style={styles.sectionHeader}>
            <Text style={styles.sectionTitle}>CAPTAIN SETTINGS</Text>
          </View>

          <TouchableOpacity
            style={styles.settingsButton}
            onPress={() => setShowTransferModal(true)}
            disabled={isProcessing || teamMembers.filter(m => m.status === 'Accepted' && m.userId !== user?.id).length === 0}
          >
            <View style={styles.settingsButtonContent}>
              <Ionicons name="swap-horizontal" size={20} color={colors.primary.teal} />
              <View style={styles.settingsButtonText}>
                <Text style={styles.settingsButtonTitle}>Transfer Captaincy</Text>
                <Text style={styles.settingsButtonSubtitle}>
                  Make another player the team captain
                </Text>
              </View>
            </View>
            <Ionicons name="chevron-forward" size={20} color={colors.text.muted} />
          </TouchableOpacity>

          {/* Warning Text */}
          <View style={styles.warningBox}>
            <Ionicons name="warning" size={16} color={colors.status.warning} />
            <Text style={styles.warningText}>
              Transferring the captain role cannot be undone. You will become a regular player.
            </Text>
          </View>
        </View>
      </ScrollView>

      {/* Player Search Modal */}
      <PlayerSearchModal
        visible={showPlayerSearch}
        onClose={() => setShowPlayerSearch(false)}
        onSelectUser={handleSelectUser}
        tournamentId={tournamentId!}
        teamId={teamId!}
        existingMemberIds={existingMemberIds}
      />

      {/* Transfer Captain Modal */}
      <TransferCaptainModal
        visible={showTransferModal}
        onClose={() => setShowTransferModal(false)}
        onTransfer={handleTransferCaptain}
        members={teamMembers}
        currentCaptainId={user?.id || ''}
        isProcessing={isProcessing}
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
    gap: spacing.md,
  },
  loadingText: {
    fontSize: 16,
    color: colors.text.muted,
  },
  scrollView: {
    flex: 1,
  },
  scrollContent: {
    padding: spacing.lg,
    paddingBottom: spacing.xl,
  },
  teamHeader: {
    marginBottom: spacing.lg,
  },
  teamName: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.xs,
  },
  teamSubtitle: {
    fontSize: 14,
    color: colors.text.muted,
  },
  addButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.primary.teal,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.lg,
    borderRadius: radius.md,
    marginBottom: spacing.lg,
    gap: spacing.sm,
  },
  addButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  section: {
    marginBottom: spacing.xl,
  },
  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: spacing.md,
  },
  sectionTitle: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  sectionCount: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
  },
  settingsButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  settingsButtonContent: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
    gap: spacing.md,
  },
  settingsButtonText: {
    flex: 1,
  },
  settingsButtonTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: 2,
  },
  settingsButtonSubtitle: {
    fontSize: 13,
    color: colors.text.muted,
  },
  warningBox: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    backgroundColor: colors.status.warningSubtle,
    borderWidth: 1,
    borderColor: colors.status.warning,
    padding: spacing.md,
    borderRadius: radius.md,
    marginTop: spacing.md,
    gap: spacing.sm,
  },
  warningText: {
    flex: 1,
    fontSize: 13,
    color: colors.status.warning,
    lineHeight: 18,
  },
});
