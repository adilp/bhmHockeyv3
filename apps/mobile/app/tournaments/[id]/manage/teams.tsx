import { useCallback, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  Modal,
  TextInput,
  Pressable,
} from 'react-native';
import { useLocalSearchParams, Stack, useFocusEffect } from 'expo-router';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { Badge, EmptyState } from '../../../../components';
import { BadgeIconsRow } from '../../../../components/badges';
import { colors, spacing, radius } from '../../../../theme';
import type { TournamentRegistrationDto, TournamentTeamDto, Position, SkillLevel } from '@bhmhockey/shared';

export default function ManageTeamsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const [selectedPlayerId, setSelectedPlayerId] = useState<string | null>(null);
  const [isProcessing, setIsProcessing] = useState(false);
  const [showCreateModal, setShowCreateModal] = useState(false);

  const {
    registrations,
    teams,
    currentTournament,
    fetchAllRegistrations,
    fetchTeams,
    assignPlayerToTeam,
    autoAssignTeams,
    bulkCreateTeams,
    isLoading,
    processingId,
  } = useTournamentStore();

  useFocusEffect(
    useCallback(() => {
      if (id) {
        fetchAllRegistrations(id);
        fetchTeams(id);
      }
      return () => {
        setSelectedPlayerId(null);
        setIsProcessing(false);
      };
    }, [id])
  );

  // Filter registrations
  const unassignedPlayers = registrations.filter(
    (reg) => reg.status === 'Registered' && !reg.assignedTeamId
  );
  const assignedPlayers = registrations.filter(
    (reg) => reg.status === 'Registered' && reg.assignedTeamId
  );

  // Get players for a specific team
  const getTeamPlayers = (teamId: string): TournamentRegistrationDto[] => {
    return assignedPlayers.filter((reg) => reg.assignedTeamId === teamId);
  };

  // Handle player tap in unassigned section
  const handlePlayerTap = (playerId: string) => {
    if (isProcessing) return;
    if (selectedPlayerId === playerId) {
      // Deselect if tapping the same player
      setSelectedPlayerId(null);
    } else {
      setSelectedPlayerId(playerId);
    }
  };

  // Handle team section tap when player is selected
  const handleTeamTap = async (teamId: string) => {
    if (!selectedPlayerId || isProcessing) return;

    setIsProcessing(true);
    try {
      const success = await assignPlayerToTeam(id, selectedPlayerId, teamId);
      if (success) {
        setSelectedPlayerId(null);
        // Refresh data
        await Promise.all([fetchAllRegistrations(id), fetchTeams(id)]);
      } else {
        Alert.alert('Error', 'Failed to assign player to team');
      }
    } catch (error) {
      Alert.alert('Error', 'Failed to assign player to team');
    } finally {
      setIsProcessing(false);
    }
  };

  // Handle removing player from team
  const handleRemovePlayerFromTeam = (registration: TournamentRegistrationDto) => {
    if (isProcessing) return;

    Alert.alert(
      'Remove from Team',
      `Remove ${registration.user.firstName} ${registration.user.lastName} from ${registration.assignedTeamName}?`,
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Remove',
          style: 'destructive',
          onPress: async () => {
            setIsProcessing(true);
            try {
              // Assign to null team (backend should handle this as unassignment)
              // We'll need to create a separate unassign endpoint or check API
              // For now, this is a placeholder - you may need to add unassign endpoint
              Alert.alert('Info', 'Unassign endpoint not yet implemented');
            } catch (error) {
              Alert.alert('Error', 'Failed to remove player from team');
            } finally {
              setIsProcessing(false);
            }
          },
        },
      ]
    );
  };

  // Handle auto-assign
  const handleAutoAssign = () => {
    if (unassignedPlayers.length === 0) {
      Alert.alert('No Players', 'All players are already assigned to teams.');
      return;
    }

    Alert.alert(
      'Auto-Assign Teams',
      'Automatically assign all unassigned players to teams?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Balance by Skill',
          onPress: () => performAutoAssign(true),
        },
        {
          text: 'Random',
          onPress: () => performAutoAssign(false),
        },
      ]
    );
  };

  const performAutoAssign = async (balanceBySkillLevel: boolean) => {
    setIsProcessing(true);
    try {
      const result = await autoAssignTeams(id, balanceBySkillLevel);
      if (result) {
        Alert.alert(
          'Success',
          result.message || `Assigned ${result.assignedCount} players to teams`
        );
        // Refresh data
        await Promise.all([fetchAllRegistrations(id), fetchTeams(id)]);
      } else {
        Alert.alert('Error', 'Failed to auto-assign teams');
      }
    } catch (error) {
      Alert.alert('Error', 'Failed to auto-assign teams');
    } finally {
      setIsProcessing(false);
    }
  };

  // Get skill level info for display
  const getSkillLevelInfo = (registration: TournamentRegistrationDto): {
    level: SkillLevel | null;
    color: string;
  } => {
    const { user, position } = registration;
    const positions = user.positions;

    if (!positions) return { level: null, color: colors.text.muted };

    const skillLevel: SkillLevel | undefined =
      position === 'Goalie' ? positions.goalie : positions.skater;

    if (!skillLevel) return { level: null, color: colors.text.muted };

    return { level: skillLevel, color: colors.skillLevel[skillLevel] || colors.text.muted };
  };

  // Show loading spinner on initial load
  if (isLoading && registrations.length === 0) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Manage Teams',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.dark },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
          <Text style={styles.loadingText}>Loading...</Text>
        </View>
      </>
    );
  }

  return (
    <View style={styles.container}>
      <Stack.Screen
        options={{
          title: 'Manage Teams',
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
        }}
      />

      <ScrollView contentContainerStyle={styles.scrollContent}>
        {/* Header Actions */}
        <View style={styles.headerActions}>
          <TouchableOpacity
            style={styles.actionButton}
            onPress={() => setShowCreateModal(true)}
            disabled={isProcessing}
          >
            <Text style={styles.actionButtonText}>Create Teams</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[styles.actionButton, styles.actionButtonPrimary]}
            onPress={handleAutoAssign}
            disabled={isProcessing || unassignedPlayers.length === 0}
          >
            <Text style={[styles.actionButtonText, styles.actionButtonPrimaryText]}>
              Auto-Assign
            </Text>
          </TouchableOpacity>
        </View>

        {/* Hint Text */}
        {selectedPlayerId && (
          <View style={styles.hintContainer}>
            <Text style={styles.hintText}>Tap a team to assign player</Text>
          </View>
        )}

        {/* Unassigned Players Section */}
        <View style={styles.section}>
          <View style={styles.sectionHeader}>
            <Text style={styles.sectionTitle}>UNASSIGNED PLAYERS ({unassignedPlayers.length})</Text>
          </View>

          {unassignedPlayers.length === 0 ? (
            <View style={styles.emptySection}>
              <Text style={styles.emptyText}>All players assigned</Text>
            </View>
          ) : (
            <View style={styles.playersGrid}>
              {unassignedPlayers.map((registration) => (
                <PlayerChip
                  key={registration.id}
                  registration={registration}
                  isSelected={selectedPlayerId === registration.id}
                  onPress={() => handlePlayerTap(registration.id)}
                  skillInfo={getSkillLevelInfo(registration)}
                />
              ))}
            </View>
          )}
        </View>

        {/* Teams Sections */}
        {teams.length === 0 ? (
          <View style={styles.emptyContainer}>
            <EmptyState
              title="No Teams Yet"
              message="Create teams to start assigning players."
            />
          </View>
        ) : (
          teams.map((team) => (
            <TeamSection
              key={team.id}
              team={team}
              players={getTeamPlayers(team.id)}
              maxPlayers={currentTournament?.maxPlayersPerTeam}
              isHighlighted={!!selectedPlayerId}
              onTap={() => handleTeamTap(team.id)}
              onPlayerTap={handleRemovePlayerFromTeam}
              getSkillLevelInfo={getSkillLevelInfo}
            />
          ))
        )}
      </ScrollView>

      {/* Create Teams Modal */}
      <CreateTeamsModal
        visible={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        onSubmit={async (count, namePrefix) => {
          setShowCreateModal(false);
          setIsProcessing(true);
          try {
            const result = await bulkCreateTeams(id, count, namePrefix);
            if (result) {
              Alert.alert('Success', result.message || `Created ${count} teams`);
              await fetchTeams(id);
            } else {
              Alert.alert('Error', 'Failed to create teams');
            }
          } catch (error) {
            Alert.alert('Error', 'Failed to create teams');
          } finally {
            setIsProcessing(false);
          }
        }}
      />
    </View>
  );
}

