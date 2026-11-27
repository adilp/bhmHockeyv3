import React, { useState } from 'react';
import { View, Text, StyleSheet, ScrollView, TouchableOpacity, Linking, Modal, TouchableWithoutFeedback } from 'react-native';
import { router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import type { GameGoalDto, GamePlayerLineDto, GamePlayerRefDto, GameResultDto, GameTeam } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../theme';
import { EmptyState } from '../EmptyState';
import {
  assistText,
  clockText,
  countText,
  goalsByPeriod,
  ordinalPeriod,
  scorerGoalNumber,
  shortDate,
  sideLabel,
  statLine,
  youPlayedText,
} from '../../utils/gameResults';
import { GamePlayerSheet } from './GamePlayerSheet';
import { SafeBoundary } from './SafeBoundary';

interface Props {
  eventId: string;
  eventDate: string;
  result: GameResultDto | null;
  currentUserId?: string | null;
}

/** "Watch" a goal: the video at that second, when the video has a link. */
function openVideo(url: string | null, offsetSec: number | null) {
  if (!url) return;
  const sep = url.includes('?') ? '&' : '?';
  Linking.openURL(offsetSec !== null ? `${url}${sep}t=${offsetSec}` : url).catch(() => { /* nothing to open */ });
}

function SideChip({ label, team }: { label: string; team: GameTeam }) {
  const dark = team === 'Black';
  return (
    <View style={[styles.chip, dark ? styles.chipBlack : styles.chipWhite]}>
      <Text style={[styles.chipText, dark ? styles.chipTextBlack : styles.chipTextWhite]} allowFontScaling={false} numberOfLines={1}>
        {label}
      </Text>
    </View>
  );
}

function Name({ who, onPress, style }: { who: GamePlayerRefDto; onPress: (r: GamePlayerRefDto) => void; style?: object }) {
  // A person with no app account is plain text, never tappable, never labelled "not in app".
  if (!who.userId) return <Text style={[styles.name, style]} allowFontScaling={false}>{who.displayName}</Text>;
  return (
    <Text style={[styles.name, styles.link, style]} onPress={() => onPress(who)} allowFontScaling={false}>
      {who.displayName}
    </Text>
  );
}

/**
 * The Results segment of a finished event (comp 02). Shown only when a result
 * is published; with none it is the "No Results Yet" state (comp 07 A). Every
 * section is optional: a game with no stars, no video, no goals or no box
 * score still renders what it has.
 */
function EventResultsTabInner({ eventId, eventDate, result, currentUserId }: Props) {
  const [sheetLine, setSheetLine] = useState<GamePlayerLineDto | null>(null);
  const [showIceTime, setShowIceTime] = useState(false);
  const [showEstimateInfo, setShowEstimateInfo] = useState(false);

  if (!result) {
    return (
      <View style={styles.container}>
        <EmptyState
          icon="bar-chart"
          title="No Results Yet"
          message="Stats appear here after the game's video has been reviewed. The roster is on the Roster tab."
        />
      </View>
    );
  }

  const allLines = [...result.whiteLines, ...result.blackLines, ...result.stars];
  const openRef = (r: GamePlayerRefDto) => {
    const l = allLines.find((x) => x.player.userId && x.player.userId === r.userId);
    if (l) setSheetLine(l);
  };
  const you = youPlayedText(result, currentUserId);
  const hasEstimates = allLines.some((l) => l.estimates);
  // The winning side first, as the comp (Home 5 - 4 Visitors)
  const order: GameTeam[] = result.winner === 'Black' ? ['Black', 'White'] : ['White', 'Black'];
  const score = (t: GameTeam) => (t === 'Black' ? result.black.score : result.white.score);
  const lines = (t: GameTeam) => (t === 'Black' ? result.blackLines : result.whiteLines);

  const renderGoal = (g: GameGoalDto) => {
    const n = scorerGoalNumber(result.goals, g);
    const assists = assistText(g);
    return (
      <View key={g.sequence} style={styles.goalRow}>
        <Text style={styles.goalClock} allowFontScaling={false}>{clockText(g.periodElapsedSec)}</Text>
        <SideChip label={sideLabel(result, g.team)} team={g.team} />
        <View style={styles.goalBody}>
          <Text allowFontScaling={false} numberOfLines={1}>
            {g.scorer ? <Name who={g.scorer} onPress={openRef} /> : <Text style={styles.muted}>Scorer not identified</Text>}
            {n ? <Text style={styles.muted}>{` (${n})`}</Text> : null}
          </Text>
          {assists ? (
            <Text style={styles.assist} allowFontScaling={false} numberOfLines={2}>
              {assists === 'Unassisted' ? assists : (
                <>
                  {g.assists.length > 1 ? 'Assists: ' : 'Assist: '}
                  {g.assists.map((a, i) => (
                    <Text key={`${a.displayName}-${i}`}>
                      {i > 0 ? ', ' : ''}
                      <Name who={a} onPress={openRef} style={styles.assistName} />
                    </Text>
                  ))}
                </>
              )}
            </Text>
          ) : null}
        </View>
        <View style={styles.goalRight}>
          <Text style={styles.goalScore} allowFontScaling={false}>
            {order[0] === 'White' ? `${g.whiteScoreAfter}–${g.blackScoreAfter}` : `${g.blackScoreAfter}–${g.whiteScoreAfter}`}
          </Text>
          {result.videoUrl && g.videoOffsetSec !== null ? (
            <TouchableOpacity onPress={() => openVideo(result.videoUrl, g.videoOffsetSec)}>
              <Text style={styles.watch} allowFontScaling={false}>▸ Watch</Text>
            </TouchableOpacity>
          ) : null}
        </View>
      </View>
    );
  };

  const renderBox = (t: GameTeam) => {
    const rows = lines(t);
    if (rows.length === 0) return null;
    return (
      <View key={t} style={styles.card}>
        <View style={styles.boxHeader}>
          <SideChip label={sideLabel(result, t)} team={t} />
          <Text style={styles.boxTitle} allowFontScaling={false}>{sideLabel(result, t)}</Text>
        </View>
        <View style={styles.boxRow}>
          <Text style={[styles.colNum, styles.colHead]} allowFontScaling={false}>#</Text>
          <Text style={[styles.colName, styles.colHead]} allowFontScaling={false}>Player</Text>
          {showIceTime ? (
            <>
              <Text style={[styles.colEst, styles.colHead]} allowFontScaling={false}>Ice time*</Text>
              <Text style={[styles.colEst, styles.colHead]} allowFontScaling={false}>Skated*</Text>
            </>
          ) : (
            <>
              <Text style={[styles.colStat, styles.colHead]} allowFontScaling={false}>G</Text>
              <Text style={[styles.colStat, styles.colHead]} allowFontScaling={false}>A</Text>
              <Text style={[styles.colStat, styles.colHead]} allowFontScaling={false}>PTS</Text>
            </>
          )}
        </View>
        {rows.map((l, i) => (
          <View key={`${l.player.displayName}-${i}`} style={styles.boxRow}>
            <Text style={styles.colNum} allowFontScaling={false}>{l.player.jersey ?? ''}</Text>
            <Text style={styles.colName} numberOfLines={1} allowFontScaling={false}>
              <Name who={l.player} onPress={openRef} />
              {l.star ? <Text style={styles.star}> ★</Text> : null}
              {l.position === 'Goalie' ? <Text style={styles.muted}> (G)</Text> : null}
            </Text>
            {showIceTime ? (
              <>
                <Text style={[styles.colEst, styles.estimate]} allowFontScaling={false}>{l.estimates?.iceTime?.display ?? ''}</Text>
                <Text style={[styles.colEst, styles.estimate]} allowFontScaling={false}>{l.estimates?.distanceSkated?.display ?? ''}</Text>
              </>
            ) : l.position === 'Goalie' ? (
              <Text style={styles.goalieAgainst} allowFontScaling={false}>
                {l.goalsAgainst !== null ? `${l.goalsAgainst} goals against` : ''}
              </Text>
            ) : (
              <>
                <Text style={styles.colStat} allowFontScaling={false}>{countText(l.goals)}</Text>
                <Text style={styles.colStat} allowFontScaling={false}>{countText(l.assists)}</Text>
                <Text style={[styles.colStat, styles.teal]} allowFontScaling={false}>{countText(l.points)}</Text>
              </>
            )}
          </View>
        ))}
      </View>
    );
  };

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      {/* Final */}
      <View style={[styles.card, styles.center]}>
        <Text style={styles.finalLabel} allowFontScaling={false}>{`FINAL · ${shortDate(eventDate).toUpperCase()}`}</Text>
        <View style={styles.scoreRow}>
          {order.map((t, i) => (
            <React.Fragment key={t}>
              {i === 1 && <Text style={styles.dash}>–</Text>}
              <View style={styles.scoreSide}>
                <SideChip label={sideLabel(result, t)} team={t} />
                <Text style={[styles.bigScore, result.winner && result.winner !== t && styles.loserScore]} allowFontScaling={false}>
                  {score(t)}
                </Text>
              </View>
            </React.Fragment>
          ))}
        </View>
        {!!you && <Text style={styles.youPlayed} allowFontScaling={false}>{you}</Text>}
      </View>

      {/* Video */}
      {!!result.videoUrl && (
        <View style={styles.card}>
          <TouchableOpacity style={styles.videoRow} onPress={() => openVideo(result.videoUrl, null)}>
            <View style={styles.play}><Ionicons name="play" size={20} color={colors.bg.darkest} /></View>
            <View style={{ flex: 1 }}>
              <Text style={styles.videoTitle} allowFontScaling={false}>Watch the full game</Text>
              <Text style={styles.muted} allowFontScaling={false}>Every goal is a chapter</Text>
            </View>
            <Ionicons name="chevron-forward" size={20} color={colors.text.muted} />
          </TouchableOpacity>
          {result.videoChapters.some((c) => c.offsetSec !== null) && (
            <View style={styles.chapters}>
              {result.videoChapters.filter((c) => c.offsetSec !== null).map((c) => (
                <TouchableOpacity key={c.label} style={styles.chapter} onPress={() => openVideo(result.videoUrl, c.offsetSec)}>
                  <Text style={styles.chapterText} allowFontScaling={false}>{`▸ ${c.label}`}</Text>
                </TouchableOpacity>
              ))}
            </View>
          )}
        </View>
      )}

      {/* Three stars */}
      {result.stars.length > 0 && (
        <View style={styles.card}>
          <Text style={styles.sectionLabel} allowFontScaling={false}>Three Stars</Text>
          {result.stars.map((l) => (
            <View key={`star-${l.star}`} style={styles.starRow}>
              <View style={styles.starRank}>
                <Text style={styles.starOrdinal} allowFontScaling={false}>{['1st', '2nd', '3rd'][(l.star ?? 1) - 1]}</Text>
                <Text style={styles.star} allowFontScaling={false}>{'★'.repeat(4 - (l.star ?? 3))}</Text>
              </View>
              <View style={{ flex: 1 }}>
                <Name who={l.player} onPress={openRef} style={styles.starName} />
                <Text style={styles.muted} allowFontScaling={false}>
                  {[l.player.jersey ? `#${l.player.jersey}` : null, sideLabel(result, l.team)].filter(Boolean).join(' · ')}
                </Text>
              </View>
              <Text style={styles.starLine} allowFontScaling={false}>{statLine(l)}</Text>
            </View>
          ))}
        </View>
      )}

      {/* Scoring */}
      {result.goals.length > 0 && (
        <View style={styles.card}>
          <View style={styles.rowBetween}>
            <Text style={styles.sectionLabel} allowFontScaling={false}>Scoring</Text>
            <Text style={styles.muted} allowFontScaling={false}>{`${sideLabel(result, order[0])}–${sideLabel(result, order[1])}`}</Text>
          </View>
          {goalsByPeriod(result.goals).map(({ period, goals }) => (
            <View key={String(period)}>
              <Text style={styles.periodLabel} allowFontScaling={false}>{ordinalPeriod(period).toUpperCase()}</Text>
              {goals.map(renderGoal)}
            </View>
          ))}
        </View>
      )}

      {/* Box score, facts by default, estimates behind the toggle */}
      {hasEstimates && (
        <View style={styles.toggleRow}>
          <TouchableOpacity style={[styles.pill, !showIceTime && styles.pillOn]} onPress={() => setShowIceTime(false)}>
            <Text style={[styles.pillText, !showIceTime && styles.pillTextOn]} allowFontScaling={false}>Scoring</Text>
          </TouchableOpacity>
          <TouchableOpacity style={[styles.pill, showIceTime && styles.pillOn]} onPress={() => setShowIceTime(true)}>
            <Text style={[styles.pillText, showIceTime && styles.pillTextOn]} allowFontScaling={false}>Ice time*</Text>
          </TouchableOpacity>
          {showIceTime && (
            <TouchableOpacity onPress={() => setShowEstimateInfo(true)} style={styles.infoButton}>
              <Ionicons name="information-circle-outline" size={20} color={colors.text.muted} />
            </TouchableOpacity>
          )}
        </View>
      )}
      {order.map(renderBox)}

      {/* Notes */}
      {(result.notes.length > 0 || allLines.some((l) => l.player.userId)) && (
        <Text style={styles.notes} allowFontScaling={false}>
          {result.notes.join(' · ')}
          {'\n'}Names in teal open a player. Stats by AMP Vision.
        </Text>
      )}

      <GamePlayerSheet
        line={sheetLine}
        eventId={eventId}
        onClose={() => setSheetLine(null)}
        onViewFullStats={(id) => router.push(`/players/${id}`)}
      />

      <Modal visible={showEstimateInfo} transparent animationType="fade" onRequestClose={() => setShowEstimateInfo(false)}>
        <TouchableWithoutFeedback onPress={() => setShowEstimateInfo(false)}>
          <View style={styles.sheetOverlay}>
            <View style={styles.sheet}>
              <Text style={styles.videoTitle} allowFontScaling={false}>Estimates, not measurements</Text>
              <Text style={styles.sheetText} allowFontScaling={false}>
                {result.estimateDisclaimer ??
                  'Ice time and distance are estimated from video tracking. A blank was held back by the project\'s own checks.'}
              </Text>
            </View>
          </View>
        </TouchableWithoutFeedback>
      </Modal>
    </ScrollView>
  );
}

