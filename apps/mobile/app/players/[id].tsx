import { useCallback, useEffect, useLayoutEffect, useState } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, ActivityIndicator, RefreshControl } from 'react-native';
import { useLocalSearchParams, useNavigation, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import type { PlayerSeasonLineDto, UserBadgeDto } from '@bhmhockey/shared';
import { userService } from '@bhmhockey/api-client';
import { colors, spacing, radius } from '../../theme';
import { EmptyState, BadgeIconsRow } from '../../components';
import { SafeBoundary } from '../../components/game-results/SafeBoundary';
import { useGameResultStore, statsKey } from '../../stores/gameResultStore';
import { shortDate, playerGameRows } from '../../utils/gameResults';

/**
 * A player's stats (comp 04): season / career totals and the game log of games
 * with published stats. Reached from a player sheet, the leaderboard, or the
 * Profile tab's "My stats". Every state is handled: loading, no endpoint or an
 * error ("Stats aren't available right now"), no stats yet (comp 07 B), and a
 * player who hid their stats (comp 07 E).
 */
function PlayerScreenInner() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const navigation = useNavigation();
  const season = String(new Date().getFullYear());
  const [range, setRange] = useState<'season' | 'career'>('season');
  const [badges, setBadges] = useState<UserBadgeDto[]>([]);
  const [refreshing, setRefreshing] = useState(false);
  const loaded = useGameResultStore((s) => (id ? s.playerStats[statsKey(id, season)] : undefined));
  const fetchPlayerStats = useGameResultStore((s) => s.fetchPlayerStats);

  const load = useCallback(async () => {
    if (!id) return;
    await fetchPlayerStats(id, season);
  }, [id, season, fetchPlayerStats]);

  useEffect(() => {
    load();
    if (!id) return;
    let live = true;
    userService.getUserBadges(id)
      .then((b) => { if (live) setBadges(Array.isArray(b) ? b : []); })
      .catch(() => { /* no badges shown */ });
    return () => { live = false; };
  }, [id, load]);

  const stats = loaded?.status === 'ready' ? loaded.data : null;
  const name = stats ? `${stats.firstName} ${stats.lastName}`.trim() : '';

  useLayoutEffect(() => {
    navigation.setOptions({ title: 'Player' });
  }, [navigation]);

  const onRefresh = async () => {
    setRefreshing(true);
    try { await load(); } finally { setRefreshing(false); }
  };

  if (!loaded || loaded.status === 'loading' && !loaded.data) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color={colors.primary.teal} />
      </View>
    );
  }

  if (!stats) {
    return (
      <View style={styles.container}>
        <View style={styles.body}>
          <EmptyState
            icon="stats-chart"
            title={loaded.status === 'error' ? 'Stats Unavailable' : 'No Stats Yet'}
            message={loaded.status === 'error'
              ? "Stats aren't available right now. Pull down to try again later."
              : 'No published game stats for this player yet.'}
            actionLabel={loaded.status === 'error' ? 'Try again' : undefined}
            onAction={loaded.status === 'error' ? load : undefined}
          />
        </View>
      </View>
    );
  }

  const line: PlayerSeasonLineDto | null = range === 'season' ? stats.seasonLine : stats.career;
  const initials = `${stats.firstName[0] ?? ''}${stats.lastName[0] ?? ''}`.toUpperCase();
  const rows = playerGameRows(stats);

  return (
    <ScrollView
      style={styles.container}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.primary.teal} />}
    >
      <View style={styles.header}>
        <View style={styles.avatar}><Text style={styles.avatarText} allowFontScaling={false}>{initials}</Text></View>
        <View style={{ flex: 1 }}>
          <Text style={styles.name} allowFontScaling={false}>{name}</Text>
          {badges.length > 0 && <View style={styles.badges}><BadgeIconsRow badges={badges} size={28} maxDisplay={6} ownerName={name} /></View>}
        </View>
      </View>

      {stats.statsHidden ? (
        <View style={styles.body}>
          <EmptyState
            icon="eye-off-outline"
            title="Stats Hidden"
            message={`${stats.firstName || 'This player'} has chosen not to show their stats.`}
          />
        </View>
      ) : (
        <View style={styles.body}>
          <View style={styles.toggleRow}>
            {(['season', 'career'] as const).map((r) => (
              <TouchableOpacity key={r} style={[styles.pill, range === r && styles.pillOn]} onPress={() => setRange(r)}>
                <Text style={[styles.pillText, range === r && styles.pillTextOn]} allowFontScaling={false}>
                  {r === 'season' ? stats.season || season : 'Career'}
                </Text>
              </TouchableOpacity>
            ))}
          </View>

          {line ? (
            <View style={styles.card}>
              <View style={styles.tiles}>
                {[
                  ['GP', line.gamesWithStats],
                  ['G', line.goals],
                  ['A', line.assists],
                  ['PTS', line.points],
                  ['STARS', line.stars],
                ].map(([k, v]) => (
                  <View key={String(k)} style={styles.tile}>
                    <Text style={[styles.tileValue, k === 'PTS' && styles.teal]} allowFontScaling={false}>{String(v)}</Text>
                    <Text style={styles.tileLabel} allowFontScaling={false}>{String(k)}</Text>
                  </View>
                ))}
              </View>
              {line.goalsAgainst !== null && (
                <Text style={styles.caption} allowFontScaling={false}>{`${line.goalsAgainst} goals against in net`}</Text>
              )}
              <Text style={styles.caption} allowFontScaling={false}>
                GP counts games with published stats. Stats start Nov 22, 2026.
              </Text>
              {line.gamesWithUnknownScorers > 0 && (
                <Text style={styles.caption} allowFontScaling={false}>
                  Some goals had no identified scorer: these totals are at least what they show.
                </Text>
              )}
            </View>
          ) : (
            <EmptyState
              icon="time-outline"
              title="No Stats Yet"
              message={`No game ${stats.firstName || 'this player'} played in has published stats yet. Games show here as their stats are published.`}
            />
          )}

          {range === 'season' && rows.length > 0 && (
            <View style={styles.card}>
              <Text style={styles.sectionLabel} allowFontScaling={false}>Game Log</Text>
              {rows.map((row) => {
                if (row.kind === 'noStats') {
                  const n = row.entry;
                  return (
                    <TouchableOpacity key={n.eventId} style={styles.logRow} onPress={() => router.push(`/events/${n.eventId}`)}>
                      <View style={{ flex: 1 }}>
                        <Text style={styles.logDate} allowFontScaling={false}>{shortDate(n.eventDate)}</Text>
                        {n.team ? <Text style={styles.logMeta} allowFontScaling={false}>{`Team ${n.team}`}</Text> : null}
                      </View>
                      <Text style={styles.noStats} allowFontScaling={false}>No stats</Text>
                      <Ionicons name="chevron-forward" size={18} color={colors.text.muted} />
                    </TouchableOpacity>
                  );
                }
                const g = row.entry;
                const parts = [g.goals ? `${g.goals} G` : null, g.assists ? `${g.assists} A` : null].filter(Boolean).join(' · ');
                const est = [g.estimates?.iceTime?.display, g.estimates?.distanceSkated?.display].filter(Boolean).join(' · ');
                return (
                  <TouchableOpacity key={g.eventId} style={styles.logRow} onPress={() => router.push(`/events/${g.eventId}`)}>
                    <View style={{ flex: 1 }}>
                      <Text style={styles.logDate} allowFontScaling={false}>{shortDate(g.eventDate)}</Text>
                      <Text style={styles.logMeta} allowFontScaling={false}>
                        {g.opponentLabel ? `vs ${g.opponentLabel} · ` : ''}Team {g.team} ·{' '}
                        <Text style={g.result === 'W' ? styles.win : g.result === 'L' ? styles.loss : undefined}>
                          {`${g.result} ${g.teamScore}–${g.opponentScore}`}
                        </Text>
                      </Text>
                      {est ? <Text style={styles.logEst} allowFontScaling={false}>{`${est} *`}</Text> : null}
                    </View>
                    <Text style={styles.logLine} allowFontScaling={false}>
                      {g.star ? <Text style={styles.star}>★ </Text> : null}
                      {parts}
                    </Text>
                    <Ionicons name="chevron-forward" size={18} color={colors.text.muted} />
                  </TouchableOpacity>
                );
              })}
            </View>
          )}
          {range === 'season' && stats.gameLog.some((g) => g.estimates) && (
            <Text style={styles.caption} allowFontScaling={false}>
              * Estimates from video tracking, not measurements. Never added up or ranked.
            </Text>
          )}
        </View>
      )}
    </ScrollView>
  );
}

