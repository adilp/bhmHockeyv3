import React, { useState, useCallback, useRef, useMemo } from 'react';
import {
  View,
  Text,
  StyleSheet,
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
import type { EventRegistrationDto, PaymentStatus, WaitlistOrderItem } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../theme';

const ROW_HEIGHT = 56; // Height of each waitlist row

interface DraggableWaitlistProps {
  /** Waitlist registrations sorted by position */
  waitlist: EventRegistrationDto[];
  /** Whether current user can manage (reorder) the waitlist */
  canManage: boolean;
  /** Callback when a waitlist item is tapped */
  onItemPress: (registration: EventRegistrationDto) => void;
  /** Callback when waitlist order changes */
  onReorder: (items: WaitlistOrderItem[]) => Promise<void>;
}

interface DragInfo {
  registration: EventRegistrationDto;
  index: number;
  startY: number;
}

// Payment status badge for waitlist items
const PaymentBadge: React.FC<{ status: PaymentStatus | null | undefined }> = ({ status }) => {
  const config: Record<string, { label: string; color: string; bg: string }> = {
    Verified: { label: 'Paid', color: colors.status.success, bg: colors.status.successSubtle },
    MarkedPaid: { label: 'Pending', color: colors.status.warning, bg: colors.status.warningSubtle },
    Pending: { label: 'Unpaid', color: colors.status.error, bg: colors.status.errorSubtle },
  };

  const { label, color, bg } = config[status ?? 'Pending'] ?? config.Pending;

  return (
    <View style={[styles.paymentBadge, { backgroundColor: bg }]}>
      <Text style={[styles.paymentBadgeText, { color }]} allowFontScaling={false}>
        {label}
      </Text>
    </View>
  );
};

// Drag handle component
function DragHandle() {
  return (
    <View style={styles.dragHandle}>
      <View style={styles.dragHandleLine} />
      <View style={styles.dragHandleLine} />
      <View style={styles.dragHandleLine} />
    </View>
  );
}

// Waitlist row component
function WaitlistRow({
  registration,
  position,
  showDragHandle,
  onPress,
  onLongPress,
  isDragging,
}: {
  registration: EventRegistrationDto;
  position: number;
  showDragHandle: boolean;
  onPress: () => void;
  onLongPress: () => void;
  isDragging: boolean;
}) {
  return (
    <TouchableOpacity
      style={[styles.waitlistRow, isDragging && styles.waitlistRowDragging]}
      onPress={onPress}
      onLongPress={showDragHandle ? onLongPress : undefined}
      delayLongPress={200}
      activeOpacity={0.7}
    >
      {showDragHandle && <DragHandle />}
      <View style={styles.waitlistPositionBadge}>
        <Text style={styles.waitlistPositionText} allowFontScaling={false}>
          #{position}
        </Text>
      </View>
      <View style={styles.waitlistUserInfo}>
        <Text style={styles.waitlistUserName} allowFontScaling={false}>
          {registration.user.firstName} {registration.user.lastName}
        </Text>
        <Text style={styles.waitlistUserMeta} allowFontScaling={false}>
          {registration.registeredPosition || 'Skater'}
        </Text>
      </View>
      <PaymentBadge status={registration.paymentStatus} />
    </TouchableOpacity>
  );
}

// Drag overlay - follows finger during drag
function DragOverlay({
  dragInfo,
  position,
  translateY,
}: {
  dragInfo: DragInfo;
  position: number;
  translateY: SharedValue<number>;
}) {
  const { registration } = dragInfo;

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
      <View style={[styles.waitlistRow, styles.waitlistRowOverlay]}>
        <DragHandle />
        <View style={styles.waitlistPositionBadge}>
          <Text style={styles.waitlistPositionText} allowFontScaling={false}>
            #{position}
          </Text>
        </View>
        <View style={styles.waitlistUserInfo}>
          <Text style={styles.waitlistUserName} allowFontScaling={false}>
            {registration.user.firstName} {registration.user.lastName}
          </Text>
          <Text style={styles.waitlistUserMeta} allowFontScaling={false}>
            {registration.registeredPosition || 'Skater'}
          </Text>
        </View>
        <PaymentBadge status={registration.paymentStatus} />
      </View>
    </Animated.View>
  );
}

