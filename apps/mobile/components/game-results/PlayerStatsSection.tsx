import React, { useEffect } from 'react';
import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { colors, spacing, radius } from '../../theme';
import { Badge } from '../Badge';
import { useGameResultStore, statsKey } from '../../stores/gameResultStore';
import { myLine, statLine, ESTIMATE_KEYS } from '../../utils/gameResults';
import { SafeBoundary } from './SafeBoundary';

interface Props {
  userId: string;
  /** The game the sheet was opened from: shows "This game" when it has a published result. */
  eventId?: string;
  /** Close the sheet, then open the player screen. */
  onViewFullStats?: () => void;
}

const ESTIMATE_LABELS: Record<(typeof ESTIMATE_KEYS)[number], string> = {
  iceTime: 'on ice',
  distanceSkated: 'skated',
};

/**
 * "This game" and the season line inside a player sheet (comp 03). Renders
 * NOTHING until there is something to show: no endpoint, a 404, an error, a
 * player with no stats or one who hid them all leave the sheet exactly as it
 * was before this feature.
 */
function PlayerStatsSectionInner({ userId, eventId, onViewFullStats }: Props) {
  const season = String(new Date().getFullYear());
  const stats = useGameResultStore((s) => s.playerStats[statsKey(userId, season)]);
  const result = useGameResultStore((s) => (eventId ? s.results[eventId] : undefined));
  const fetchPlayerStats = useGameResultStore((s) => s.fetchPlayerStats);

  useEffect(() => {
    if (userId) fetchPlayerStats(userId, season);
  }, [userId, season, fetchPlayerStats]);

  const game = result?.status === 'ready' && result.data ? myLine(result.data, userId) : null;
  const line = stats?.status === 'ready' && stats.data && !stats.data.statsHidden ? stats.data.seasonLine : null;
  if (!game && !line) return null;

  const estimates = game?.estimates
    ? ESTIMATE_KEYS.filter((k) => game.estimates?.[k]).map((k) => `${game.estimates![k]!.display} ${ESTIMATE_LABELS[k]}*`)
    : [];

  return (
    <View style={styles.container}>
      {game && (
        <View style={styles.block}>
          <Text style={styles.label} allowFontScaling={false}>This Game</Text>
          <View style={styles.badgeRow}>
            {game.goals ? <Badge variant="teal">{`${game.goals} G`}</Badge> : null}
            {game.assists ? <Badge variant="teal">{`${game.assists} A`}</Badge> : null}
            {game.star ? <Badge variant="warning">{`★ ${['1st', '2nd', '3rd'][game.star - 1]} star`}</Badge> : null}
            {game.position === 'Goalie' && game.goalsAgainst !== null ? (
              <Badge>{`${game.goalsAgainst} GA`}</Badge>
            ) : null}
            {!statLine(game) && !game.star && game.position !== 'Goalie' ? (
              <Text style={styles.muted} allowFontScaling={false}>Played</Text>
            ) : null}
          </View>
          {estimates.length > 0 && (
            <>
              <Text style={styles.estimates} allowFontScaling={false}>{estimates.join('    ')}</Text>
              <Text style={styles.footnote} allowFontScaling={false}>
                * Estimates from video tracking, not measurements
              </Text>
            </>
          )}
        </View>
      )}
      {line && (
        <View style={styles.block}>
          <Text style={styles.label} allowFontScaling={false}>{`${season} Season`}</Text>
          <View style={styles.tiles}>
            {[
              ['GP', line.gamesWithStats],
              ['G', line.goals],
              ['A', line.assists],
              ['PTS', line.points],
              ['★', line.stars],
            ].map(([k, v]) => (
              <View key={String(k)} style={styles.tile}>
                <Text style={[styles.tileValue, k === 'PTS' && styles.teal]} allowFontScaling={false}>{String(v)}</Text>
                <Text style={styles.tileLabel} allowFontScaling={false}>{String(k)}</Text>
              </View>
            ))}
          </View>
          {line.gamesWithUnknownScorers > 0 && (
            <Text style={styles.footnote} allowFontScaling={false}>
              Some goals had no identified scorer: totals are at least these.
            </Text>
          )}
        </View>
      )}
      {onViewFullStats && (
        <TouchableOpacity style={styles.button} onPress={onViewFullStats} activeOpacity={0.8}>
          <Text style={styles.buttonText} allowFontScaling={false}>View full stats</Text>
        </TouchableOpacity>
      )}
    </View>
  );
}

export function PlayerStatsSection(props: Props) {
  return (
    <SafeBoundary name="player stats">
      <PlayerStatsSectionInner {...props} />
    </SafeBoundary>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginBottom: spacing.md,
  },
  block: { marginBottom: spacing.md },
  label: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.sm,
  },
  badgeRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, alignItems: 'center' },
  muted: { color: colors.text.muted, fontSize: 14 },
  estimates: { color: colors.text.secondary, fontStyle: 'italic', fontSize: 14, marginTop: spacing.sm },
  footnote: { color: colors.text.subtle, fontSize: 11, marginTop: spacing.xs },
  tiles: { flexDirection: 'row', justifyContent: 'space-between' },
  tile: { flex: 1, alignItems: 'center' },
  tileValue: { fontSize: 20, fontWeight: '700', color: colors.text.primary },
  tileLabel: { fontSize: 11, color: colors.text.muted, marginTop: 2 },
  teal: { color: colors.primary.teal },
  button: {
    backgroundColor: colors.primary.teal,
    borderRadius: radius.md,
    paddingVertical: spacing.sm + 4,
    alignItems: 'center',
  },
  buttonText: { color: colors.bg.darkest, fontSize: 15, fontWeight: '600' },
});
