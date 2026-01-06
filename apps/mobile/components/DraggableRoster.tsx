import React, { useCallback, useState, useRef, useMemo } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Dimensions,
  Pressable,
} from 'react-native';
import { Gesture, GestureDetector } from 'react-native-gesture-handler';
import Animated, {
  useAnimatedStyle,
  useSharedValue,
  runOnJS,
  SharedValue,
} from 'react-native-reanimated';
import type { EventRegistrationDto, TeamAssignment, RosterOrderItem } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../theme';

const SCREEN_WIDTH = Dimensions.get('window').width;
const ROW_HEIGHT = 56;
const SLOT_BADGE_WIDTH = 32;
const CONTAINER_PADDING = spacing.md; // 16px on each side = 32 total
const CELL_MARGIN = spacing.xs; // margin between cells and badge

interface DraggableRosterProps {
  registrations: EventRegistrationDto[];
  showPayment: boolean;
  onPlayerPress: (registration: EventRegistrationDto) => void;
  onRosterChange: (items: RosterOrderItem[]) => void;
}

type SlotType = 'goalie' | 'skater';

interface RosterSlot {
  type: SlotType;
  index: number;
  blackPlayer: EventRegistrationDto | null;
  whitePlayer: EventRegistrationDto | null;
}

interface DragInfo {
  registration: EventRegistrationDto;
  side: 'left' | 'right';
  sourceSlotIndex: number;
  startY: number;
}

const getPaymentInfo = (status?: string): { label: string; color: string; bgColor: string } => {
  switch (status) {
    case 'Verified':
      return { label: 'Paid', color: colors.primary.green, bgColor: colors.subtle.green };
    case 'MarkedPaid':
      return { label: 'Pending', color: colors.status.warning, bgColor: colors.status.warningSubtle };
    case 'Pending':
    default:
      return { label: 'Unpaid', color: colors.status.error, bgColor: colors.status.errorSubtle };
  }
};

// Static player cell (non-draggable, just displays)
function PlayerCell({
  registration,
  showPayment,
  side,
  onPress,
  onLongPress,
}: {
  registration: EventRegistrationDto;
  showPayment: boolean;
  side: 'left' | 'right';
  onPress: () => void;
  onLongPress: () => void;
}) {
  const { user, paymentStatus } = registration;
  const fullName = `${user.firstName} ${user.lastName}`;
  const paymentInfo = getPaymentInfo(paymentStatus);

  return (
    <Pressable
      style={[
        styles.playerCell,
        side === 'left' ? styles.playerCellLeft : styles.playerCellRight,
      ]}
      onPress={onPress}
      onLongPress={onLongPress}
      delayLongPress={200}
    >
      {side === 'left' && (
        <>
          <Text style={[styles.playerName, styles.playerNameLeft]} numberOfLines={1}>
            {fullName}
          </Text>
          {showPayment && (
            <View style={[styles.paymentBadge, { backgroundColor: paymentInfo.bgColor }]}>
              <Text style={[styles.paymentBadgeText, { color: paymentInfo.color }]}>
                {paymentInfo.label}
              </Text>
            </View>
          )}
        </>
      )}
      {side === 'right' && (
        <>
          {showPayment && (
            <View style={[styles.paymentBadge, { backgroundColor: paymentInfo.bgColor }]}>
              <Text style={[styles.paymentBadgeText, { color: paymentInfo.color }]}>
                {paymentInfo.label}
              </Text>
            </View>
          )}
          <Text style={[styles.playerName, styles.playerNameRight]} numberOfLines={1}>
            {fullName}
          </Text>
        </>
      )}
    </Pressable>
  );
}

