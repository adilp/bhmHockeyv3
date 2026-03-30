import React, { useCallback, useState, useRef, useMemo, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Dimensions,
  Pressable,
  LayoutChangeEvent,
} from 'react-native';
import { Gesture, GestureDetector } from 'react-native-gesture-handler';
import Animated, {
  useAnimatedStyle,
  useSharedValue,
  runOnJS,
  SharedValue,
} from 'react-native-reanimated';
import type { EventRegistrationDto, TeamAssignment, RosterOrderItem, SkillLevel } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../theme';
import { BadgeIconsRow } from './badges';
import { Badge } from './Badge';
import { buildRosterSlots, buildRosterOrderItems, type RosterSlot } from './DraggableRoster.utils';
import { getPaymentBadgeInfo } from '../utils/payment';

const SCREEN_WIDTH = Dimensions.get('window').width;
const ROW_HEIGHT = 94; // 88px cell + 6px gap
const ROSTER_GAP = 2; // gap between rows in roster container
const ROW_PITCH = ROW_HEIGHT + ROSTER_GAP; // effective row spacing
const SLOT_BADGE_WIDTH = 32;
const CONTAINER_PADDING = spacing.md; // 16px on each side = 32 total
const CELL_MARGIN = spacing.xs; // margin between cells and badge

// Position cycle order: number → LW → RW → C → LD → RD → number
const POSITION_CYCLE = ['LW', 'RW', 'C', 'LD', 'RD'] as const;

interface DraggableRosterProps {
  registrations: EventRegistrationDto[];
  onPlayerPress: (registration: EventRegistrationDto) => void;
  onRosterChange?: (items: RosterOrderItem[]) => void;
  /** When true, disables drag-and-drop reordering (view-only mode) */
  readOnly?: boolean;
  /** Slot position labels (maps slot index to position label like "C", "LW", etc.) */
  slotPositionLabels?: Record<number, string>;
  /** Callback when a slot label is tapped (only called when canManage=true) */
  onSlotLabelChange?: (slotIndex: number, newLabel: string | null) => void;
  /** When true, shows payment status indicators for unpaid players instead of badges */
  canManage?: boolean;
  /** Notifies parent when drag starts/ends (used to disable scroll during drag) */
  onDragStateChange?: (isDragging: boolean) => void;
}


interface DragInfo {
  registration: EventRegistrationDto;
  side: 'left' | 'right';
  sourceSlotIndex: number;
  startY: number;
}

// Get skill level info based on registered position
const getSkillLevelInfo = (registration: EventRegistrationDto): { level: SkillLevel | null; color: string } => {
  const { user, registeredPosition } = registration;
  const positions = user.positions;

  if (!positions) return { level: null, color: colors.text.muted };

  const skillLevel: SkillLevel | undefined =
    registeredPosition === 'Goalie' ? positions.goalie : positions.skater;

  if (!skillLevel) return { level: null, color: colors.text.muted };

  return { level: skillLevel, color: colors.skillLevel[skillLevel] || colors.text.muted };
};

// Static player cell (non-draggable, just displays)
// 2-line layout: Name, Achievement Badges
// Vertical skill level bar component
function SkillBar({ level, color, side }: { level: SkillLevel | null; color: string; side: 'left' | 'right' }) {
  if (!level) return null;

  return (
    <View style={[styles.skillBar, side === 'left' ? styles.skillBarLeft : styles.skillBarRight, { backgroundColor: color }]}>
      <Text style={styles.skillBarText} allowFontScaling={false}>{level}</Text>
    </View>
  );
}

function PlayerCell({
  registration,
  side,
  onPress,
  onLongPress,
  canManage,
}: {
  registration: EventRegistrationDto;
  side: 'left' | 'right';
  onPress: () => void;
  onLongPress: () => void;
  canManage?: boolean;
}) {
  const { user } = registration;
  const fullName = `${user.firstName} ${user.lastName}`;
  const skillInfo = getSkillLevelInfo(registration);

  // For organizers: show payment status if not verified
  const showPaymentStatus = canManage && registration.paymentStatus && registration.paymentStatus !== 'Verified';

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
      {/* Skill level bar on inner edge */}
      <SkillBar level={skillInfo.level} color={skillInfo.color} side={side} />

      {/* Line 1: Name */}
      <Text
        style={[styles.playerName, side === 'left' ? styles.playerNameLeft : styles.playerNameRight]}
        numberOfLines={1}
        allowFontScaling={false}
      >
        {fullName}
      </Text>
      {/* Line 2: Payment status (organizer only, unpaid) or badges/guest */}
      {showPaymentStatus ? (
        <Badge variant={getPaymentBadgeInfo(registration.paymentStatus).variant}>
          {getPaymentBadgeInfo(registration.paymentStatus).text}
        </Badge>
      ) : registration.user.isGhostPlayer ? (
        <Text style={[styles.guestLabel, side === 'left' ? styles.guestLabelLeft : styles.guestLabelRight]} allowFontScaling={false}>
          Guest
        </Text>
      ) : (
        <BadgeIconsRow
          badges={user.badges || []}
          totalCount={user.totalBadgeCount || 0}
        />
      )}
    </Pressable>
  );
}

