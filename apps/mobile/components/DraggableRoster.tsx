import React, { useCallback } from 'react';
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
  withSpring,
  runOnJS,
} from 'react-native-reanimated';
import type { EventRegistrationDto, TeamAssignment, RosterOrderItem } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../theme';

const SCREEN_WIDTH = Dimensions.get('window').width;
const ROW_HEIGHT = 56;
const SPRING_CONFIG = { damping: 15, stiffness: 150 };

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

// Individual draggable player cell
function DraggablePlayer({
  registration,
  showPayment,
  side,
  onTap,
  onDragEnd,
  totalSlots,
  isGoalie,
}: {
  registration: EventRegistrationDto;
  showPayment: boolean;
  side: 'left' | 'right';
  onTap: () => void;
  onDragEnd: (reg: EventRegistrationDto, newTeam: TeamAssignment, newSlotIndex: number) => void;
  totalSlots: number;
  isGoalie: boolean;
}) {
  const translateX = useSharedValue(0);
  const translateY = useSharedValue(0);
  const scale = useSharedValue(1);
  const isDragging = useSharedValue(false);

  const handleDragComplete = useCallback(
    (absoluteY: number, absoluteX: number) => {
      // Determine which slot based on Y position
      const rowIndex = Math.floor(absoluteY / ROW_HEIGHT);
      const targetSlotIndex = Math.max(0, Math.min(rowIndex, totalSlots - 1));

      // Determine which team based on X position
      const screenCenter = SCREEN_WIDTH / 2;
      const targetTeam: TeamAssignment = absoluteX < screenCenter ? 'Black' : 'White';

      // Goalies can only go to goalie slot (index 0)
      // Skaters can only go to skater slots (index > 0)
      if (isGoalie && targetSlotIndex !== 0) {
        return; // Invalid drop for goalie
      }
      if (!isGoalie && targetSlotIndex === 0) {
        return; // Invalid drop for skater
      }

      onDragEnd(registration, targetTeam, targetSlotIndex);
    },
    [totalSlots, isGoalie, registration, onDragEnd]
  );

  // Long press + drag gesture
  const dragGesture = Gesture.Pan()
    .activateAfterLongPress(250)
    .onStart(() => {
      'worklet';
      isDragging.value = true;
      scale.value = withSpring(1.08, SPRING_CONFIG);
    })
    .onUpdate((event) => {
      'worklet';
      translateX.value = event.translationX;
      translateY.value = event.translationY;
    })
    .onEnd((event) => {
      'worklet';
      runOnJS(handleDragComplete)(event.absoluteY, event.absoluteX);
      translateX.value = withSpring(0, SPRING_CONFIG);
      translateY.value = withSpring(0, SPRING_CONFIG);
      scale.value = withSpring(1, SPRING_CONFIG);
      isDragging.value = false;
    })
    .onFinalize(() => {
      'worklet';
      translateX.value = withSpring(0, SPRING_CONFIG);
      translateY.value = withSpring(0, SPRING_CONFIG);
      scale.value = withSpring(1, SPRING_CONFIG);
      isDragging.value = false;
    });

  const animatedStyle = useAnimatedStyle(() => {
    'worklet';
    return {
      transform: [
        { translateX: translateX.value },
        { translateY: translateY.value },
        { scale: scale.value },
      ],
      zIndex: isDragging.value ? 100 : 0,
      elevation: isDragging.value ? 10 : 0,
    };
  });

  const { user, paymentStatus } = registration;
  const fullName = `${user.firstName} ${user.lastName}`;
  const paymentInfo = getPaymentInfo(paymentStatus);

  return (
    <GestureDetector gesture={dragGesture}>
      <Animated.View style={[styles.animatedWrapper, animatedStyle]}>
        <Pressable
          style={[
            styles.playerCell,
            side === 'left' ? styles.playerCellLeft : styles.playerCellRight,
          ]}
          onPress={onTap}
        >
          {/* Left side: Name then Badge */}
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

          {/* Right side: Badge then Name */}
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
      </Animated.View>
    </GestureDetector>
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

// Matchup row (one slot with players from both teams)
function MatchupRow({
  slot,
  showPayment,
  onPlayerPress,
  onDragEnd,
  totalSlots,
}: {
  slot: RosterSlot;
  showPayment: boolean;
  onPlayerPress: (registration: EventRegistrationDto) => void;
  onDragEnd: (reg: EventRegistrationDto, newTeam: TeamAssignment, newSlotIndex: number) => void;
  totalSlots: number;
}) {
  const slotLabel = slot.type === 'goalie' ? 'G' : `${slot.index}`;
  const isGoalieSlot = slot.type === 'goalie';

  return (
    <View style={styles.matchupRow}>
      {/* Black Team (Left) */}
      <View style={styles.playerSide}>
        {slot.blackPlayer ? (
          <DraggablePlayer
            registration={slot.blackPlayer}
            showPayment={showPayment}
            side="left"
            onTap={() => onPlayerPress(slot.blackPlayer!)}
            onDragEnd={onDragEnd}
            totalSlots={totalSlots}
            isGoalie={slot.blackPlayer.registeredPosition === 'Goalie'}
          />
        ) : (
          <EmptySlot side="left" />
        )}
      </View>

      {/* Center Slot Badge */}
      <View style={[styles.slotBadge, isGoalieSlot && styles.slotBadgeGoalie]}>
        <Text style={[styles.slotBadgeText, isGoalieSlot && styles.slotBadgeTextGoalie]}>
          {slotLabel}
        </Text>
      </View>

      {/* White Team (Right) */}
      <View style={styles.playerSide}>
        {slot.whitePlayer ? (
          <DraggablePlayer
            registration={slot.whitePlayer}
            showPayment={showPayment}
            side="right"
            onTap={() => onPlayerPress(slot.whitePlayer!)}
            onDragEnd={onDragEnd}
            totalSlots={totalSlots}
            isGoalie={slot.whitePlayer.registeredPosition === 'Goalie'}
          />
        ) : (
          <EmptySlot side="right" />
        )}
      </View>
    </View>
  );
}

export function DraggableRoster({
  registrations,
  showPayment,
  onPlayerPress,
  onRosterChange,
}: DraggableRosterProps) {
  // Separate goalies and skaters, sorted by roster order
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

  // Build slot structure
  const slots: RosterSlot[] = [];

  // Goalie slot (always first)
  slots.push({
    type: 'goalie',
    index: 0,
    blackPlayer: blackGoalies[0] || null,
    whitePlayer: whiteGoalies[0] || null,
  });

  // Skater slots
  const maxSkaters = Math.max(blackSkaters.length, whiteSkaters.length, 1);
  for (let i = 0; i < maxSkaters; i++) {
    slots.push({
      type: 'skater',
      index: i + 1,
      blackPlayer: blackSkaters[i] || null,
      whitePlayer: whiteSkaters[i] || null,
    });
  }

  // Handle drag end - update roster
  const handleDragEnd = useCallback(
    (reg: EventRegistrationDto, newTeam: TeamAssignment, newSlotIndex: number) => {
      const isGoalie = reg.registeredPosition === 'Goalie';
      const currentTeam = reg.teamAssignment as TeamAssignment;

      // Get all players grouped
      const allBlackGoalies = [...blackGoalies];
      const allWhiteGoalies = [...whiteGoalies];
      const allBlackSkaters = [...blackSkaters];
      const allWhiteSkaters = [...whiteSkaters];

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
        if (newTeam === 'Black') {
          allBlackGoalies.unshift(reg);
        } else {
          allWhiteGoalies.unshift(reg);
        }
      } else {
        const insertIndex = newSlotIndex - 1;
        if (newTeam === 'Black') {
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

      onRosterChange(items);
    },
    [blackGoalies, whiteGoalies, blackSkaters, whiteSkaters, onRosterChange]
  );

  return (
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
      <View style={styles.roster}>
        {slots.map((slot) => (
          <MatchupRow
            key={slot.type === 'goalie' ? 'goalie' : `skater-${slot.index}`}
            slot={slot}
            showPayment={showPayment}
            onPlayerPress={onPlayerPress}
            onDragEnd={handleDragEnd}
            totalSlots={slots.length}
          />
        ))}
      </View>

      {/* Hint */}
      <Text style={styles.hint}>Tap for options • Hold and drag to move</Text>
    </View>
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
  },
  animatedWrapper: {
    flex: 1,
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
  hint: {
    fontSize: 12,
    color: colors.text.subtle,
    textAlign: 'center',
    marginTop: spacing.md,
  },
});
