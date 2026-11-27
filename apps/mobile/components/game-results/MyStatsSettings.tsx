import React, { useEffect } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, Switch, Alert } from 'react-native';
import { router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing, radius } from '../../theme';
import { useGameResultStore } from '../../stores/gameResultStore';
import { SafeBoundary } from './SafeBoundary';

/**
 * The Profile tab's "Game Stats" block: a row to my player screen and the
 * "Show my stats to other players" switch. It appears only once the server
 * answers GET /users/me/stats-visibility - on a server without game results it
 * is not there at all.
 */
function MyStatsSettingsInner({ userId }: { userId: string }) {
  const hidden = useGameResultStore((s) => s.statsHidden);
  const fetchStatsHidden = useGameResultStore((s) => s.fetchStatsHidden);
  const setStatsHidden = useGameResultStore((s) => s.setStatsHidden);

  useEffect(() => { fetchStatsHidden(); }, [fetchStatsHidden]);

  if (hidden.status !== 'ready' || hidden.data === null) return null;

  const onToggle = async (show: boolean) => {
    const ok = await setStatsHidden(!show);
    if (!ok) Alert.alert('Not saved', 'Your stats setting could not be changed. Please try again.');
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title} allowFontScaling={false}>Game Stats</Text>
      <TouchableOpacity style={styles.row} onPress={() => router.push(`/players/${userId}`)}>
        <Text style={styles.rowText} allowFontScaling={false}>My stats</Text>
        <Ionicons name="chevron-forward" size={18} color={colors.text.muted} />
      </TouchableOpacity>
      <View style={styles.row}>
        <View style={{ flex: 1 }}>
          <Text style={styles.rowText} allowFontScaling={false}>Show my stats to other players</Text>
          <Text style={styles.hint} allowFontScaling={false}>
            Off hides your totals and your leaderboard spot. Your name stays on the goals you scored.
          </Text>
        </View>
        <Switch
          value={!hidden.data}
          onValueChange={onToggle}
          trackColor={{ false: colors.bg.active, true: colors.primary.teal }}
        />
      </View>
    </View>
  );
}

export function MyStatsSettings(props: { userId: string }) {
  return (
    <SafeBoundary name="my stats settings">
      <MyStatsSettingsInner {...props} />
    </SafeBoundary>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
    padding: spacing.md,
    marginBottom: spacing.lg,
  },
  title: { fontSize: 13, fontWeight: '600', color: colors.text.muted, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: spacing.sm },
  row: { flexDirection: 'row', alignItems: 'center', paddingVertical: spacing.sm, gap: spacing.md },
  rowText: { flex: 1, color: colors.text.primary, fontSize: 15 },
  hint: { color: colors.text.muted, fontSize: 12, marginTop: 2 },
});