// Drag overlay - follows finger during drag
// Mirrors PlayerCell 2-line layout for consistent drag visuals
function DragOverlay({
  dragInfo,
  translateX,
  translateY,
}: {
  dragInfo: DragInfo;
  translateX: SharedValue<number>;
  translateY: SharedValue<number>;
}) {
  const { registration, side, startY } = dragInfo;
  const { user } = registration;
  const fullName = `${user.firstName} ${user.lastName}`;
  const skillInfo = getSkillLevelInfo(registration);

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
          side === 'left' ? styles.playerCellLeft : styles.playerCellRight,
          { marginRight: 0, marginLeft: 0 },
        ]}
      >
        {/* Skill level bar on inner edge */}
        <SkillBar level={skillInfo.level} color={skillInfo.color} side={side} />

        {/* Line 1: Name */}
        <Text
          style={[styles.playerName, side === 'left' ? styles.playerNameLeft : styles.playerNameRight]}
          numberOfLines={1}
          allowFontScaling={false}
        >
          {fullName}
        </Text>
        {/* Line 2: Achievement Badges or Guest label */}
        {registration.user.isGhostPlayer ? (
          <Text style={[styles.guestLabel, side === 'left' ? styles.guestLabelLeft : styles.guestLabelRight]} allowFontScaling={false}>
            Guest
          </Text>
        ) : (
          <BadgeIconsRow
            badges={user.badges || []}
            totalCount={user.totalBadgeCount || 0}
          />
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
      <Text style={styles.emptyText} allowFontScaling={false}>-</Text>
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
  onPlayerPress,
  onRosterChange,
  readOnly = false,
  slotPositionLabels,
  onSlotLabelChange,
  canManage = false,
  onDragStateChange,
}: DraggableRosterProps) {
  const [rosterTopOffset, setRosterTopOffset] = useState(0);

  // Drag state
  const [dragInfo, setDragInfo] = useState<DragInfo | null>(null);
  const [hoverSlotIndex, setHoverSlotIndex] = useState(-1);
  const [hoverTeam, setHoverTeam] = useState<TeamAssignment | null>(null);

  // Shared values for overlay position
  const translateX = useSharedValue(0);
  const translateY = useSharedValue(0);

  // Capture roster offset within the container (stable across scrolling, unlike measureInWindow)
  const handleRosterLayout = useCallback((e: LayoutChangeEvent) => {
    setRosterTopOffset(e.nativeEvent.layout.y);
  }, []);

  // Ref tracks latest labels so rapid taps always cycle correctly (no stale props between renders)
  const labelsRef = useRef(slotPositionLabels);
  useEffect(() => { labelsRef.current = slotPositionLabels; }, [slotPositionLabels]);

  // Cycle through position labels when slot badge is tapped: number -> LW -> RW -> C -> LD -> RD -> number
  const handleSlotLabelTap = useCallback((slotIndex: number, isGoalie: boolean) => {
    if (!onSlotLabelChange || isGoalie) return;

    const currentLabel = labelsRef.current?.[slotIndex];
    const nextLabel = (() => {
      if (!currentLabel) return POSITION_CYCLE[0];
      const currentIndex = POSITION_CYCLE.indexOf(currentLabel as typeof POSITION_CYCLE[number]);
      const isLastOrUnknown = currentIndex === -1 || currentIndex === POSITION_CYCLE.length - 1;
      return isLastOrUnknown ? null : POSITION_CYCLE[currentIndex + 1];
    })();

    // Update ref immediately so the next rapid tap reads the correct value
    labelsRef.current = nextLabel === null
      ? (() => { const { [slotIndex]: _, ...rest } = labelsRef.current || {}; return rest; })()
      : { ...labelsRef.current, [slotIndex]: nextLabel };

    onSlotLabelChange(slotIndex, nextLabel);
  }, [onSlotLabelChange]);

  // Build slots (memoized for performance)
  const slots = useMemo(() => buildRosterSlots(registrations), [registrations]);
  const goalieRowCount = useMemo(() => slots.filter(s => s.type === 'goalie').length, [slots]);

  // Handlers called from worklet via runOnJS
  // y is relative to the GestureDetector view (container); subtract rosterTopOffset to get roster-relative
  const handleDragMove = useCallback((y: number, absX: number) => {
    const relativeY = y - rosterTopOffset;
    const rowIndex = Math.floor(relativeY / ROW_PITCH);
    const targetSlotIndex = Math.max(0, Math.min(rowIndex, slots.length - 1));
    const screenCenter = SCREEN_WIDTH / 2;
    const targetTeam: TeamAssignment = absX < screenCenter ? 'Black' : 'White';

    setHoverSlotIndex(targetSlotIndex);
    setHoverTeam(targetTeam);
  }, [slots.length, rosterTopOffset]);

  const handleDragEnd = useCallback((y: number, absX: number) => {
    if (!dragInfo) return;

    const relativeY = y - rosterTopOffset;
    const rowIndex = Math.floor(relativeY / ROW_PITCH);
    const targetSlotIndex = Math.max(0, Math.min(rowIndex, slots.length - 1));
    const screenCenter = SCREEN_WIDTH / 2;
    const targetTeam: TeamAssignment = absX < screenCenter ? 'Black' : 'White';

    const items = buildRosterOrderItems(registrations, dragInfo.registration, targetTeam, targetSlotIndex);

    // Clear drag state
    setDragInfo(null);
    setHoverSlotIndex(-1);
    setHoverTeam(null);
    translateX.value = 0;
    translateY.value = 0;
    onDragStateChange?.(false);

    // Update roster if valid drop
    if (items) {
      onRosterChange?.(items);
    }
  }, [dragInfo, slots.length, rosterTopOffset, registrations, onRosterChange, translateX, translateY, onDragStateChange]);

  const handleDragCancel = useCallback(() => {
    setDragInfo(null);
    setHoverSlotIndex(-1);
    setHoverTeam(null);
    translateX.value = 0;
    translateY.value = 0;
    onDragStateChange?.(false);
  }, [translateX, translateY, onDragStateChange]);

  // Shared value to track if drag is active (for worklet access)
  const isDragging = useSharedValue(false);

  // Update isDragging when dragInfo changes
  useEffect(() => {
    isDragging.value = !!dragInfo;
  }, [dragInfo, isDragging]);

  // Pan gesture for drag-and-drop
  // Uses manualActivation so it only captures touches when we're dragging
  const panGesture = Gesture.Pan()
    .manualActivation(true)
    .onTouchesMove((event, stateManager) => {
      // Only activate the gesture if we're in drag mode
      if (isDragging.value) {
        stateManager.activate();
      } else {
        stateManager.fail();
      }
    })
    .onUpdate((event) => {
      'worklet';
      translateX.value = event.translationX;
      translateY.value = event.translationY;
      runOnJS(handleDragMove)(event.y, event.absoluteX);
    })
    .onEnd((event) => {
      'worklet';
      runOnJS(handleDragEnd)(event.y, event.absoluteX);
    })
    .onFinalize(() => {
      'worklet';
      if (isDragging.value) {
        runOnJS(handleDragCancel)();
      }
    });

  // Function to start drag from a player cell
  const startDrag = useCallback((registration: EventRegistrationDto, side: 'left' | 'right', slotIndex: number) => {
    const startY = slotIndex * ROW_PITCH;
    setDragInfo({
      registration,
      side,
      sourceSlotIndex: slotIndex,
      startY,
    });
    onDragStateChange?.(true);
  }, [onDragStateChange]);

  return (
    <GestureDetector gesture={panGesture}>
      <View style={styles.container}>
        {/* Team Headers */}
        <View style={styles.teamHeaders}>
          <View style={styles.teamHeaderLeft}>
            <View style={styles.teamDotBlack} />
            <Text style={styles.teamHeaderText} allowFontScaling={false}>BLACK</Text>
          </View>
          <View style={styles.teamHeaderRight}>
            <Text style={styles.teamHeaderText} allowFontScaling={false}>WHITE</Text>
            <View style={styles.teamDotWhite} />
          </View>
        </View>

        {/* Matchup Rows */}
        <View style={styles.roster} onLayout={handleRosterLayout}>
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
                key={`${slot.type}-${slot.index}`}
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
                        side="left"
                        onPress={() => onPlayerPress(slot.blackPlayer!)}
                        onLongPress={readOnly ? () => {} : () => startDrag(slot.blackPlayer!, 'left', slot.index)}
                        canManage={canManage}
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
                {(() => {
                  const isGoalie = slot.type === 'goalie';
                  const skaterNumber = slot.index - goalieRowCount + 1;
                  const positionLabel = isGoalie ? 'G' : (slotPositionLabels?.[slot.index] || `${skaterNumber}`);
                  const hasCustomLabel = !isGoalie && slotPositionLabels?.[slot.index];
                  const isTappable = onSlotLabelChange && !isGoalie && !readOnly;

                  const badge = (
                    <View style={[
                      styles.slotBadge,
                      isGoalie && styles.slotBadgeGoalie,
                      hasCustomLabel && styles.slotBadgeCustom,
                    ]}>
                      <Text
                        style={[
                          styles.slotBadgeText,
                          isGoalie && styles.slotBadgeTextGoalie,
                          hasCustomLabel && styles.slotBadgeTextCustom,
                        ]}
                        allowFontScaling={false}
                      >
                        {positionLabel}
                      </Text>
                    </View>
                  );

                  return isTappable ? (
                    <Pressable onPress={() => handleSlotLabelTap(slot.index, isGoalie)}>
                      {badge}
                    </Pressable>
                  ) : badge;
                })()}

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
                        side="right"
                        onPress={() => onPlayerPress(slot.whitePlayer!)}
                        onLongPress={readOnly ? () => {} : () => startDrag(slot.whitePlayer!, 'right', slot.index)}
                        canManage={canManage}
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
              translateX={translateX}
              translateY={translateY}
            />
          )}
        </View>

        {/* Hint */}
        <Text style={styles.hint} allowFontScaling={false}>Tap for options • Hold and drag to move</Text>
      </View>
    </GestureDetector>
  );
}

