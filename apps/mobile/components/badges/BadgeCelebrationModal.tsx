import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TouchableWithoutFeedback,
} from 'react-native';
import Animated, {
  useSharedValue,
  useAnimatedStyle,
  withTiming,
  withSpring,
  withRepeat,
  Easing,
} from 'react-native-reanimated';
import type { UncelebratedBadgeDto } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../theme';
import { BadgeIcon } from './BadgeIcon';
import { Confetti } from './Confetti';

interface BadgeCelebrationModalProps {
  badge: UncelebratedBadgeDto;
  remaining: number;  // How many badges after this one
  onDismiss: () => void;
  onViewTrophyCase: () => void;
}

/**
 * Gets display text for badge rarity based on total awarded count
 */
const getRarityText = (count: number): string => {
  if (count === 1) return "You're the first to earn this badge!";
  if (count <= 10) return `Only ${count} players have this badge`;
  if (count <= 50) return `${count} players have earned this badge`;
  return `Join ${count} players with this badge`;
};

/**
 * Extracts context text from badge context JSON for display
 * Handles tournament badges, event badges, or falls back to generic description
 */
const getContextText = (context?: Record<string, unknown>): string | null => {
  if (!context) return null;

  // Tournament context
  if (context.tournamentName && typeof context.tournamentName === 'string') {
    return context.tournamentName;
  }

  // Event context
  if (context.eventName && typeof context.eventName === 'string') {
    return context.eventName;
  }

  // Generic description
  if (context.description && typeof context.description === 'string') {
    return context.description;
  }

  return null;
};

/**
 * BadgeCelebrationModal - Displays celebratory modal when user earns a badge
 *
 * Features:
 * - Dark backdrop (rgba(0,0,0,0.85))
 * - Badge icon 96x96 with golden pulsing glow animation
 * - Badge details: name, context text, rarity
 * - Confetti burst animation
 * - Primary "Got it!" button and "View Trophy Case" text button
 * - Shows counter if multiple badges in queue
 */
