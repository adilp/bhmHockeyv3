import React, { useState, useCallback, useRef, useMemo } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ViewStyle,
  StyleProp,
  TouchableOpacity,
  LayoutChangeEvent,
} from 'react-native';
import { Gesture, GestureDetector, GestureHandlerRootView } from 'react-native-gesture-handler';
import Animated, {
  useAnimatedStyle,
  useSharedValue,
  runOnJS,
  SharedValue,
} from 'react-native-reanimated';
import type { UserBadgeDto } from '@bhmhockey/shared';
import { BadgeIcon } from './BadgeIcon';
import { colors, spacing, radius } from '../../theme';

const ROW_HEIGHT = 88; // Height of each badge row for drag calculations (increased for 48px icons)

interface TrophyCaseProps {
  /** Array of all user badges */
  badges: UserBadgeDto[];
  /** Optional style for the container */
  style?: StyleProp<ViewStyle>;
  /** Enable edit mode with drag-to-reorder */
  editable?: boolean;
  /** Callback when badge order changes (receives ordered badge IDs) */
  onOrderChange?: (badgeIds: string[]) => void;
}

interface DragInfo {
  badge: UserBadgeDto;
  index: number;
  startY: number;
}

/**
 * Extracts display context text from badge context JSON
 * Handles tournament badges (tournamentName) and generic badges (description)
 */
function getContextText(context: Record<string, unknown>): string | null {
  if (typeof context.tournamentName === 'string') {
    return context.tournamentName;
  }
  if (typeof context.description === 'string') {
    return context.description;
  }
  return null;
}

/**
 * Formats an ISO date string to "Earned [Month Day, Year]"
 */
function formatEarnedDate(isoDate: string): string {
  try {
    const date = new Date(isoDate);
    const formatted = date.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
    return `Earned ${formatted}`;
  } catch {
    return 'Earned';
  }
}

/**
 * Badge row component - displays a single badge with optional drag handle
 */
function BadgeRow({
  badge,
  isEditMode,
  onLongPress,
  isDragging,
}: {
  badge: UserBadgeDto;
  isEditMode: boolean;
  onLongPress: () => void;
  isDragging: boolean;
}) {
  const contextText = getContextText(badge.context);
  const earnedText = formatEarnedDate(badge.earnedAt);

  return (
    <TouchableOpacity
      style={[styles.badgeRow, isDragging && styles.badgeRowDragging]}
      onLongPress={isEditMode ? onLongPress : undefined}
      delayLongPress={150}
      activeOpacity={isEditMode ? 0.7 : 1}
      disabled={!isEditMode}
    >
      {/* Drag handle - only visible in edit mode */}
      {isEditMode && (
        <View style={styles.dragHandle}>
          <View style={styles.dragHandleLine} />
          <View style={styles.dragHandleLine} />
          <View style={styles.dragHandleLine} />
        </View>
      )}
      <BadgeIcon
        iconName={badge.badgeType.iconName}
        size={48}
        style={styles.icon}
      />
      <View style={styles.badgeInfo}>
        <Text style={styles.badgeName}>{badge.badgeType.name}</Text>
        {contextText && (
          <Text style={styles.badgeContext}>{contextText}</Text>
        )}
        <Text style={styles.badgeDate}>{earnedText}</Text>
      </View>
    </TouchableOpacity>
  );
}

/**
 * Drag overlay - follows finger during drag
 */
function DragOverlay({
  dragInfo,
  translateY,
}: {
  dragInfo: DragInfo;
  translateY: SharedValue<number>;
}) {
  const { badge } = dragInfo;
  const contextText = getContextText(badge.context);
  const earnedText = formatEarnedDate(badge.earnedAt);

  const animatedStyle = useAnimatedStyle(() => ({
    transform: [{ translateY: translateY.value }] as const,
  }));

  return (
    <Animated.View
      style={[
        styles.dragOverlay,
        { top: dragInfo.startY },
        animatedStyle,
      ]}
    >
      <View style={[styles.badgeRow, styles.badgeRowOverlay]}>
        <View style={styles.dragHandle}>
          <View style={styles.dragHandleLine} />
          <View style={styles.dragHandleLine} />
          <View style={styles.dragHandleLine} />
        </View>
        <BadgeIcon
          iconName={badge.badgeType.iconName}
          size={48}
          style={styles.icon}
        />
        <View style={styles.badgeInfo}>
          <Text style={styles.badgeName}>{badge.badgeType.name}</Text>
          {contextText && (
            <Text style={styles.badgeContext}>{contextText}</Text>
          )}
          <Text style={styles.badgeDate}>{earnedText}</Text>
        </View>
      </View>
    </Animated.View>
  );
}