const styles = StyleSheet.create({
  container: {
    // No flex: 1 here - let content determine height for ScrollView compatibility
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
    flexDirection: 'column',
    justifyContent: 'center',
    backgroundColor: colors.bg.dark,
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.sm,
    borderRadius: radius.sm,
    height: 88,
    gap: 6,
  },
  playerCellDragging: {
    backgroundColor: colors.bg.elevated,
    borderWidth: 2,
    borderColor: colors.primary.teal,
  },
  playerCellLeft: {
    marginRight: spacing.xs,
    alignItems: 'flex-end',
    paddingRight: 28,
  },
  playerCellRight: {
    marginLeft: spacing.xs,
    alignItems: 'flex-start',
    paddingLeft: 28,
  },
  playerName: {
    fontSize: 14,
    fontWeight: '600',
    flexShrink: 1,
    color: colors.text.primary,
  },
  playerNameLeft: {
    textAlign: 'right',
  },
  playerNameRight: {
    textAlign: 'left',
  },
  guestLabel: {
    fontSize: 12,
    fontWeight: '500',
    color: colors.text.muted,
    fontStyle: 'italic',
  },
  guestLabelLeft: {
    textAlign: 'right',
    paddingRight: 28,
  },
  guestLabelRight: {
    textAlign: 'left',
    paddingLeft: 28,
  },
  skillBar: {
    position: 'absolute',
    top: 4,
    bottom: 4,
    width: 20,
    borderRadius: radius.sm,
    justifyContent: 'center',
    alignItems: 'center',
  },
  skillBarLeft: {
    right: 4,
  },
  skillBarRight: {
    left: 4,
  },
  skillBarText: {
    fontSize: 9,
    fontWeight: '700',
    color: colors.bg.darkest,
    transform: [{ rotate: '90deg' }],
    width: 70,
    textAlign: 'center',
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
  slotBadgeCustom: {
    backgroundColor: colors.primary.purple,
    borderColor: colors.primary.purple,
  },
  slotBadgeText: {
    fontSize: 12,
    fontWeight: '700',
    color: colors.text.muted,
  },
  slotBadgeTextGoalie: {
    color: colors.bg.darkest,
  },
  slotBadgeTextCustom: {
    color: colors.text.primary,
    fontSize: 10,
  },
  emptySlot: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.bg.elevated,
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.sm,
    borderRadius: radius.sm,
    height: 88,
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
    height: 88,
    justifyContent: 'center',
    alignItems: 'center',
  },
  sourcePlaceholderInner: {
    width: '100%',
    height: 88,
    borderRadius: radius.sm,
    borderWidth: 2,
    borderColor: colors.primary.teal,
    borderStyle: 'dashed',
    backgroundColor: colors.subtle.teal,
    opacity: 0.6,
  },
  dropPlaceholder: {
    flex: 1,
    height: 88,
    justifyContent: 'center',
    alignItems: 'center',
    marginHorizontal: spacing.xs,
  },
  dropPlaceholderInner: {
    width: '100%',
    height: 88,
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