export function BadgeCelebrationModal({
  badge,
  remaining,
  onDismiss,
  onViewTrophyCase,
}: BadgeCelebrationModalProps) {
  // State for controlling confetti
  const [showConfetti, setShowConfetti] = useState(false);

  // Animated values
  const backdropOpacity = useSharedValue(0);
  const badgeScale = useSharedValue(0);
  const glowOpacity = useSharedValue(0.3);

  // Extract context text
  const contextText = getContextText(badge.context);
  const rarityText = getRarityText(badge.totalAwarded);

  // Animation sequence on mount
  useEffect(() => {
    // 1. Modal backdrop fades in
    backdropOpacity.value = withTiming(1, { duration: 200 });

    // 2. Fire confetti
    setShowConfetti(true);

    // 3. Badge icon scales up with spring
    badgeScale.value = withSpring(1, {
      damping: 12,
      stiffness: 100,
    });

    // 4. After confetti settles (5s), start pulsing glow
    const glowTimeout = setTimeout(() => {
      glowOpacity.value = withRepeat(
        withTiming(0.8, { duration: 1000, easing: Easing.inOut(Easing.ease) }),
        -1,  // Infinite repeat
        true  // Reverse (0.3 -> 0.8 -> 0.3)
      );
    }, 5000);

    return () => {
      clearTimeout(glowTimeout);
    };
  }, [badge.id]);

  // Handle confetti completion (no-op, just for hook)
  const handleConfettiComplete = () => {
    // Confetti finished - glow animation started in useEffect timeout
  };

  // Animated styles
  const backdropStyle = useAnimatedStyle(() => ({
    opacity: backdropOpacity.value,
  }));

  const badgeContainerStyle = useAnimatedStyle(() => ({
    transform: [{ scale: badgeScale.value }],
  }));

  const glowStyle = useAnimatedStyle(() => ({
    opacity: glowOpacity.value,
  }));

  return (
    <Modal
      visible={true}
      transparent
      animationType="none"
      onRequestClose={onDismiss}
    >
      {/* Confetti layer */}
      <Confetti isActive={showConfetti} onComplete={handleConfettiComplete} />

      {/* Dark backdrop */}
      <TouchableWithoutFeedback onPress={onDismiss}>
        <Animated.View style={[styles.backdrop, backdropStyle]}>
          <TouchableWithoutFeedback>
            <View style={styles.modal}>
              {/* Badge icon with glow */}
              <Animated.View style={[styles.badgeContainer, badgeContainerStyle]}>
                {/* Glow effect */}
                <Animated.View style={[styles.glow, glowStyle]} />
                {/* Badge icon */}
                <BadgeIcon iconName={badge.badgeType.iconName} size={96} />
              </Animated.View>

              {/* "Badge Earned!" header */}
              <Text style={styles.header} allowFontScaling={false}>
                BADGE EARNED!
              </Text>

              {/* Badge name */}
              <Text style={styles.badgeName} allowFontScaling={false}>
                {badge.badgeType.name}
              </Text>

              {/* Context text (if available) */}
              {contextText && (
                <Text style={styles.contextText} allowFontScaling={false}>
                  {contextText}
                </Text>
              )}

              {/* Rarity text */}
              <Text style={styles.rarityText} allowFontScaling={false}>
                {rarityText}
              </Text>

              {/* Got it! button */}
              <TouchableOpacity style={styles.primaryButton} onPress={onDismiss}>
                <Text style={styles.primaryButtonText} allowFontScaling={false}>
                  Got it!
                </Text>
              </TouchableOpacity>

              {/* View Trophy Case button */}
              <TouchableOpacity style={styles.textButton} onPress={onViewTrophyCase}>
                <Text style={styles.textButtonText} allowFontScaling={false}>
                  View Trophy Case →
                </Text>
              </TouchableOpacity>

              {/* Counter (if multiple badges) */}
              {remaining > 0 && (
                <Text style={styles.counter} allowFontScaling={false}>
                  ({remaining === 1 ? '1 more badge' : `${remaining} more badges`})
                </Text>
              )}
            </View>
          </TouchableWithoutFeedback>
        </Animated.View>
      </TouchableWithoutFeedback>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.85)',
    justifyContent: 'center',
    alignItems: 'center',
    padding: spacing.lg,
  },
  modal: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.xl,
    padding: spacing.xl,
    alignItems: 'center',
    maxWidth: 400,
    width: '100%',
  },
  badgeContainer: {
    position: 'relative',
    width: 96,
    height: 96,
    marginBottom: spacing.lg,
    alignItems: 'center',
    justifyContent: 'center',
  },
  glow: {
    position: 'absolute',
    width: 120,
    height: 120,
    borderRadius: 60,
    backgroundColor: 'rgba(255, 215, 0, 1)',
    shadowColor: '#FFD700',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 1,
    shadowRadius: 20,
    elevation: 10,
  },
  header: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 1,
    marginBottom: spacing.sm,
  },
  badgeName: {
    fontSize: 24,
    fontWeight: '700',
    color: colors.text.primary,
    textAlign: 'center',
    marginBottom: spacing.sm,
  },
  contextText: {
    fontSize: 16,
    color: colors.text.secondary,
    textAlign: 'center',
    marginBottom: spacing.md,
  },
  rarityText: {
    fontSize: 14,
    color: colors.primary.teal,
    textAlign: 'center',
    marginBottom: spacing.lg,
  },
  primaryButton: {
    backgroundColor: colors.primary.teal,
    paddingVertical: spacing.md,
    paddingHorizontal: spacing.xl,
    borderRadius: radius.md,
    width: '100%',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  primaryButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: colors.text.primary,
  },
  textButton: {
    paddingVertical: spacing.sm,
    paddingHorizontal: spacing.md,
  },
  textButtonText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.muted,
  },
  counter: {
    fontSize: 12,
    color: colors.text.subtle,
    marginTop: spacing.md,
  },
});
