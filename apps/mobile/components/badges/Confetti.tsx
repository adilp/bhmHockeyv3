import React, { useEffect, useMemo } from 'react';
import { View, StyleSheet, Dimensions } from 'react-native';
import Animated, {
  useSharedValue,
  useAnimatedStyle,
  withTiming,
  Easing,
  runOnJS,
  SharedValue,
} from 'react-native-reanimated';

interface ConfettiProps {
  isActive: boolean;
  onComplete?: () => void;
}

const SCREEN_WIDTH = Dimensions.get('window').width;
const SCREEN_HEIGHT = Dimensions.get('window').height;

// BHM brand colors for confetti
const CONFETTI_COLORS = ['#008B8B', '#FFD700', '#FFFFFF', '#87CEEB'];

// Physics constants
const GRAVITY = 400; // pixels per second^2
const ANIMATION_DURATION = 5000; // 5 seconds
const FADE_START_TIME = 4000; // Start fading at 4 seconds
const PARTICLE_COUNT = 65; // 50-75 particles

interface Particle {
  id: number;
  startX: number;
  startY: number;
  velocityX: number;
  velocityY: number;
  rotation: number;
  rotationSpeed: number;
  color: string;
  size: number;
}

/**
 * Generates a random number between min and max
 */
const random = (min: number, max: number) => Math.random() * (max - min) + min;

/**
 * ConfettiParticle - Individual confetti piece with physics-based animation
 */
const ConfettiParticle: React.FC<{ particle: Particle; time: SharedValue<number> }> = ({
  particle,
  time,
}) => {
  const animatedStyle = useAnimatedStyle(() => {
    'worklet';
    const t = time.value / 1000; // Convert to seconds

    // Physics formula: y = startY + (velocity × time) + (0.5 × gravity × time²)
    const y = particle.startY + particle.velocityY * t + 0.5 * GRAVITY * t * t;
    const x = particle.startX + particle.velocityX * t;

    // Rotation increases linearly with time
    const rotation = particle.rotation + particle.rotationSpeed * t;

    // Fade out near the end (from 4s to 5s)
    let opacity = 1;
    if (time.value > FADE_START_TIME) {
      const fadeProgress = (time.value - FADE_START_TIME) / (ANIMATION_DURATION - FADE_START_TIME);
      opacity = 1 - fadeProgress;
    }

    return {
      transform: [
        { translateX: x },
        { translateY: y },
        { rotate: `${rotation}deg` },
      ] as const,
      opacity,
    };
  });

  return (
    <Animated.View
      style={[
        styles.particle,
        {
          backgroundColor: particle.color,
          width: particle.size,
          height: particle.size,
        },
        animatedStyle,
      ]}
    />
  );
};

/**
 * Confetti - Reusable confetti burst animation component
 *
 * Renders 50-75 animated particles with physics-based motion.
 * Single shared time value drives all particles for optimal performance.
 * Particles burst upward then fall with gravity over ~5 seconds.
 */
export function Confetti({ isActive, onComplete }: ConfettiProps) {
  // Single shared value for time drives all particles (performance optimization)
  const time = useSharedValue(0);

  // Generate particles once using useMemo - randomize position, velocity, rotation, color
  const particles = useMemo<Particle[]>(() => {
    return Array.from({ length: PARTICLE_COUNT }, (_, i) => ({
      id: i,
      // Start from center-ish area, spread across width
      startX: random(SCREEN_WIDTH * 0.2, SCREEN_WIDTH * 0.8),
      // Start from middle of screen
      startY: SCREEN_HEIGHT * 0.4,
      // Horizontal velocity - spread particles left and right
      velocityX: random(-100, 100),
      // Vertical velocity - burst upward (negative = up)
      velocityY: random(-400, -200),
      // Initial rotation
      rotation: random(0, 360),
      // Rotation speed (degrees per second)
      rotationSpeed: random(-180, 180),
      // Random color from BHM palette
      color: CONFETTI_COLORS[Math.floor(Math.random() * CONFETTI_COLORS.length)],
      // Particle size variation
      size: random(6, 12),
    }));
  }, []);

  useEffect(() => {
    if (isActive) {
      // Reset time to 0
      time.value = 0;

      // Animate time from 0 to ANIMATION_DURATION over 5 seconds
      time.value = withTiming(
        ANIMATION_DURATION,
        {
          duration: ANIMATION_DURATION,
          easing: Easing.linear,
        },
        (finished) => {
          // Call onComplete callback when animation finishes
          if (finished && onComplete) {
            runOnJS(onComplete)();
          }
        }
      );
    } else {
      // Reset when not active
      time.value = 0;
    }
  }, [isActive, time, onComplete]);

  // Don't render anything if not active
  if (!isActive) {
    return null;
  }

  return (
    <View style={styles.container} pointerEvents="none">
      {particles.map((particle) => (
        <ConfettiParticle key={particle.id} particle={particle} time={time} />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    // pointerEvents="none" allows touches to pass through
  },
  particle: {
    position: 'absolute',
    borderRadius: 2,
  },
});
