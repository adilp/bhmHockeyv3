import React from 'react';
import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import type { LeaderboardRowDto, LeaderboardStat } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../theme';
import { LEADERBOARD_STATS, rankText } from '../../utils/gameResults';

const STAT_LABELS: Record<LeaderboardStat, string> = {
  points: 'Points', goals: 'Goals', assists: 'Assists', stars: 'Stars',
};

export function StatPicker({ stat, onChange }: { stat: LeaderboardStat; onChange: (s: LeaderboardStat) => void }) {
  return (
    <View style={styles.picker}>
      {LEADERBOARD_STATS.map((s) => (
        <TouchableOpacity key={s} style={[styles.pill, stat === s && styles.pillOn]} onPress={() => onChange(s)}>
          <Text style={[styles.pillText, stat === s && styles.pillTextOn]} allowFontScaling={false}>{STAT_LABELS[s]}</Text>
        </TouchableOpacity>
      ))}
    </View>
  );
}

interface Props {
  rows: LeaderboardRowDto[];
  stat: LeaderboardStat;
  currentUserId?: string | null;
  onOpenPlayer: (userId: string) => void;
}

/** Ranked rows in the tournament standings' layout: rank, name, small stat columns, teal emphasis, the viewer highlighted. */
export function LeaderboardTable({ rows, stat, currentUserId, onOpenPlayer }: Props) {
  const lastCol: { key: keyof LeaderboardRowDto; label: string } =
    stat === 'stars' ? { key: 'stars', label: '★' } : { key: 'points', label: 'PTS' };
  return (
    <View>
      <View style={styles.headRow}>
        <Text style={[styles.rank, styles.head]} allowFontScaling={false}>#</Text>
        <Text style={[styles.player, styles.head]} allowFontScaling={false}>Player</Text>
        <Text style={[styles.col, styles.head]} allowFontScaling={false}>G</Text>
        <Text style={[styles.col, styles.head]} allowFontScaling={false}>A</Text>
        <Text style={[styles.col, styles.head]} allowFontScaling={false}>{lastCol.label}</Text>
      </View>
      {rows.map((r, i) => {
        const me = !!currentUserId && r.userId.toLowerCase() === currentUserId.toLowerCase();
        return (
          <TouchableOpacity key={r.userId} style={[styles.row, me && styles.rowMe]} onPress={() => onOpenPlayer(r.userId)}>
            <Text style={styles.rank} allowFontScaling={false}>{rankText(rows, i)}</Text>
            <Text style={styles.player} numberOfLines={1} allowFontScaling={false}>{`${r.firstName} ${r.lastName}`.trim()}</Text>
            <Text style={[styles.col, stat === 'goals' && styles.emph]} allowFontScaling={false}>{r.goals}</Text>
            <Text style={[styles.col, stat === 'assists' && styles.emph]} allowFontScaling={false}>{r.assists}</Text>
            <Text style={[styles.col, styles.emph]} allowFontScaling={false}>{String(r[lastCol.key])}</Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  picker: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, marginBottom: spacing.md },
  pill: { borderWidth: 1, borderColor: colors.border.muted, borderRadius: radius.round, paddingHorizontal: spacing.md, paddingVertical: spacing.xs + 2 },
  pillOn: { backgroundColor: colors.primary.teal, borderColor: colors.primary.teal },
  pillText: { color: colors.text.secondary, fontSize: 14, fontWeight: '600' },
  pillTextOn: { color: colors.bg.darkest },
  headRow: { flexDirection: 'row', paddingHorizontal: spacing.sm, paddingBottom: spacing.xs, borderBottomWidth: 1, borderBottomColor: colors.border.default },
  head: { color: colors.text.muted, fontSize: 11, fontWeight: '600', textTransform: 'uppercase' },
  row: {
    flexDirection: 'row', alignItems: 'center', paddingVertical: spacing.sm + 2, paddingHorizontal: spacing.sm,
    borderWidth: 1, borderColor: colors.border.default, borderRadius: radius.md, marginTop: spacing.xs,
    backgroundColor: colors.bg.dark,
  },
  rowMe: { borderColor: colors.primary.teal, backgroundColor: colors.subtle.teal },
  rank: { width: 36, color: colors.text.secondary, fontSize: 14, fontWeight: '600' },
  player: { flex: 1, color: colors.primary.teal, fontSize: 15, fontWeight: '500' },
  col: { width: 38, textAlign: 'center', color: colors.text.secondary, fontSize: 14 },
  emph: { color: colors.primary.teal, fontWeight: '700' },
});