export function DraggableWaitlist({
  waitlist,
  canManage,
  onItemPress,
  onReorder,
}: DraggableWaitlistProps) {
  const [orderedWaitlist, setOrderedWaitlist] = useState<EventRegistrationDto[]>(waitlist);
  const [dragInfo, setDragInfo] = useState<DragInfo | null>(null);
  const [hoverIndex, setHoverIndex] = useState(-1);
  const containerRef = useRef<View>(null);
  const [containerTop, setContainerTop] = useState(0);

  // Sync orderedWaitlist when waitlist prop changes
  React.useEffect(() => {
    setOrderedWaitlist(waitlist);
  }, [waitlist]);

  // Shared value for drag translation
  const translateY = useSharedValue(0);

  const handleContainerLayout = useCallback((event: LayoutChangeEvent) => {
    containerRef.current?.measureInWindow((x, y) => {
      setContainerTop(y);
    });
  }, []);

  const startDrag = useCallback((registration: EventRegistrationDto, index: number) => {
    const startY = index * ROW_HEIGHT;
    setDragInfo({ registration, index, startY });
  }, []);

  // Determine which row is being touched based on Y position
  const getRowIndexFromY = useCallback((absoluteY: number) => {
    const relativeY = absoluteY - containerTop;
    const index = Math.floor(relativeY / ROW_HEIGHT);
    return Math.max(0, Math.min(index, orderedWaitlist.length - 1));
  }, [containerTop, orderedWaitlist.length]);

  const handleDragMove = useCallback((absoluteY: number) => {
    const relativeY = absoluteY - containerTop;
    const targetIndex = Math.floor(relativeY / ROW_HEIGHT);
    const clampedIndex = Math.max(0, Math.min(targetIndex, orderedWaitlist.length - 1));
    setHoverIndex(clampedIndex);
  }, [containerTop, orderedWaitlist.length]);

  const handleDragEnd = useCallback(async () => {
    if (!dragInfo || hoverIndex < 0) {
      setDragInfo(null);
      setHoverIndex(-1);
      translateY.value = 0;
      return;
    }

    const sourceIndex = dragInfo.index;
    const targetIndex = hoverIndex;

    if (sourceIndex !== targetIndex) {
      // Reorder the waitlist optimistically
      const newOrder = [...orderedWaitlist];
      const [removed] = newOrder.splice(sourceIndex, 1);
      newOrder.splice(targetIndex, 0, removed);
      setOrderedWaitlist(newOrder);

      // Build items for API call
      const items: WaitlistOrderItem[] = newOrder.map((reg, index) => ({
        registrationId: reg.id,
        position: index + 1,
      }));

      // Persist to backend
      try {
        await onReorder(items);
      } catch (error) {
        // Revert on failure - parent will show error
        setOrderedWaitlist(waitlist);
      }
    }

    setDragInfo(null);
    setHoverIndex(-1);
    translateY.value = 0;
  }, [dragInfo, hoverIndex, orderedWaitlist, onReorder, translateY, waitlist]);

  const handleDragCancel = useCallback(() => {
    setDragInfo(null);
    setHoverIndex(-1);
    translateY.value = 0;
  }, [translateY]);

  // Start drag from gesture position
  const handleDragStart = useCallback((absoluteY: number) => {
    if (!canManage) return;
    const index = getRowIndexFromY(absoluteY);
    const registration = orderedWaitlist[index];
    if (registration) {
      startDrag(registration, index);
    }
  }, [canManage, getRowIndexFromY, orderedWaitlist, startDrag]);

  // Track if drag was started (shared value for worklet access)
  const isDragging = useSharedValue(false);

  // Sync isDragging shared value with dragInfo state
  React.useEffect(() => {
    isDragging.value = dragInfo !== null;
  }, [dragInfo, isDragging]);

  // Pan gesture for drag-and-drop
  // activateAfterLongPress allows taps to pass through to TouchableOpacity onPress
  const panGesture = useMemo(() =>
    Gesture.Pan()
      .activateAfterLongPress(200)
      .onStart((event) => {
        'worklet';
        runOnJS(handleDragStart)(event.absoluteY);
      })
      .onUpdate((event) => {
        'worklet';
        if (isDragging.value) {
          translateY.value = event.translationY;
          runOnJS(handleDragMove)(event.absoluteY);
        }
      })
      .onEnd(() => {
        'worklet';
        if (isDragging.value) {
          runOnJS(handleDragEnd)();
        }
      })
      .onFinalize(() => {
        'worklet';
        runOnJS(handleDragCancel)();
      }),
    [handleDragStart, handleDragMove, handleDragEnd, handleDragCancel, translateY, isDragging]
  );

  // Empty state
  if (!waitlist || waitlist.length === 0) {
    return null;
  }

  // Calculate the displayed position for the dragging item
  const getDraggedPosition = () => {
    if (!dragInfo) return 1;
    return hoverIndex >= 0 ? hoverIndex + 1 : dragInfo.index + 1;
  };

  return (
    <GestureHandlerRootView style={styles.gestureRoot}>
      <GestureDetector gesture={panGesture}>
        <View
          ref={containerRef}
          style={styles.container}
          onLayout={handleContainerLayout}
        >
          {orderedWaitlist.map((registration, index) => {
            const isDragging = dragInfo?.registration.id === registration.id;
            const isHoverTarget = hoverIndex === index && dragInfo && dragInfo.index !== index;

            // Calculate display position based on hover state
            let displayPosition = index + 1;
            if (dragInfo) {
              if (isDragging) {
                // The dragged item shows target position
                displayPosition = hoverIndex >= 0 ? hoverIndex + 1 : index + 1;
              } else if (hoverIndex >= 0) {
                // Adjust other items' positions based on drag direction
                const sourceIdx = dragInfo.index;
                if (sourceIdx < hoverIndex) {
                  // Dragging down: items between source and hover shift up
                  if (index > sourceIdx && index <= hoverIndex) {
                    displayPosition = index;
                  }
                } else if (sourceIdx > hoverIndex) {
                  // Dragging up: items between hover and source shift down
                  if (index >= hoverIndex && index < sourceIdx) {
                    displayPosition = index + 2;
                  }
                }
              }
            }

            return (
              <View key={registration.id}>
                {/* Drop indicator line above this row */}
                {isHoverTarget && dragInfo.index > index && (
                  <View style={styles.dropIndicator} />
                )}
                <View style={[isDragging && styles.waitlistRowHidden]}>
                  <WaitlistRow
                    registration={registration}
                    position={displayPosition}
                    showDragHandle={canManage}
                    onPress={() => onItemPress(registration)}
                    onLongPress={() => startDrag(registration, index)}
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
            <DragOverlay
              dragInfo={dragInfo}
              position={getDraggedPosition()}
              translateY={translateY}
            />
          )}
        </View>
      </GestureDetector>
    </GestureHandlerRootView>
  );
}

const styles = StyleSheet.create({
  gestureRoot: {
    flex: 1,
  },
  container: {
    position: 'relative',
  },
  waitlistRow: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.xs,
    height: ROW_HEIGHT,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  waitlistRowDragging: {
    opacity: 0.5,
  },
  waitlistRowHidden: {
    opacity: 0.3,
  },
  waitlistRowOverlay: {
    backgroundColor: colors.bg.dark,
    borderWidth: 2,
    borderColor: colors.primary.teal,
    borderRadius: radius.sm,
    elevation: 10,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
  },
  dragHandle: {
    width: 24,
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
  waitlistPositionBadge: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: colors.status.warningSubtle,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: spacing.sm,
  },
  waitlistPositionText: {
    color: colors.status.warning,
    fontWeight: '700',
    fontSize: 12,
  },
  waitlistUserInfo: {
    flex: 1,
  },
  waitlistUserName: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  waitlistUserMeta: {
    fontSize: 13,
    color: colors.text.muted,
    marginTop: 2,
  },
  paymentBadge: {
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.xs,
    borderRadius: radius.sm,
    marginLeft: spacing.sm,
  },
  paymentBadgeText: {
    fontSize: 11,
    fontWeight: '600',
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
    marginVertical: 2,
  },
});