// Player Chip Component
function PlayerChip({
  registration,
  isSelected,
  onPress,
  skillInfo,
}: {
  registration: TournamentRegistrationDto;
  isSelected: boolean;
  onPress: () => void;
  skillInfo: { level: SkillLevel | null; color: string };
}) {
  const { user, position } = registration;
  const fullName = `${user.firstName} ${user.lastName}`;

  return (
    <TouchableOpacity
      style={[styles.playerChip, isSelected && styles.playerChipSelected]}
      onPress={onPress}
    >
      {/* Skill level indicator */}
      {skillInfo.level && (
        <View style={[styles.skillIndicator, { backgroundColor: skillInfo.color }]}>
          <Text style={styles.skillText}>{skillInfo.level}</Text>
        </View>
      )}

      <View style={styles.playerChipContent}>
        <Text style={styles.playerName} numberOfLines={1}>
          {fullName}
        </Text>
        <View style={styles.playerMeta}>
          {position && (
            <Badge variant="default" style={styles.positionBadge}>
              {position}
            </Badge>
          )}
          <BadgeIconsRow
            badges={user.badges || []}
            totalCount={user.totalBadgeCount || 0}
          />
        </View>
      </View>
    </TouchableOpacity>
  );
}

// Team Section Component
function TeamSection({
  team,
  players,
  maxPlayers,
  isHighlighted,
  onTap,
  onPlayerTap,
  getSkillLevelInfo,
}: {
  team: TournamentTeamDto;
  players: TournamentRegistrationDto[];
  maxPlayers?: number;
  isHighlighted: boolean;
  onTap: () => void;
  onPlayerTap: (registration: TournamentRegistrationDto) => void;
  getSkillLevelInfo: (registration: TournamentRegistrationDto) => {
    level: SkillLevel | null;
    color: string;
  };
}) {
  const playerCount = players.length;
  const countText = maxPlayers ? `${playerCount}/${maxPlayers}` : `${playerCount}`;

  return (
    <Pressable
      style={[styles.teamSection, isHighlighted && styles.teamSectionHighlighted]}
      onPress={onTap}
      disabled={!isHighlighted}
    >
      <View style={styles.teamHeader}>
        <Text style={styles.teamName}>{team.name}</Text>
        <Text style={styles.teamCount}>{countText}</Text>
      </View>

      {players.length === 0 ? (
        <View style={styles.emptyTeam}>
          <Text style={styles.emptyTeamText}>No players assigned</Text>
        </View>
      ) : (
        <View style={styles.teamPlayersGrid}>
          {players.map((registration) => {
            const skillInfo = getSkillLevelInfo(registration);
            return (
              <PlayerChip
                key={registration.id}
                registration={registration}
                isSelected={false}
                onPress={() => onPlayerTap(registration)}
                skillInfo={skillInfo}
              />
            );
          })}
        </View>
      )}
    </Pressable>
  );
}

