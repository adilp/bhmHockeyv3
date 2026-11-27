import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableWithoutFeedback,
  ScrollView,
  ActivityIndicator,
} from 'react-native';
import type { GamePlayerLineDto, UserBadgeDto } from '@bhmhockey/shared';
import { userService } from '@bhmhockey/api-client';
import { colors, spacing, radius } from '../../theme';
import { TrophyCase } from '../badges/TrophyCase';
import { PlayerStatsSection } from './PlayerStatsSection';
import { SafeBoundary } from './SafeBoundary';

interface Props {
  /** The tapped line; only lines with a userId (app accounts) are tappable. */
  line: GamePlayerLineDto | null;
  eventId: string;
  onClose: () => void;
  onViewFullStats: (userId: string) => void;
}

/**
 * The player sheet opened from the Results tab (comp 03): the same layout as the
 * roster's PlayerDetailModal - name, position and team, this game, the season,
 * "View full stats", and the trophy case as the FULL list (the owner,
 * 2026-11-27: expanded on this screen, not a collapsed row).
 */
export function GamePlayerSheet({ line, eventId, onClose, onViewFullStats }: Props) {
  const userId = line?.player.userId ?? null;
  const [badges, setBadges] = useState<UserBadgeDto[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let live = true;
    setBadges([]);
    if (!userId) return;
    setLoading(true);
    userService.getUserBadges(userId)
      .then((b) => { if (live) setBadges(Array.isArray(b) ? b : []); })
      .catch(() => { /* the trophy case simply stays empty */ })
      .finally(() => { if (live) setLoading(false); });
    return () => { live = false; };
  }, [userId]);

  if (!line || !userId) return null;

  return (
    <Modal visible transparent animationType="fade" onRequestClose={onClose}>
      <TouchableWithoutFeedback onPress={onClose}>
        <View style={styles.overlay}>
          <TouchableWithoutFeedback>
            <View style={styles.modal}>
              <SafeBoundary name="player sheet">
                <ScrollView contentContainerStyle={styles.content} showsVerticalScrollIndicator={false} bounces={false}>
                  <Text style={styles.name} allowFontScaling={false}>{line.player.displayName}</Text>
                  <Text style={styles.meta} allowFontScaling={false}>
                    {line.position} • Team {line.team}
                  </Text>

                  <PlayerStatsSection
                    userId={userId}
                    eventId={eventId}
                    onViewFullStats={() => { onClose(); onViewFullStats(userId); }}
                  />

                  <Text style={styles.sectionLabel} allowFontScaling={false}>Trophy Case</Text>
                  {loading ? (
                    <ActivityIndicator size="small" color={colors.primary.teal} />
                  ) : (
                    <TrophyCase badges={badges} ownerName={line.player.displayName} />
                  )}
                </ScrollView>
              </SafeBoundary>
            </View>
          </TouchableWithoutFeedback>
        </View>
      </TouchableWithoutFeedback>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: colors.overlay,
    justifyContent: 'center',
    padding: spacing.lg,
  },
  modal: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.xl,
    borderWidth: 1,
    borderColor: colors.border.muted,
    maxHeight: '85%',
  },
  content: { padding: spacing.lg },
  name: { fontSize: 24, fontWeight: '700', color: colors.text.primary },
  meta: { fontSize: 15, color: colors.text.muted, marginTop: spacing.xs, marginBottom: spacing.md },
  sectionLabel: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    marginBottom: spacing.sm,
  },
});