export default function PlayerScreen() {
  return (
    <SafeBoundary
      name="player screen"
      fallback={<View style={styles.container}><View style={styles.body}><EmptyState icon="stats-chart" title="Stats Unavailable" message="This player's stats could not be shown." /></View></View>}
    >
      <PlayerScreenInner />
    </SafeBoundary>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg.darkest },
  centered: { flex: 1, justifyContent: 'center', alignItems: 'center', backgroundColor: colors.bg.darkest },
  body: { padding: spacing.md },
  header: {
    flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg,
    backgroundColor: colors.bg.dark, borderBottomWidth: 1, borderBottomColor: colors.border.default,
  },
  avatar: { width: 56, height: 56, borderRadius: 28, backgroundColor: colors.bg.elevated, alignItems: 'center', justifyContent: 'center' },
  avatarText: { color: colors.text.muted, fontSize: 20, fontWeight: '700' },
  name: { color: colors.text.primary, fontSize: 24, fontWeight: '700' },
  badges: { marginTop: spacing.sm },
  toggleRow: { flexDirection: 'row', gap: spacing.sm, marginBottom: spacing.md },
  pill: { borderWidth: 1, borderColor: colors.border.muted, borderRadius: radius.round, paddingHorizontal: spacing.md, paddingVertical: spacing.xs + 2 },
  pillOn: { backgroundColor: colors.primary.teal, borderColor: colors.primary.teal },
  pillText: { color: colors.text.secondary, fontSize: 14, fontWeight: '600' },
  pillTextOn: { color: colors.bg.darkest },
  card: { backgroundColor: colors.bg.dark, borderRadius: radius.lg, borderWidth: 1, borderColor: colors.border.default, padding: spacing.md, marginBottom: spacing.md },
  tiles: { flexDirection: 'row', justifyContent: 'space-between' },
  tile: { flex: 1, alignItems: 'center' },
  tileValue: { fontSize: 24, fontWeight: '700', color: colors.text.primary },
  tileLabel: { fontSize: 11, color: colors.text.muted, marginTop: 2 },
  teal: { color: colors.primary.teal },
  caption: { color: colors.text.subtle, fontSize: 11, textAlign: 'center', marginTop: spacing.sm },
  sectionLabel: { fontSize: 13, fontWeight: '600', color: colors.text.muted, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: spacing.sm },
  logRow: { flexDirection: 'row', alignItems: 'center', paddingVertical: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border.default, gap: spacing.sm },
  logDate: { color: colors.text.primary, fontSize: 16, fontWeight: '600' },
  logMeta: { color: colors.text.secondary, fontSize: 13, marginTop: 2 },
  logEst: { color: colors.text.muted, fontSize: 12, fontStyle: 'italic', marginTop: 2 },
  logLine: { color: colors.text.primary, fontSize: 14, fontWeight: '700' },
  noStats: { color: colors.text.muted, fontSize: 14 },
  star: { color: colors.status.warning },
  win: { color: colors.primary.green, fontWeight: '700' },
  loss: { color: colors.status.error, fontWeight: '700' },
});
