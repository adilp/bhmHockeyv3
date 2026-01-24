import { useState, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
  RefreshControl,
} from 'react-native';
import { useLocalSearchParams, useRouter, Stack } from 'expo-router';
import { useTournamentStore } from '../../../../stores/tournamentStore';
import { tournamentService } from '@bhmhockey/api-client';
import { EmptyState } from '../../../../components';
import { colors, spacing, radius } from '../../../../theme';
import type { TournamentTeamDto } from '@bhmhockey/shared';

interface TeamWithMemberCount extends TournamentTeamDto {
  memberCount: number;
  isLoadingCount: boolean;
}

export default function BrowseTeamsScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [teamsWithCounts, setTeamsWithCounts] = useState<TeamWithMemberCount[]>([]);

  const { teams, fetchTeams, isLoading } = useTournamentStore();

  // Initialize teams with member counts
  useEffect(() => {
    if (teams.length > 0) {
      // Convert teams to teams with counts
      const teamsWithCountsData: TeamWithMemberCount[] = teams.map(team => ({
        ...team,
        memberCount: 0,
        isLoadingCount: true,
      }));
      setTeamsWithCounts(teamsWithCountsData);

      // Fetch member counts for each team
      teams.forEach((team) => {
        fetchMemberCount(team.id);
      });
    } else {
      setTeamsWithCounts([]);
    }
  }, [teams]);

  const fetchMemberCount = async (teamId: string) => {
    if (!id) return;
    try {
      const members = await tournamentService.getTeamMembers(id, teamId);
      // Filter only accepted members
      const acceptedMembers = members.filter(m => m.status === 'Accepted');
      setTeamsWithCounts(prev =>
        prev.map(team =>
          team.id === teamId
            ? { ...team, memberCount: acceptedMembers.length, isLoadingCount: false }
            : team
        )
      );
    } catch (error) {
      console.error(`Failed to fetch members for team ${teamId}:`, error);
      // Mark as not loading even on error
      setTeamsWithCounts(prev =>
        prev.map(team =>
          team.id === teamId
            ? { ...team, isLoadingCount: false }
            : team
        )
      );
    }
  };

  const handleRefresh = async () => {
    if (!id) return;
    setIsRefreshing(true);
    try {
      await fetchTeams(id);
    } finally {
      setIsRefreshing(false);
    }
  };

  const handleContactCaptain = (team: TeamWithMemberCount) => {
    const captainName = team.captainName
      ? team.captainName
      : 'the team captain';

    Alert.alert(
      'Contact Captain',
      `Contact ${captainName} to request joining ${team.name}.`,
      [{ text: 'OK' }]
    );
  };

  const renderTeamCard = ({ item }: { item: TeamWithMemberCount }) => (
    <View style={styles.teamCard}>
      {/* Team Name */}
      <Text style={styles.teamName} numberOfLines={1}>
        {item.name}
      </Text>

      {/* Captain Name */}
      {item.captainName && (
        <View style={styles.infoRow}>
          <Text style={styles.infoLabel}>Captain:</Text>
          <Text style={styles.infoValue} numberOfLines={1}>
            {item.captainName}
          </Text>
        </View>
      )}

      {/* Roster Count */}
      <View style={styles.infoRow}>
        <Text style={styles.infoLabel}>Roster:</Text>
        {item.isLoadingCount ? (
          <ActivityIndicator size="small" color={colors.primary.teal} />
        ) : (
          <Text style={styles.infoValue}>
            {item.memberCount} {item.memberCount === 1 ? 'player' : 'players'}
          </Text>
        )}
      </View>

      {/* Contact Button */}
      <TouchableOpacity
        style={styles.contactButton}
        onPress={() => handleContactCaptain(item)}
        activeOpacity={0.7}
      >
        <Text style={styles.contactButtonText}>Contact Captain</Text>
      </TouchableOpacity>
    </View>
  );

  const ListEmptyComponent = () => (
    <View style={styles.emptyContainer}>
      <EmptyState
        title="No Teams Available"
        message="There are no teams with open roster spots at this time."
      />
    </View>
  );

  // Show loading spinner only on initial load
  if (isLoading && teams.length === 0) {
    return (
      <>
        <Stack.Screen
          options={{
            title: 'Browse Teams',
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
          title: 'Browse Teams',
          headerBackTitle: 'Back',
          headerStyle: { backgroundColor: colors.bg.dark },
          headerTintColor: colors.text.primary,
        }}
      />

      <FlatList
        data={teamsWithCounts}
        keyExtractor={(item) => item.id}
        renderItem={renderTeamCard}
        ListEmptyComponent={ListEmptyComponent}
        contentContainerStyle={[
          styles.listContent,
          teamsWithCounts.length === 0 && styles.emptyListContent,
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

  // Team Card
  teamCard: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  teamName: {
    fontSize: 17,
    fontWeight: '700',
    color: colors.text.primary,
    marginBottom: spacing.sm,
  },
  infoRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.xs,
  },
  infoLabel: {
    fontSize: 14,
    color: colors.text.secondary,
    width: 70,
  },
  infoValue: {
    flex: 1,
    fontSize: 14,
    color: colors.text.primary,
    fontWeight: '500',
  },

  // Contact Button
  contactButton: {
    marginTop: spacing.sm,
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
    backgroundColor: colors.bg.darkest,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.primary.teal,
    alignItems: 'center',
  },
  contactButtonText: {
    fontSize: 15,
    fontWeight: '600',
    color: colors.primary.teal,
  },

  separator: {
    height: spacing.md,
  },
});
