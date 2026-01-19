import { useState, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { useLocalSearchParams, Stack, useFocusEffect, router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { colors, spacing, radius } from '../../../../theme';
import type { TeamStandingDto, TiedGroupDto } from '@bhmhockey/shared';

// Format goal differential with sign
const formatGoalDiff = (diff: number): string => {
  if (diff > 0) return `+${diff}`;
  return String(diff);
};

export default function ManageStandingsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const {
    standings,
    tiedGroups,
    fetchStandings,
    resolveTies,
    isLoading,
  } = useTournamentStore();

  // Local state: Map<groupIndex, teamId[]> to track reordering within each tied group
  const [groupOrders, setGroupOrders] = useState<Map<number, string[]>>(new Map());
  const [isSaving, setIsSaving] = useState(false);

  // Fetch standings when screen focuses
  useFocusEffect(
    useCallback(() => {
      if (id) {
        fetchStandings(id);
      }
    }, [id])
  );

  // Initialize groupOrders when tiedGroups changes
  useFocusEffect(
    useCallback(() => {
      if (tiedGroups && tiedGroups.length > 0) {
        const initialOrders = new Map<number, string[]>();
        tiedGroups.forEach((group, index) => {
          initialOrders.set(index, [...group.teamIds]);
        });
        setGroupOrders(initialOrders);
      }
    }, [tiedGroups])
  );

  // Move team up in the order
  const moveUp = (groupIndex: number, teamId: string) => {
    const currentOrder = groupOrders.get(groupIndex);
    if (!currentOrder) return;

    const idx = currentOrder.indexOf(teamId);
    if (idx <= 0) return; // Already at top or not found

    const newOrder = [...currentOrder];
    [newOrder[idx - 1], newOrder[idx]] = [newOrder[idx], newOrder[idx - 1]];

    setGroupOrders(prev => new Map(prev).set(groupIndex, newOrder));
  };

  // Move team down in the order
  const moveDown = (groupIndex: number, teamId: string) => {
    const currentOrder = groupOrders.get(groupIndex);
    if (!currentOrder) return;

    const idx = currentOrder.indexOf(teamId);
    if (idx < 0 || idx >= currentOrder.length - 1) return; // Already at bottom or not found

    const newOrder = [...currentOrder];
    [newOrder[idx], newOrder[idx + 1]] = [newOrder[idx + 1], newOrder[idx]];

    setGroupOrders(prev => new Map(prev).set(groupIndex, newOrder));
  };

  // Get team details by ID
  const getTeamById = (teamId: string): TeamStandingDto | undefined => {
    return standings.find(s => s.teamId === teamId);
  };

  // Get the starting rank for a tied group
  const getStartingRank = (group: TiedGroupDto): number => {
    const firstTeam = getTeamById(group.teamIds[0]);
    return firstTeam?.rank || 1;
  };

  // Check if any changes have been made
  const hasChanges = (): boolean => {
    if (!tiedGroups) return false;

    for (let i = 0; i < tiedGroups.length; i++) {
      const original = tiedGroups[i].teamIds;
      const current = groupOrders.get(i) || [];

      if (original.length !== current.length) return true;
      for (let j = 0; j < original.length; j++) {
        if (original[j] !== current[j]) return true;
      }
    }

    return false;
  };

  // Save the reordered standings
  const handleSave = async () => {
    if (!id || !tiedGroups) return;

    setIsSaving(true);
    const resolutions: { teamId: string; finalPlacement: number }[] = [];

    // For each tied group, assign final placements based on the new order
    tiedGroups.forEach((group, groupIndex) => {
      const startRank = getStartingRank(group);
      const teamIds = groupOrders.get(groupIndex) || group.teamIds;

      teamIds.forEach((teamId, index) => {
        resolutions.push({
          teamId,
          finalPlacement: startRank + index,
        });
      });
    });

    const success = await resolveTies(id, resolutions);
    setIsSaving(false);

    if (success) {
      Alert.alert('Success', 'Standings updated successfully', [
        {
          text: 'OK',
          onPress: () => {
            // Refresh standings and navigate back
            fetchStandings(id);
            router.back();
          },
        },
      ]);
    } else {
      Alert.alert('Error', 'Failed to update standings. Please try again.');
    }
  };

  // Confirm save if changes were made
  const confirmSave = () => {
    Alert.alert(
      'Confirm Changes',
      'Are you sure you want to save these standings adjustments?',
      [
        { text: 'Cancel', style: 'cancel' },
        { text: 'Save', onPress: handleSave, style: 'default' },
      ]
    );
  };

  // Show loading spinner only on initial load
  if (isLoading && standings.length === 0) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Manage Standings',
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

  // No ties to resolve
  if (!tiedGroups || tiedGroups.length === 0) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Manage Standings',
            headerBackTitle: 'Back',
            headerStyle: { backgroundColor: colors.bg.dark },
            headerTintColor: colors.text.primary,
          }}
        />
        <View style={styles.emptyContainer}>
          <Ionicons name="checkmark-circle-outline" size={64} color={colors.primary.teal} />
          <Text style={styles.emptyTitle}>No Ties to Resolve</Text>
          <Text style={styles.emptyMessage}>
            All teams have unique standings. No manual resolution needed.
          </Text>
          <TouchableOpacity style={styles.backButton} onPress={() => router.back()}>
            <Text style={styles.backButtonText}>Back to Standings</Text>
          </TouchableOpacity>
        </View>
      </>
    );
  }

  return (
    <View style={styles.container}>
      <Stack.Screen
        options={{
          title: 'Resolve Ties',
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
          headerRight: () =>
            hasChanges() ? (
              <TouchableOpacity onPress={confirmSave} disabled={isSaving}>
                {isSaving ? (
                  <ActivityIndicator size="small" color={colors.primary.teal} />
                ) : (
                  <Text style={styles.saveButtonText}>Save</Text>
                )}
              </TouchableOpacity>
            ) : null,
        }}
      />

      <ScrollView
        style={styles.scrollView}
        contentContainerStyle={styles.scrollContent}
      >
        {/* Instructions */}
        <View style={styles.instructionsCard}>
          <Ionicons
            name="information-circle"
            size={20}
            color={colors.primary.teal}
            style={styles.instructionsIcon}
          />
          <View style={styles.instructionsTextContainer}>
            <Text style={styles.instructionsTitle}>Resolve Tied Teams</Text>
            <Text style={styles.instructionsMessage}>
              Use the arrows to manually set the order for tied teams. Teams will be
              assigned final placements based on your order.
            </Text>
          </View>
        </View>

        {/* Tied Groups */}
        {tiedGroups.map((group, groupIndex) => {
          const startRank = getStartingRank(group);
          const endRank = startRank + group.teamIds.length - 1;
          const currentOrder = groupOrders.get(groupIndex) || group.teamIds;

          return (
            <View key={groupIndex} style={styles.tiedGroupCard}>
              {/* Group Header */}
              <View style={styles.groupHeader}>
                <Text style={styles.groupTitle}>
                  Tied Teams (positions {startRank}-{endRank})
                </Text>
              </View>

              {/* Reason */}
              <View style={styles.reasonContainer}>
                <Ionicons
                  name="alert-circle"
                  size={14}
                  color={colors.status.warning}
                  style={styles.reasonIcon}
                />
                <Text style={styles.reasonText}>{group.reason}</Text>
              </View>

              {/* Team List */}
              <View style={styles.teamList}>
                {currentOrder.map((teamId, index) => {
                  const team = getTeamById(teamId);
                  if (!team) return null;

                  const isFirst = index === 0;
                  const isLast = index === currentOrder.length - 1;

                  return (
                    <View key={teamId} style={styles.teamRow}>
                      {/* Rank Indicator */}
                      <View style={styles.rankIndicator}>
                        <Text style={styles.rankIndicatorText}>
                          {startRank + index}
                        </Text>
                      </View>

                      {/* Team Info */}
                      <View style={styles.teamInfo}>
                        <Text style={styles.teamName} numberOfLines={1}>
                          {team.teamName}
                        </Text>
                        <View style={styles.teamStats}>
                          <Text style={styles.statItem}>
                            {team.wins}-{team.losses}-{team.ties}
                          </Text>
                          <Text style={styles.statDivider}>•</Text>
                          <Text style={styles.statItem}>{team.points} PTS</Text>
                          <Text style={styles.statDivider}>•</Text>
                          <Text
                            style={[
                              styles.statItem,
                              team.goalDifferential > 0 && styles.goalDiffPositive,
                              team.goalDifferential < 0 && styles.goalDiffNegative,
                            ]}
                          >
                            {formatGoalDiff(team.goalDifferential)} GD
                          </Text>
                          <Text style={styles.statDivider}>•</Text>
                          <Text style={styles.statItem}>{team.goalsFor} GF</Text>
                        </View>
                      </View>

                      {/* Arrow Controls */}
                      <View style={styles.arrowControls}>
                        <TouchableOpacity
                          style={[
                            styles.arrowButton,
                            isFirst && styles.arrowButtonDisabled,
                          ]}
                          onPress={() => moveUp(groupIndex, teamId)}
                          disabled={isFirst}
                        >
                          <Ionicons
                            name="chevron-up"
                            size={20}
                            color={isFirst ? colors.text.subtle : colors.primary.teal}
                          />
                        </TouchableOpacity>
                        <TouchableOpacity
                          style={[
                            styles.arrowButton,
                            isLast && styles.arrowButtonDisabled,
                          ]}
                          onPress={() => moveDown(groupIndex, teamId)}
                          disabled={isLast}
                        >
                          <Ionicons
                            name="chevron-down"
                            size={20}
                            color={isLast ? colors.text.subtle : colors.primary.teal}
                          />
                        </TouchableOpacity>
                      </View>
                    </View>
                  );
                })}
              </View>
            </View>
          );
        })}

        {/* Bottom Save Button (mobile-friendly) */}
        {hasChanges() && (
          <TouchableOpacity
            style={[styles.saveButton, isSaving && styles.saveButtonDisabled]}
            onPress={confirmSave}
            disabled={isSaving}
          >
            {isSaving ? (
              <ActivityIndicator size="small" color={colors.bg.darkest} />
            ) : (
              <Text style={styles.saveButtonLabel}>Save Changes</Text>
            )}
          </TouchableOpacity>
        )}
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
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: colors.bg.darkest,
    paddingHorizontal: spacing.xl,
  },
  emptyTitle: {
    fontSize: 20,
    fontWeight: '600',
    color: colors.text.primary,
    marginTop: spacing.md,
    marginBottom: spacing.xs,
  },
  emptyMessage: {
    fontSize: 14,
    color: colors.text.muted,
    textAlign: 'center',
    lineHeight: 20,
  },
  backButton: {
    marginTop: spacing.lg,
    backgroundColor: colors.primary.teal,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
    borderRadius: radius.md,
  },
  backButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.bg.darkest,
  },
  scrollView: {
    flex: 1,
  },
  scrollContent: {
    padding: spacing.md,
  },
  saveButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.primary.teal,
    marginRight: spacing.sm,
  },

  // Instructions Card
  instructionsCard: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    backgroundColor: colors.subtle.teal,
    borderWidth: 1,
    borderColor: colors.primary.teal,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.md,
  },
  instructionsIcon: {
    marginTop: 2,
  },
  instructionsTextContainer: {
    flex: 1,
    marginLeft: spacing.sm,
  },
  instructionsTitle: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.primary.teal,
    marginBottom: spacing.xs,
  },
  instructionsMessage: {
    fontSize: 13,
    color: colors.text.secondary,
    lineHeight: 18,
  },

  // Tied Group Card
  tiedGroupCard: {
    backgroundColor: colors.bg.dark,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderRadius: radius.md,
    padding: spacing.md,
    marginBottom: spacing.md,
  },
  groupHeader: {
    marginBottom: spacing.sm,
  },
  groupTitle: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.text.primary,
  },
  reasonContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.status.warningSubtle,
    borderRadius: radius.sm,
    padding: spacing.sm,
    marginBottom: spacing.md,
  },
  reasonIcon: {
    marginRight: spacing.xs,
  },
  reasonText: {
    flex: 1,
    fontSize: 12,
    color: colors.text.secondary,
    lineHeight: 16,
  },

  // Team List
  teamList: {
    gap: spacing.sm,
  },
  teamRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.sm,
    padding: spacing.sm,
  },
  rankIndicator: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: colors.primary.teal,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.sm,
  },
  rankIndicatorText: {
    fontSize: 14,
    fontWeight: '700',
    color: colors.bg.darkest,
  },
  teamInfo: {
    flex: 1,
  },
  teamName: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.text.primary,
    marginBottom: 4,
  },
  teamStats: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  statItem: {
    fontSize: 12,
    fontWeight: '500',
    color: colors.text.muted,
  },
  statDivider: {
    fontSize: 12,
    color: colors.text.subtle,
    marginHorizontal: spacing.xs,
  },
  goalDiffPositive: {
    color: colors.status.success,
  },
  goalDiffNegative: {
    color: colors.status.error,
  },

  // Arrow Controls
  arrowControls: {
    flexDirection: 'column',
    gap: 2,
    marginLeft: spacing.sm,
  },
  arrowButton: {
    width: 32,
    height: 32,
    borderRadius: radius.sm,
    backgroundColor: colors.bg.dark,
    borderWidth: 1,
    borderColor: colors.border.default,
    justifyContent: 'center',
    alignItems: 'center',
  },
  arrowButtonDisabled: {
    opacity: 0.3,
  },

  // Bottom Save Button
  saveButton: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
    justifyContent: 'center',
    marginTop: spacing.md,
  },
  saveButtonDisabled: {
    opacity: 0.6,
  },
  saveButtonLabel: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.bg.darkest,
  },
});
