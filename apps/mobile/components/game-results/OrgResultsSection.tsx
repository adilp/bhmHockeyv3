import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import type { GameResultSummaryDto, LeaderboardStat } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../theme';
import { useGameResultStore, boardKey } from '../../stores/gameResultStore';
import { useAuthStore } from '../../stores/authStore';
import { shortDate } from '../../utils/gameResults';
import { LeaderboardTable, StatPicker } from './LeaderboardTable';
import { SafeBoundary } from './SafeBoundary';

const TOP_N = 5;

function ScoreChip({ label, score, dark }: { label: string; score: number; dark: boolean }) {
  return (
    <View style={[styles.chip, dark ? styles.chipDark : styles.chipLight]}>
      <Text style={[styles.chipText, { color: dark ? colors.text.primary : colors.bg.darkest }]} allowFontScaling={false}>
        {`${label} ${score}`}
      </Text>
    </View>
  );
}

function ResultRow({ r }: { r: GameResultSummaryDto }) {
  const black = r.black.label ?? 'Black';
  const white = r.white.label ?? 'White';
  // the winner first, as the comp
  const whiteFirst = r.white.score >= r.black.score;
  return (
    <TouchableOpacity style={styles.resultRow} onPress={() => router.push(`/events/${r.eventId}`)}>
      <View style={{ flex: 1 }}>
        <Text style={styles.date} allowFontScaling={false}>{shortDate(r.eventDate)}</Text>
        <Text style={styles.muted} allowFontScaling={false} numberOfLines={1}>{r.eventName ?? `${white} vs ${black}`}</Text>
      </View>
      {whiteFirst ? (
        <>
          <ScoreChip label={white} score={r.white.score} dark={false} />
          <ScoreChip label={black} score={r.black.score} dark />
        </>
      ) : (
        <>
          <ScoreChip label={black} score={r.black.score} dark />
          <ScoreChip label={white} score={r.white.score} dark={false} />
        </>
      )}
      <Ionicons name="chevron-forward" size={18} color={colors.text.muted} />
    </TouchableOpacity>
  );
}

/**
 * The organization page's "Recent results" and "Leaders" (comp 05). Loads only
 * for a member (the caller passes `isMember`) and renders NOTHING unless the
 * server answered with data - so on a server without these endpoints, or with
 * nothing published, the organization page is exactly as it was.
 */
function OrgResultsSectionInner({ organizationId, isMember }: { organizationId: string; isMember: boolean }) {
  const season = String(new Date().getFullYear());
  const [stat, setStat] = useState<LeaderboardStat>('points');
  const userId = useAuthStore((s) => s.user?.id);
  const recent = useGameResultStore((s) => s.recentResults[organizationId]);
  const board = useGameResultStore((s) => s.leaderboards[boardKey(organizationId, stat, season)]);
  const fetchRecentResults = useGameResultStore((s) => s.fetchRecentResults);
  const fetchLeaderboard = useGameResultStore((s) => s.fetchLeaderboard);

  useEffect(() => {
    if (isMember) fetchRecentResults(organizationId, 3);
  }, [organizationId, isMember, fetchRecentResults]);
  useEffect(() => {
    if (isMember) fetchLeaderboard(organizationId, stat, season);
  }, [organizationId, isMember, stat, season, fetchLeaderboard]);

  const results = recent?.status === 'ready' ? recent.data ?? [] : [];
  const leaders = board?.data && board.data.rows.length > 0 ? board.data : null;
  if (!isMember || (results.length === 0 && !leaders)) return null;

  return (
    <View>
      {results.length > 0 && (
        <View style={styles.section}>
          <Text style={styles.sectionLabel} allowFontScaling={false}>Recent Results</Text>
          {results.map((r) => <ResultRow key={r.eventId} r={r} />)}
        </View>
      )}
      {leaders && (
        <View style={styles.section}>
          <View style={styles.rowBetween}>
            <Text style={styles.sectionLabel} allowFontScaling={false}>{`Leaders · ${season}`}</Text>
            <Text style={styles.muted} allowFontScaling={false}>
              {`${leaders.gamesWithStats} game${leaders.gamesWithStats === 1 ? '' : 's'} with stats`}
            </Text>
          </View>
          <StatPicker stat={stat} onChange={setStat} />
          <LeaderboardTable
            rows={leaders.rows.slice(0, TOP_N)}
            stat={stat}
            currentUserId={userId}
            onOpenPlayer={(u) => router.push(`/players/${u}`)}
          />
          <TouchableOpacity style={styles.fullButton} onPress={() => router.push(`/organizations/${organizationId}/leaderboard`)}>
            <Text style={styles.fullButtonText} allowFontScaling={false}>Full leaderboard</Text>
          </TouchableOpacity>
          <Text style={styles.footnote} allowFontScaling={false}>Players not in the app are not ranked.</Text>
        </View>
      )}
    </View>
  );
}

export function OrgResultsSection(props: { organizationId: string; isMember: boolean }) {
  return (
    <SafeBoundary name="organization results">
      <OrgResultsSectionInner {...props} />
    </SafeBoundary>
  );
}

const styles = StyleSheet.create({
  section: {
    backgroundColor: colors.bg.dark,
    padding: spacing.md,
    marginBottom: spacing.sm,
  },
  sectionLabel: { fontSize: 13, fontWeight: '600', color: colors.text.muted, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: spacing.sm },
  rowBetween: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'baseline' },
  muted: { color: colors.text.muted, fontSize: 13 },
  resultRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, paddingVertical: spacing.sm },
  date: { color: colors.text.primary, fontSize: 16, fontWeight: '600' },
  chip: { paddingHorizontal: spacing.sm, paddingVertical: spacing.xs, borderRadius: radius.sm, borderWidth: 1 },
  chipDark: { backgroundColor: colors.bg.darkest, borderColor: colors.border.emphasis },
  chipLight: { backgroundColor: colors.text.primary, borderColor: colors.text.primary },
  chipText: { fontSize: 13, fontWeight: '700' },
  fullButton: { marginTop: spacing.md, backgroundColor: colors.bg.elevated, borderRadius: radius.md, paddingVertical: spacing.sm + 4, alignItems: 'center' },
  fullButtonText: { color: colors.text.secondary, fontSize: 15, fontWeight: '600' },
  footnote: { color: colors.text.subtle, fontSize: 12, marginTop: spacing.sm },
});