export function EventResultsTab(props: Props) {
  return (
    <SafeBoundary
      name="results tab"
      fallback={
        <View style={styles.container}>
          <EmptyState icon="bar-chart" title="No Results Yet" message="This game's stats could not be shown." />
        </View>
      }
    >
      <EventResultsTabInner {...props} />
    </SafeBoundary>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: colors.bg.darkest, padding: spacing.md },
  content: { paddingBottom: spacing.xxl },
  card: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border.default,
    padding: spacing.md,
    marginBottom: spacing.md,
  },
  center: { alignItems: 'center' },
  rowBetween: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  finalLabel: { color: colors.text.muted, fontSize: 13, fontWeight: '600', letterSpacing: 1 },
  scoreRow: { flexDirection: 'row', alignItems: 'center', marginTop: spacing.sm },
  scoreSide: { alignItems: 'center', minWidth: 90 },
  bigScore: { fontSize: 44, fontWeight: '800', color: colors.text.primary, marginTop: spacing.xs },
  loserScore: { color: colors.text.muted },
  dash: { fontSize: 28, color: colors.text.subtle, marginHorizontal: spacing.md },
  youPlayed: {
    marginTop: spacing.sm,
    color: colors.primary.green,
    backgroundColor: colors.subtle.green,
    paddingHorizontal: spacing.sm,
    paddingVertical: 2,
    borderRadius: radius.sm,
    fontSize: 13,
    fontWeight: '600',
  },
  chip: { paddingHorizontal: 6, paddingVertical: 2, borderRadius: radius.sm, minWidth: 34, alignItems: 'center', borderWidth: 1 },
  chipBlack: { backgroundColor: colors.bg.darkest, borderColor: colors.border.emphasis },
  chipWhite: { backgroundColor: colors.text.primary, borderColor: colors.text.primary },
  chipText: { fontSize: 11, fontWeight: '700' },
  chipTextBlack: { color: colors.text.primary },
  chipTextWhite: { color: colors.bg.darkest },
  videoRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  play: { width: 40, height: 40, borderRadius: 20, backgroundColor: colors.primary.teal, alignItems: 'center', justifyContent: 'center' },
  videoTitle: { color: colors.text.primary, fontSize: 16, fontWeight: '600' },
  chapters: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm, marginTop: spacing.md },
  chapter: { borderWidth: 1, borderColor: colors.border.muted, borderRadius: radius.round, paddingHorizontal: spacing.md, paddingVertical: spacing.xs },
  chapterText: { color: colors.text.secondary, fontSize: 13 },
  sectionLabel: { fontSize: 13, fontWeight: '600', color: colors.text.muted, textTransform: 'uppercase', letterSpacing: 0.5, marginBottom: spacing.sm },
  starRow: { flexDirection: 'row', alignItems: 'center', paddingVertical: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border.default },
  starRank: { width: 52 },
  starOrdinal: { color: colors.text.primary, fontWeight: '700', fontSize: 14 },
  star: { color: colors.status.warning, fontSize: 12 },
  starName: { fontSize: 16 },
  starLine: { color: colors.text.primary, fontWeight: '700', fontSize: 15 },
  periodLabel: { color: colors.text.muted, fontSize: 12, fontWeight: '600', letterSpacing: 0.5, marginTop: spacing.sm, marginBottom: spacing.xs },
  goalRow: { flexDirection: 'row', alignItems: 'flex-start', paddingVertical: spacing.sm, borderBottomWidth: 1, borderBottomColor: colors.border.default, gap: spacing.sm },
  goalClock: { width: 40, color: colors.text.muted, fontSize: 13, marginTop: 2 },
  goalBody: { flex: 1 },
  goalRight: { alignItems: 'flex-end' },
  goalScore: { color: colors.text.primary, fontWeight: '600', fontSize: 13 },
  watch: { color: colors.primary.teal, fontSize: 12, marginTop: 2 },
  name: { color: colors.text.primary, fontSize: 15, fontWeight: '500' },
  link: { color: colors.primary.teal },
  muted: { color: colors.text.muted, fontSize: 13 },
  assist: { color: colors.text.muted, fontSize: 13, marginTop: 2 },
  assistName: { fontSize: 13 },
  toggleRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.md },
  pill: { borderWidth: 1, borderColor: colors.border.muted, borderRadius: radius.round, paddingHorizontal: spacing.md, paddingVertical: spacing.xs + 2 },
  pillOn: { backgroundColor: colors.primary.teal, borderColor: colors.primary.teal },
  pillText: { color: colors.text.secondary, fontSize: 13, fontWeight: '600' },
  pillTextOn: { color: colors.bg.darkest },
  infoButton: { padding: spacing.xs },
  boxHeader: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, marginBottom: spacing.sm },
  boxTitle: { color: colors.text.primary, fontSize: 17, fontWeight: '700' },
  boxRow: { flexDirection: 'row', alignItems: 'center', paddingVertical: 6, borderBottomWidth: 1, borderBottomColor: colors.border.default },
  colHead: { color: colors.text.muted, fontSize: 11, fontWeight: '600', textTransform: 'uppercase' },
  colNum: { width: 30, color: colors.text.muted, fontSize: 13 },
  colName: { flex: 1 },
  colStat: { width: 34, textAlign: 'center', color: colors.text.primary, fontSize: 14 },
  colEst: { width: 82, textAlign: 'right', fontSize: 12 },
  estimate: { color: colors.text.secondary, fontStyle: 'italic' },
  goalieAgainst: { color: colors.text.muted, fontSize: 12 },
  teal: { color: colors.primary.teal, fontWeight: '700' },
  notes: { color: colors.text.subtle, fontSize: 12, textAlign: 'center', marginTop: spacing.sm, lineHeight: 18 },
  sheetOverlay: { flex: 1, backgroundColor: colors.overlay, justifyContent: 'flex-end' },
  sheet: { backgroundColor: colors.bg.dark, borderTopLeftRadius: radius.xl, borderTopRightRadius: radius.xl, padding: spacing.lg },
  sheetText: { color: colors.text.secondary, fontSize: 14, lineHeight: 21, marginTop: spacing.sm },
});