// Drag overlay - follows finger during drag
function DragOverlay({
  dragInfo,
  showPayment,
  translateX,
  translateY,
}: {
  dragInfo: DragInfo;
  showPayment: boolean;
  translateX: SharedValue<number>;
  translateY: SharedValue<number>;
}) {
  const { registration, side, startY } = dragInfo;
  const { user, paymentStatus } = registration;
  const fullName = `${user.firstName} ${user.lastName}`;
  const paymentInfo = getPaymentInfo(paymentStatus);

  const animatedStyle = useAnimatedStyle(() => ({
    transform: [
      { translateX: translateX.value },
      { translateY: translateY.value },
    ] as const,
  }));

  // Calculate initial position based on side
  const cellWidth = (SCREEN_WIDTH - CONTAINER_PADDING * 2 - SLOT_BADGE_WIDTH - CELL_MARGIN * 4) / 2;
  const leftPosition = side === 'left'
    ? CELL_MARGIN
    : SCREEN_WIDTH / 2 + SLOT_BADGE_WIDTH / 2 + CELL_MARGIN;

  return (
    <Animated.View
      style={[
        styles.dragOverlay,
        {
          top: startY,
          left: leftPosition,
          width: cellWidth,
        },
        animatedStyle,
      ]}
    >
      <View
        style={[
          styles.playerCell,
          styles.playerCellDragging,
          { marginRight: 0, marginLeft: 0 },
        ]}
      >
        {side === 'left' && (
          <>
            <Text style={[styles.playerName, styles.playerNameLeft]} numberOfLines={1}>
              {fullName}
            </Text>
            {showPayment && (
              <View style={[styles.paymentBadge, { backgroundColor: paymentInfo.bgColor }]}>
                <Text style={[styles.paymentBadgeText, { color: paymentInfo.color }]}>
                  {paymentInfo.label}
                </Text>
              </View>
            )}
          </>
        )}
        {side === 'right' && (
          <>
            {showPayment && (
              <View style={[styles.paymentBadge, { backgroundColor: paymentInfo.bgColor }]}>
                <Text style={[styles.paymentBadgeText, { color: paymentInfo.color }]}>
                  {paymentInfo.label}
                </Text>
              </View>
            )}
            <Text style={[styles.playerName, styles.playerNameRight]} numberOfLines={1}>
              {fullName}
            </Text>
          </>
        )}
      </View>
    </Animated.View>
  );
}

// Empty slot placeholder
function EmptySlot({ side }: { side: 'left' | 'right' }) {
  return (
    <View
      style={[
        styles.emptySlot,
        side === 'left' ? styles.playerCellLeft : styles.playerCellRight,
      ]}
    >
      <Text style={styles.emptyText}>-</Text>
    </View>
  );
}

// Source placeholder - shows where the dragged player came FROM (ghost style)
function SourcePlaceholder({ side }: { side: 'left' | 'right' }) {
  return (
    <View
      style={[
        styles.sourcePlaceholder,
        side === 'left' ? styles.playerCellLeft : styles.playerCellRight,
      ]}
    >
      <View style={styles.sourcePlaceholderInner} />
    </View>
  );
}

// Drop placeholder (teal dashed)
function DropPlaceholder() {
  return (
    <View style={styles.dropPlaceholder}>
      <View style={styles.dropPlaceholderInner} />
    </View>
  );
}