/**
 * TrophyCase - Displays full badge list with details
 *
 * Used in PlayerDetailModal and Profile screen to show all user badges
 * with name, context, and earned date.
 *
 * When editable=true, shows Edit/Done toggle and allows drag-to-reorder.
 *
 * Each badge row shows:
 * - Drag handle (edit mode only)
 * - 48px icon
 * - Badge name (16px semibold)
 * - Context text (14px muted) - tournament name or description
 * - Earned date (12px subtle)
 */
export function TrophyCase({ badges, style, editable = false, onOrderChange }: TrophyCaseProps) {
  const [isEditMode, setIsEditMode] = useState(false);
  const [orderedBadges, setOrderedBadges] = useState<UserBadgeDto[]>(badges);
  const [dragInfo, setDragInfo] = useState<DragInfo | null>(null);
  const [hoverIndex, setHoverIndex] = useState(-1);
  const containerRef = useRef<View>(null);
  const [containerTop, setContainerTop] = useState(0);

  // Sync orderedBadges when badges prop changes
  React.useEffect(() => {
    setOrderedBadges(badges);
  }, [badges]);

  // Shared value for drag translation
  const translateY = useSharedValue(0);

  const handleContainerLayout = useCallback((event: LayoutChangeEvent) => {
    containerRef.current?.measureInWindow((x, y) => {
      setContainerTop(y);
    });
  }, []);

  const handleToggleEditMode = useCallback(() => {
    setIsEditMode(prev => !prev);
    // Reset drag state when exiting edit mode
    if (isEditMode) {
      setDragInfo(null);
      setHoverIndex(-1);
      translateY.value = 0;
    }
  }, [isEditMode, translateY]);

  const startDrag = useCallback((badge: UserBadgeDto, index: number) => {
    const startY = index * ROW_HEIGHT;
    setDragInfo({ badge, index, startY });
  }, []);

  const handleDragMove = useCallback((absoluteY: number) => {
    const relativeY = absoluteY - containerTop;
    const targetIndex = Math.floor(relativeY / ROW_HEIGHT);
    const clampedIndex = Math.max(0, Math.min(targetIndex, orderedBadges.length - 1));
    setHoverIndex(clampedIndex);
  }, [containerTop, orderedBadges.length]);

  const handleDragEnd = useCallback(() => {
    if (!dragInfo || hoverIndex < 0) {
      setDragInfo(null);
      setHoverIndex(-1);
      translateY.value = 0;
      return;
    }

    const sourceIndex = dragInfo.index;
    const targetIndex = hoverIndex;

    if (sourceIndex !== targetIndex) {
      // Reorder the badges
      const newOrder = [...orderedBadges];
      const [removed] = newOrder.splice(sourceIndex, 1);
      newOrder.splice(targetIndex, 0, removed);
      setOrderedBadges(newOrder);

      // Notify parent of order change
      const badgeIds = newOrder.map(b => b.id);
      onOrderChange?.(badgeIds);
    }

    setDragInfo(null);
    setHoverIndex(-1);
    translateY.value = 0;
  }, [dragInfo, hoverIndex, orderedBadges, onOrderChange, translateY]);

  const handleDragCancel = useCallback(() => {
    setDragInfo(null);
    setHoverIndex(-1);
    translateY.value = 0;
  }, [translateY]);

  // Pan gesture for drag-and-drop
  const panGesture = useMemo(() =>
    Gesture.Pan()
      .onUpdate((event) => {
        'worklet';
        if (dragInfo) {
          translateY.value = event.translationY;
          runOnJS(handleDragMove)(event.absoluteY);
        }
      })
      .onEnd(() => {
        'worklet';
        if (dragInfo) {
          runOnJS(handleDragEnd)();
        }
      })
      .onFinalize(() => {
        'worklet';
        runOnJS(handleDragCancel)();
      }),
    [dragInfo, handleDragMove, handleDragEnd, handleDragCancel, translateY]
  );

  // Empty state
  if (!badges || badges.length === 0) {
    return (
      <View style={[styles.container, styles.emptyContainer, style]}>
        <Text style={styles.emptyText}>No badges earned yet</Text>
      </View>
    );
  }

  // Read-only mode (non-editable)
  if (!editable) {
    return (
      <View style={[styles.container, style]}>
        {orderedBadges.map((badge) => (
          <BadgeRow
            key={badge.id}
            badge={badge}
            isEditMode={false}
            onLongPress={() => {}}
            isDragging={false}
          />
        ))}
      </View>
    );
  }

  // Editable mode
  return (
    <View style={[styles.container, style]}>
      {/* Edit/Done toggle button */}
      <TouchableOpacity
        style={styles.editButton}
        onPress={handleToggleEditMode}
      >
        <Text style={styles.editButtonText}>
          {isEditMode ? 'Done' : 'Edit'}
        </Text>
      </TouchableOpacity>

      {/* Badge list with gesture handler */}
      <GestureHandlerRootView style={styles.gestureRoot}>
        <GestureDetector gesture={panGesture}>
          <View
            ref={containerRef}
            style={styles.badgeList}
            onLayout={handleContainerLayout}
          >
            {orderedBadges.map((badge, index) => {
              const isDragging = dragInfo?.badge.id === badge.id;
              const isHoverTarget = hoverIndex === index && dragInfo && dragInfo.index !== index;

              return (
                <View key={badge.id}>
                  {/* Drop indicator line above this row */}
                  {isHoverTarget && dragInfo.index > index && (
                    <View style={styles.dropIndicator} />
                  )}
                  <View style={[isDragging && styles.badgeRowHidden]}>
                    <BadgeRow
                      badge={badge}
                      isEditMode={isEditMode}
                      onLongPress={() => startDrag(badge, index)}
                      isDragging={isDragging}
                    />
                  </View>
                  {/* Drop indicator line below this row */}
                  {isHoverTarget && dragInfo.index < index && (
                    <View style={styles.dropIndicator} />
                  )}
                </View>
              );
            })}

            {/* Drag overlay */}
            {dragInfo && (
              <DragOverlay dragInfo={dragInfo} translateY={translateY} />
            )}
          </View>
        </GestureDetector>
      </GestureHandlerRootView>

      {/* Hint text in edit mode */}
      {isEditMode && (
        <Text style={styles.hint}>Hold and drag to reorder</Text>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: spacing.sm,
  },
  emptyContainer: {
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.lg,
    alignItems: 'center',
  },
  emptyText: {
    fontSize: 14,
    color: colors.text.muted,
  },
  editButton: {
    alignSelf: 'flex-end',
    paddingVertical: spacing.xs,
    paddingHorizontal: spacing.sm,
  },
  editButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.primary.teal,
  },
  gestureRoot: {
    flex: 1,
  },
  badgeList: {
    position: 'relative',
    gap: spacing.md,
  },
  badgeRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.sm,
    minHeight: ROW_HEIGHT - spacing.md, // Account for gap
  },
  badgeRowDragging: {
    opacity: 0.5,
  },
  badgeRowHidden: {
    opacity: 0.3,
  },
  badgeRowOverlay: {
    backgroundColor: colors.bg.dark,
    borderWidth: 2,
    borderColor: colors.primary.teal,
    elevation: 10,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
  },
  dragHandle: {
    width: 20,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.xs,
    gap: 3,
  },
  dragHandleLine: {
    width: 14,
    height: 2,
    backgroundColor: colors.text.subtle,
    borderRadius: 1,
  },
  icon: {
    marginRight: spacing.sm,
    marginTop: 2,
  },
  badgeInfo: {
    flex: 1,
  },
  badgeName: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  badgeContext: {
    fontSize: 14,
    color: colors.text.muted,
    marginTop: 2,
  },
  badgeDate: {
    fontSize: 12,
    color: colors.text.subtle,
    marginTop: 2,
  },
  dragOverlay: {
    position: 'absolute',
    left: 0,
    right: 0,
    zIndex: 1000,
  },
  dropIndicator: {
    height: 3,
    backgroundColor: colors.primary.teal,
    borderRadius: 2,
    marginVertical: spacing.xs,
  },
  hint: {
    fontSize: 12,
    color: colors.text.subtle,
    textAlign: 'center',
    marginTop: spacing.sm,
  },
});