// Create Teams Modal Component
function CreateTeamsModal({
  visible,
  onClose,
  onSubmit,
}: {
  visible: boolean;
  onClose: () => void;
  onSubmit: (count: number, namePrefix: string) => void;
}) {
  const [count, setCount] = useState('4');
  const [namePrefix, setNamePrefix] = useState('Team');

  const handleSubmit = () => {
    const teamCount = parseInt(count, 10);
    if (isNaN(teamCount) || teamCount < 1 || teamCount > 32) {
      Alert.alert('Invalid Count', 'Please enter a number between 1 and 32');
      return;
    }
    if (!namePrefix.trim()) {
      Alert.alert('Invalid Prefix', 'Please enter a team name prefix');
      return;
    }
    onSubmit(teamCount, namePrefix.trim());
  };

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable style={styles.modalOverlay} onPress={onClose}>
        <Pressable style={styles.modalContent} onPress={(e) => e.stopPropagation()}>
          <Text style={styles.modalTitle}>Create Teams</Text>

          <View style={styles.inputGroup}>
            <Text style={styles.inputLabel}>Number of Teams</Text>
            <TextInput
              style={styles.input}
              value={count}
              onChangeText={setCount}
              keyboardType="number-pad"
              placeholder="4"
              placeholderTextColor={colors.text.muted}
            />
          </View>

          <View style={styles.inputGroup}>
            <Text style={styles.inputLabel}>Name Prefix</Text>
            <TextInput
              style={styles.input}
              value={namePrefix}
              onChangeText={setNamePrefix}
              placeholder="Team"
              placeholderTextColor={colors.text.muted}
            />
            <Text style={styles.inputHint}>
              Teams will be named: {namePrefix} 1, {namePrefix} 2, etc.
            </Text>
          </View>

          <View style={styles.modalActions}>
            <TouchableOpacity style={styles.modalButton} onPress={onClose}>
              <Text style={styles.modalButtonText}>Cancel</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[styles.modalButton, styles.modalButtonPrimary]}
              onPress={handleSubmit}
            >
              <Text style={[styles.modalButtonText, styles.modalButtonPrimaryText]}>
                Create
              </Text>
            </TouchableOpacity>
          </View>
        </Pressable>
      </Pressable>
    </Modal>
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
  scrollContent: {
    padding: spacing.md,
  },

  // Header Actions
  headerActions: {
    flexDirection: 'row',
    gap: spacing.sm,
    marginBottom: spacing.md,
  },
  actionButton: {
    flex: 1,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    alignItems: 'center',
  },
  actionButtonPrimary: {
    backgroundColor: colors.primary.teal,
    borderColor: colors.primary.teal,
  },
  actionButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.primary,
  },
  actionButtonPrimaryText: {
    color: colors.bg.darkest,
  },

  // Hint
  hintContainer: {
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    backgroundColor: colors.subtle.teal,
    borderRadius: radius.md,
    marginBottom: spacing.md,
    borderWidth: 1,
    borderColor: colors.primary.teal,
  },
  hintText: {
    fontSize: 14,
    color: colors.primary.teal,
    textAlign: 'center',
    fontWeight: '600',
  },

  // Section
  section: {
    marginBottom: spacing.lg,
  },
  sectionHeader: {
    marginBottom: spacing.sm,
  },
  sectionTitle: {
    fontSize: 12,
    fontWeight: '700',
    color: colors.text.muted,
    letterSpacing: 1,
  },
  emptySection: {
    padding: spacing.lg,
    backgroundColor: colors.bg.dark,
    borderRadius: radius.md,
    alignItems: 'center',
  },
  emptyText: {
    fontSize: 14,
    color: colors.text.muted,
  },

  // Players Grid
  playersGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.sm,
  },

  // Player Chip
  playerChip: {
    width: '48%',
    backgroundColor: colors.bg.dark,
    borderRadius: radius.md,
    padding: spacing.sm,
    borderWidth: 2,
    borderColor: colors.border.default,
    position: 'relative',
  },
  playerChipSelected: {
    borderColor: colors.primary.teal,
    backgroundColor: colors.subtle.teal,
  },
  skillIndicator: {
    position: 'absolute',
    top: 4,
    left: 4,
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: radius.sm,
  },
  skillText: {
    fontSize: 9,
    fontWeight: '700',
    color: colors.bg.darkest,
  },
  playerChipContent: {
    gap: spacing.xs,
  },
  playerName: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.primary,
    paddingTop: 16, // Space for skill indicator
  },
  playerMeta: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.xs,
  },
  positionBadge: {
    fontSize: 10,
    paddingHorizontal: 6,
    paddingVertical: 2,
  },

  // Team Section
  teamSection: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.md,
    borderWidth: 2,
    borderColor: colors.border.default,
  },
  teamSectionHighlighted: {
    borderColor: colors.primary.teal,
    backgroundColor: colors.bg.elevated,
  },
  teamHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  teamName: {
    fontSize: 16,
    fontWeight: '700',
    color: colors.text.primary,
  },
  teamCount: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.primary.teal,
  },
  emptyTeam: {
    padding: spacing.md,
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border.default,
    borderRadius: radius.md,
    borderStyle: 'dashed',
  },
  emptyTeamText: {
    fontSize: 13,
    color: colors.text.subtle,
  },
  teamPlayersGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: spacing.sm,
  },

  // Empty Container
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: spacing.xl,
  },

  // Modal
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.7)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.lg,
  },
  modalContent: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.lg,
    width: '100%',
    maxWidth: 400,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  modalTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.lg,
  },
  inputGroup: {
    marginBottom: spacing.md,
  },
  inputLabel: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.secondary,
    marginBottom: spacing.xs,
  },
  input: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.sm,
    fontSize: 15,
    color: colors.text.primary,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  inputHint: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: spacing.xs,
  },
  modalActions: {
    flexDirection: 'row',
    gap: spacing.sm,
    marginTop: spacing.md,
  },
  modalButton: {
    flex: 1,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border.default,
    alignItems: 'center',
  },
  modalButtonPrimary: {
    backgroundColor: colors.primary.teal,
    borderColor: colors.primary.teal,
  },
  modalButtonText: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.text.primary,
  },
  modalButtonPrimaryText: {
    color: colors.bg.darkest,
  },
});