export function DraggableRoster({
  registrations,
  showPayment,
  onPlayerPress,
  onRosterChange,
}: DraggableRosterProps) {
  const rosterRef = useRef<View>(null);
  const [rosterTopOffset, setRosterTopOffset] = useState(0);

  // Drag state
  const [dragInfo, setDragInfo] = useState<DragInfo | null>(null);
  const [hoverSlotIndex, setHoverSlotIndex] = useState(-1);
  const [hoverTeam, setHoverTeam] = useState<TeamAssignment | null>(null);

  // Shared values for overlay position
  const translateX = useSharedValue(0);
  const translateY = useSharedValue(0);

  const handleRosterLayout = useCallback(() => {
    rosterRef.current?.measureInWindow((x, y) => {
      setRosterTopOffset(y);
    });
  }, []);

  // Build slots (memoized for performance)
  const slots = useMemo(() => {
    const blackGoalies = registrations
      .filter(r => r.teamAssignment === 'Black' && r.registeredPosition === 'Goalie')
      .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
    const whiteGoalies = registrations
      .filter(r => r.teamAssignment === 'White' && r.registeredPosition === 'Goalie')
      .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
    const blackSkaters = registrations
      .filter(r => r.teamAssignment === 'Black' && r.registeredPosition !== 'Goalie')
      .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
    const whiteSkaters = registrations
      .filter(r => r.teamAssignment === 'White' && r.registeredPosition !== 'Goalie')
      .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));

    const result: RosterSlot[] = [];
    result.push({
      type: 'goalie',
      index: 0,
      blackPlayer: blackGoalies[0] || null,
      whitePlayer: whiteGoalies[0] || null,
    });
    const maxSkaters = Math.max(blackSkaters.length, whiteSkaters.length, 1);
    for (let i = 0; i < maxSkaters; i++) {
      result.push({
        type: 'skater',
        index: i + 1,
        blackPlayer: blackSkaters[i] || null,
        whitePlayer: whiteSkaters[i] || null,
      });
    }
    return result;
  }, [registrations]);

  // Handlers called from worklet via runOnJS
  const handleDragMove = useCallback((absY: number, absX: number) => {
    const relativeY = absY - rosterTopOffset;
    const rowIndex = Math.floor(relativeY / ROW_HEIGHT);
    const targetSlotIndex = Math.max(0, Math.min(rowIndex, slots.length - 1));
    const screenCenter = SCREEN_WIDTH / 2;
    const targetTeam: TeamAssignment = absX < screenCenter ? 'Black' : 'White';

    setHoverSlotIndex(targetSlotIndex);
    setHoverTeam(targetTeam);
  }, [slots.length, rosterTopOffset]);

  const handleDragEnd = useCallback((absY: number, absX: number) => {
    if (!dragInfo) return;

    const relativeY = absY - rosterTopOffset;
    const rowIndex = Math.floor(relativeY / ROW_HEIGHT);
    const targetSlotIndex = Math.max(0, Math.min(rowIndex, slots.length - 1));
    const screenCenter = SCREEN_WIDTH / 2;
    const targetTeam: TeamAssignment = absX < screenCenter ? 'Black' : 'White';

    const reg = dragInfo.registration;
    const isGoalie = reg.registeredPosition === 'Goalie';
    const currentTeam = reg.teamAssignment as TeamAssignment;

    // Validate drop target
    if (isGoalie && targetSlotIndex !== 0) {
      // Invalid drop for goalie
      setDragInfo(null);
      setHoverSlotIndex(-1);
      setHoverTeam(null);
      translateX.value = 0;
      translateY.value = 0;
      return;
    }
    if (!isGoalie && targetSlotIndex === 0) {
      // Invalid drop for skater
      setDragInfo(null);
      setHoverSlotIndex(-1);
      setHoverTeam(null);
      translateX.value = 0;
      translateY.value = 0;
      return;
    }

    // Build fresh arrays
    const allBlackGoalies = registrations
      .filter(r => r.teamAssignment === 'Black' && r.registeredPosition === 'Goalie')
      .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
    const allWhiteGoalies = registrations
      .filter(r => r.teamAssignment === 'White' && r.registeredPosition === 'Goalie')
      .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
    const allBlackSkaters = registrations
      .filter(r => r.teamAssignment === 'Black' && r.registeredPosition !== 'Goalie')
      .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
    const allWhiteSkaters = registrations
      .filter(r => r.teamAssignment === 'White' && r.registeredPosition !== 'Goalie')
      .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));

    // Remove from current position
    if (isGoalie) {
      if (currentTeam === 'Black') {
        const idx = allBlackGoalies.findIndex(r => r.id === reg.id);
        if (idx >= 0) allBlackGoalies.splice(idx, 1);
      } else {
        const idx = allWhiteGoalies.findIndex(r => r.id === reg.id);
        if (idx >= 0) allWhiteGoalies.splice(idx, 1);
      }
    } else {
      if (currentTeam === 'Black') {
        const idx = allBlackSkaters.findIndex(r => r.id === reg.id);
        if (idx >= 0) allBlackSkaters.splice(idx, 1);
      } else {
        const idx = allWhiteSkaters.findIndex(r => r.id === reg.id);
        if (idx >= 0) allWhiteSkaters.splice(idx, 1);
      }
    }

    // Add to new position
    if (isGoalie) {
      if (targetTeam === 'Black') {
        allBlackGoalies.unshift(reg);
      } else {
        allWhiteGoalies.unshift(reg);
      }
    } else {
      const insertIndex = targetSlotIndex - 1;
      if (targetTeam === 'Black') {
        allBlackSkaters.splice(Math.min(insertIndex, allBlackSkaters.length), 0, reg);
      } else {
        allWhiteSkaters.splice(Math.min(insertIndex, allWhiteSkaters.length), 0, reg);
      }
    }

    // Build roster order items
    const items: RosterOrderItem[] = [];
    allBlackGoalies.forEach((r, i) => {
      items.push({ registrationId: r.id, teamAssignment: 'Black', rosterOrder: i });
    });
    allWhiteGoalies.forEach((r, i) => {
      items.push({ registrationId: r.id, teamAssignment: 'White', rosterOrder: i });
    });
    const goalieOffset = Math.max(allBlackGoalies.length, allWhiteGoalies.length, 1);
    allBlackSkaters.forEach((r, i) => {
      items.push({ registrationId: r.id, teamAssignment: 'Black', rosterOrder: goalieOffset + i });
    });
    allWhiteSkaters.forEach((r, i) => {
      items.push({ registrationId: r.id, teamAssignment: 'White', rosterOrder: goalieOffset + i });
    });

    // Clear drag state
    setDragInfo(null);
    setHoverSlotIndex(-1);
    setHoverTeam(null);
    translateX.value = 0;
    translateY.value = 0;

    // Update roster
    onRosterChange(items);
  }, [dragInfo, slots.length, rosterTopOffset, registrations, onRosterChange, translateX, translateY]);

  const handleDragCancel = useCallback(() => {
    setDragInfo(null);
    setHoverSlotIndex(-1);
    setHoverTeam(null);
    translateX.value = 0;
    translateY.value = 0;
  }, [translateX, translateY]);

  // Pan gesture for the entire roster area (captures drag after long press)
  const panGesture = Gesture.Pan()
    .onUpdate((event) => {
      'worklet';
      if (dragInfo) {
        translateX.value = event.translationX;
        translateY.value = event.translationY;
        runOnJS(handleDragMove)(event.absoluteY, event.absoluteX);
      }
    })
    .onEnd((event) => {
      'worklet';
      if (dragInfo) {
        runOnJS(handleDragEnd)(event.absoluteY, event.absoluteX);
      }
    })
    .onFinalize(() => {
      'worklet';
      // Clean up if drag was active but gesture ended without proper drop
      runOnJS(handleDragCancel)();
    });

  // Function to start drag from a player cell
  const startDrag = useCallback((registration: EventRegistrationDto, side: 'left' | 'right', slotIndex: number) => {
    const startY = slotIndex * ROW_HEIGHT;
    setDragInfo({
      registration,
      side,
      sourceSlotIndex: slotIndex,
      startY,
    });
  }, []);

  return (
    <GestureDetector gesture={panGesture}>
      <View style={styles.container}>
        {/* Team Headers */}
        <View style={styles.teamHeaders}>
          <View style={styles.teamHeaderLeft}>
            <View style={styles.teamDotBlack} />
            <Text style={styles.teamHeaderText}>BLACK</Text>
          </View>
          <View style={styles.teamHeaderRight}>
            <Text style={styles.teamHeaderText}>WHITE</Text>
            <View style={styles.teamDotWhite} />
          </View>
        </View>

        {/* Matchup Rows */}
        <View ref={rosterRef} style={styles.roster} onLayout={handleRosterLayout}>
          {slots.map((slot) => {
            // Check if this row is a valid drop target for the dragged player type
            const isValidDropTarget = dragInfo &&
              (slot.type === 'goalie' ? dragInfo.registration.registeredPosition === 'Goalie' : dragInfo.registration.registeredPosition !== 'Goalie');
            const isHoverRow = hoverSlotIndex === slot.index && isValidDropTarget;

            // Determine which specific SIDE is being hovered (not the whole row)
            const isBlackHovered = isHoverRow && hoverTeam === 'Black';
            const isWhiteHovered = isHoverRow && hoverTeam === 'White';

            return (
              <View
                key={slot.type === 'goalie' ? 'goalie' : `skater-${slot.index}`}
                style={styles.matchupRow}
              >
                {/* Black Team (Left) */}
                <View style={[
                  styles.playerSide,
                  isBlackHovered && styles.playerSideHovered,
                ]}>
                  {slot.blackPlayer ? (
                    dragInfo?.registration.id === slot.blackPlayer.id ? (
                      // Source slot - show ghost placeholder
                      <SourcePlaceholder side="left" />
                    ) : (
                      <PlayerCell
                        registration={slot.blackPlayer}
                        showPayment={showPayment}
                        side="left"
                        onPress={() => onPlayerPress(slot.blackPlayer!)}
                        onLongPress={() => startDrag(slot.blackPlayer!, 'left', slot.index)}
                      />
                    )
                  ) : (
                    isBlackHovered ? (
                      <DropPlaceholder />
                    ) : (
                      <EmptySlot side="left" />
                    )
                  )}
                </View>

                {/* Center Slot Badge */}
                <View style={[styles.slotBadge, slot.type === 'goalie' && styles.slotBadgeGoalie]}>
                  <Text style={[styles.slotBadgeText, slot.type === 'goalie' && styles.slotBadgeTextGoalie]}>
                    {slot.type === 'goalie' ? 'G' : `${slot.index}`}
                  </Text>
                </View>

                {/* White Team (Right) */}
                <View style={[
                  styles.playerSide,
                  isWhiteHovered && styles.playerSideHovered,
                ]}>
                  {slot.whitePlayer ? (
                    dragInfo?.registration.id === slot.whitePlayer.id ? (
                      // Source slot - show ghost placeholder
                      <SourcePlaceholder side="right" />
                    ) : (
                      <PlayerCell
                        registration={slot.whitePlayer}
                        showPayment={showPayment}
                        side="right"
                        onPress={() => onPlayerPress(slot.whitePlayer!)}
                        onLongPress={() => startDrag(slot.whitePlayer!, 'right', slot.index)}
                      />
                    )
                  ) : (
                    isWhiteHovered ? (
                      <DropPlaceholder />
                    ) : (
                      <EmptySlot side="right" />
                    )
                  )}
                </View>
              </View>
            );
          })}

          {/* Drag Overlay - renders on top when dragging */}
          {dragInfo && (
            <DragOverlay
              dragInfo={dragInfo}
              showPayment={showPayment}
              translateX={translateX}
              translateY={translateY}
            />
          )}
        </View>

        {/* Hint */}
        <Text style={styles.hint}>Tap for options • Hold and drag to move</Text>
      </View>
    </GestureDetector>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  teamHeaders: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingHorizontal: spacing.md,
    paddingBottom: spacing.sm,
  },
  teamHeaderLeft: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  teamHeaderRight: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  teamDotBlack: {
    width: 10,
    height: 10,
    borderRadius: 5,
    backgroundColor: '#1a1a1a',
    borderWidth: 1,
    borderColor: colors.border.muted,
    marginRight: spacing.xs,
  },
  teamDotWhite: {
    width: 10,
    height: 10,
    borderRadius: 5,
    backgroundColor: colors.text.primary,
    marginLeft: spacing.xs,
  },
  teamHeaderText: {
    fontSize: 11,
    fontWeight: '700',
    color: colors.text.muted,
    letterSpacing: 1,
  },
  roster: {
    gap: 2,
    position: 'relative',
  },
  matchupRow: {
    flexDirection: 'row',
    alignItems: 'center',
    height: ROW_HEIGHT,
    paddingHorizontal: spacing.xs,
  },
  playerSide: {
    flex: 1,
    height: '100%',
    justifyContent: 'center',
    borderRadius: radius.sm,
    paddingVertical: 2,
  },
  playerSideHovered: {
    backgroundColor: colors.subtle.teal,
  },
  playerCell: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bg.dark,
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.sm,
    borderRadius: radius.sm,
    height: 48,
  },
  playerCellDragging: {
    backgroundColor: colors.bg.elevated,
    borderWidth: 2,
    borderColor: colors.primary.teal,
  },
  playerCellLeft: {
    marginRight: spacing.xs,
    justifyContent: 'flex-end',
  },
  playerCellRight: {
    marginLeft: spacing.xs,
    justifyContent: 'flex-start',
  },
  playerName: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.primary,
    flexShrink: 1,
  },
  playerNameLeft: {
    textAlign: 'right',
  },
  playerNameRight: {
    textAlign: 'left',
  },
  paymentBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: 3,
    borderRadius: radius.sm,
    marginHorizontal: spacing.xs,
    flexShrink: 0,
  },
  paymentBadgeText: {
    fontSize: 10,
    fontWeight: '700',
  },
  slotBadge: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: colors.bg.elevated,
    justifyContent: 'center',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  slotBadgeGoalie: {
    backgroundColor: colors.primary.teal,
    borderColor: colors.primary.teal,
  },
  slotBadgeText: {
    fontSize: 12,
    fontWeight: '700',
    color: colors.text.muted,
  },
  slotBadgeTextGoalie: {
    color: colors.bg.darkest,
  },
  emptySlot: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bg.elevated,
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.sm,
    borderRadius: radius.sm,
    height: 48,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderStyle: 'dashed',
  },
  emptyText: {
    fontSize: 14,
    color: colors.text.subtle,
    flex: 1,
    textAlign: 'center',
  },
  // Source placeholder - ghost showing where player came FROM
  sourcePlaceholder: {
    flex: 1,
    height: 48,
    justifyContent: 'center',
    alignItems: 'center',
  },
  sourcePlaceholderInner: {
    width: '100%',
    height: 48,
    borderRadius: radius.sm,
    borderWidth: 2,
    borderColor: colors.primary.teal,
    borderStyle: 'dashed',
    backgroundColor: colors.subtle.teal,
    opacity: 0.6,
  },
  dropPlaceholder: {
    flex: 1,
    height: 48,
    justifyContent: 'center',
    alignItems: 'center',
    marginHorizontal: spacing.xs,
  },
  dropPlaceholderInner: {
    width: '100%',
    height: 48,
    borderRadius: radius.sm,
    borderWidth: 2,
    borderColor: colors.primary.teal,
    borderStyle: 'dashed',
    backgroundColor: colors.subtle.teal,
  },
  dragOverlay: {
    position: 'absolute',
    zIndex: 1000,
    elevation: 10,
  },
  hint: {
    fontSize: 12,
    color: colors.text.subtle,
    textAlign: 'center',
    marginTop: spacing.md,
  },
});
