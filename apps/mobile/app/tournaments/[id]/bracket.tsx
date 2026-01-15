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
import { useLocalSearchParams, useRouter, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';

import { useTournamentStore } from '../../../stores/tournamentStore';
import { BracketMatchBox } from '../../../components';
import { colors, spacing, radius } from '../../../theme';
import type { TournamentMatchDto } from '@bhmhockey/shared';

/**
 * Get human-readable round name based on position from final
 */
function getRoundName(round: number, totalRounds: number): string {
  const fromFinal = totalRounds - round;
  if (fromFinal === 0) return 'Final';
  if (fromFinal === 1) return 'Semifinals';
  if (fromFinal === 2) return 'Quarterfinals';
  return `Round ${round}`;
}

/**
 * Get short round name for chips
 */
function getShortRoundName(round: number, totalRounds: number): string {
  const fromFinal = totalRounds - round;
  if (fromFinal === 0) return 'Final';
  if (fromFinal === 1) return 'SF';
  if (fromFinal === 2) return 'QF';
  return `R${round}`;
}

interface RoundSection {
  title: string;
  round: number;
  data: TournamentMatchDto[];
}

export default function BracketScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const [selectedRound, setSelectedRound] = useState<number | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const { matches, currentTournament, fetchMatches, isLoading } = useTournamentStore(
    (state) => ({
      matches: state.matches,
      currentTournament: state.currentTournament,
      fetchMatches: state.fetchMatches,
      isLoading: state.isLoading,
    })
  );

  // Fetch matches on focus
  useFocusEffect(
    useCallback(() => {
      if (id) {
        fetchMatches(id);
      }
    }, [id, fetchMatches])
  );

  // Calculate total rounds for naming
  const totalRounds = useMemo(() => {
    if (matches.length === 0) return 0;
    return Math.max(...matches.map((m) => m.round));
  }, [matches]);

  // Get unique rounds for chips
  const rounds = useMemo(() => {
    return [...new Set(matches.map((m) => m.round))].sort((a, b) => a - b);
  }, [matches]);

  // Group matches by round into sections
  const sections: RoundSection[] = useMemo(() => {
    const filteredMatches =
      selectedRound === null
        ? matches
        : matches.filter((m) => m.round === selectedRound);

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
        title: getRoundName(Number(round), totalRounds),
        round: Number(round),
        data: roundMatches.sort((a, b) => a.matchNumber - b.matchNumber),
      }))
      .sort((a, b) => a.round - b.round);
  }, [matches, selectedRound, totalRounds]);

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
      // Navigate to score entry modal/screen
      router.push(`/tournaments/${id}/match/${match.id}`);
    },
    [currentTournament?.canManage, router, id]
  );

  // Render round selector chip
  const renderRoundChip = useCallback(
    (round: number | null, label: string) => {
      const isSelected = selectedRound === round;
      return (
        <TouchableOpacity
          key={round ?? 'all'}
          style={[styles.chip, isSelected && styles.chipSelected]}
          onPress={() => setSelectedRound(round)}
          activeOpacity={0.7}
        >
          <Text style={[styles.chipText, isSelected && styles.chipTextSelected]}>
            {label}
          </Text>
        </TouchableOpacity>
      );
    },
    [selectedRound]
  );

  // Render section header
  const renderSectionHeader = useCallback(
    ({ section }: { section: RoundSection }) => (
      <View style={styles.sectionHeader}>
        <Text style={styles.sectionTitle}>{section.title}</Text>
        <Text style={styles.matchCount}>
          {section.data.length} {section.data.length === 1 ? 'match' : 'matches'}
        </Text>
      </View>
    ),
    []
  );

  // Render match item
  const renderMatchItem = useCallback(
    ({ item }: { item: TournamentMatchDto }) => (
      <View style={styles.matchContainer}>
        <BracketMatchBox
          match={item}
          onPress={() => handleMatchPress(item)}
          canEdit={currentTournament?.canManage ?? false}
        />
      </View>
    ),
    [handleMatchPress, currentTournament?.canManage]
  );

  // Render empty state
  const renderEmptyState = () => (
    <View style={styles.emptyContainer}>
      <Ionicons name="git-branch-outline" size={64} color={colors.text.muted} />
      <Text style={styles.emptyTitle}>No Bracket Yet</Text>
      <Text style={styles.emptySubtitle}>
        The bracket will appear here once it has been generated.
      </Text>
    </View>
  );

  // Show loading only on initial load
  const showLoading = isLoading && matches.length === 0;

  return (
    <View style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <TouchableOpacity
          style={styles.backButton}
          onPress={() => router.back()}
          activeOpacity={0.7}
        >
          <Ionicons name="arrow-back" size={24} color={colors.text.primary} />
        </TouchableOpacity>
        <View style={styles.headerContent}>
          <Text style={styles.headerTitle}>Bracket</Text>
          {currentTournament && (
            <Text style={styles.headerSubtitle} numberOfLines={1}>
              {currentTournament.name}
            </Text>
          )}
        </View>
      </View>

      {/* Round selector chips */}
      {rounds.length > 0 && (
        <View style={styles.chipContainer}>
          {renderRoundChip(null, 'All')}
          {rounds.map((round) =>
            renderRoundChip(round, getShortRoundName(round, totalRounds))
          )}
        </View>
      )}

      {/* Content */}
      {showLoading ? (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={colors.primary.teal} />
        </View>
      ) : matches.length === 0 ? (
        renderEmptyState()
      ) : (
        <SectionList
          sections={sections}
          keyExtractor={(item) => item.id}
          renderItem={renderMatchItem}
          renderSectionHeader={renderSectionHeader}
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

  // Header styles
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: spacing.md,
    paddingTop: spacing.xl + spacing.md, // Account for status bar
    paddingBottom: spacing.md,
    backgroundColor: colors.bg.dark,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  backButton: {
    width: 40,
    height: 40,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.sm,
  },
  headerContent: {
    flex: 1,
  },
  headerTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: colors.text.primary,
  },
  headerSubtitle: {
    fontSize: 13,
    color: colors.text.muted,
    marginTop: 2,
  },

  // Chip styles
  chipContainer: {
    flexDirection: 'row',
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    backgroundColor: colors.bg.dark,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
    gap: spacing.sm,
  },
  chip: {
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.round,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  chipSelected: {
    backgroundColor: colors.subtle.teal,
    borderColor: colors.primary.teal,
  },
  chipText: {
    fontSize: 13,
    fontWeight: '500',
    color: colors.text.muted,
  },
  chipTextSelected: {
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

  // Loading styles
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },

  // Empty state styles
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
