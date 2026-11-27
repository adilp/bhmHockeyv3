import { useCallback, useEffect, useState } from 'react';
import { View, Text, StyleSheet, ScrollView, ActivityIndicator, RefreshControl } from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import type { LeaderboardStat } from '@bhmhockey/shared';
import { colors, spacing } from '../../../theme';
import { EmptyState } from '../../../components';
import { SafeBoundary } from '../../../components/game-results/SafeBoundary';
import { LeaderboardTable, StatPicker } from '../../../components/game-results/LeaderboardTable';
import { useGameResultStore, boardKey } from '../../../stores/gameResultStore';
import { useAuthStore } from '../../../stores/authStore';

/** The organization's full leaderboard (comp 06). */
function LeaderboardInner() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const userId = useAuthStore((s) => s.user?.id);
  const season = String(new Date().getFullYear());
  const [stat, setStat] = useState<LeaderboardStat>('points');
  const [refreshing, setRefreshing] = useState(false);
  const board = useGameResultStore((s) => (id ? s.leaderboards[boardKey(id, stat, season)] : undefined));
  const fetchLeaderboard = useGameResultStore((s) => s.fetchLeaderboard);

  const load = useCallback(async () => {
    if (id) await fetchLeaderboard(id, stat, season);
  }, [id, stat, season, fetchLeaderboard]);

  useEffect(() => { load(); }, [load]);

  const data = board?.status === 'ready' ? board.data : null;

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.content}
      refreshControl={<RefreshControl refreshing={refreshing} tintColor={colors.primary.teal}
        onRefresh={async () => { setRefreshing(true); try { await load(); } finally { setRefreshing(false); } }} />}
    >
      <View style={styles.titleRow}>
        <Text style={styles.title} allowFontScaling={false}>{`Leaders · ${season}`}</Text>
        {data && <Text style={styles.muted} allowFontScaling={false}>{`${data.gamesWithStats} game${data.gamesWithStats === 1 ? '' : 's'} with stats`}</Text>}
      </View>
      <StatPicker stat={stat} onChange={setStat} />
      {!board || (board.status === 'loading' && !board.data) ? (
        <ActivityIndicator color={colors.primary.teal} style={{ marginTop: spacing.lg }} />
      ) : data && data.rows.length > 0 ? (
        <LeaderboardTable rows={data.rows} stat={stat} currentUserId={userId} onOpenPlayer={(u) => router.push(`/players/${u}`)} />
      ) : (
        <EmptyState
          icon="podium-outline"
          title={board.status === 'error' ? 'Leaders Unavailable' : 'No Leaders Yet'}
          message={board.status === 'error' ? "The leaderboard isn't available right now." : 'Leaders appear once games have published stats.'}
        />
      )}
      <Text style={styles.footnote} allowFontScaling={false}>Players not in the app are not ranked.</Text>
    </ScrollView>
  );
}

export default function LeaderboardScreen() {
  return (
    <SafeBoundary name="leaderboard" fallback={<View style={styles.container}><View style={styles.content}><EmptyState icon="podium-outline" title="Leaders Unavailable" message="The leaderboard could not be shown." /></View></View>}>
      <LeaderboardInner />
    </SafeBoundary>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg.darkest },
  content: { padding: spacing.md, paddingBottom: spacing.xxl },
  titleRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: spacing.md },
  title: { fontSize: 13, fontWeight: '600', color: colors.text.muted, textTransform: 'uppercase', letterSpacing: 0.5 },
  muted: { color: colors.text.muted, fontSize: 13 },
  footnote: { color: colors.text.subtle, fontSize: 12, marginTop: spacing.md },
});
