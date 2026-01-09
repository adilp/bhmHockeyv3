import React from 'react';
import { Image, ImageStyle, StyleProp } from 'react-native';

interface BadgeIconProps {
  iconName: string;
  /** Render size in pixels (default: 24). Asset scales to fit. */
  size?: number;
  style?: StyleProp<ImageStyle>;
}

// Icon map: iconName -> 24px asset
// All badges only need a single 24px asset - it scales to any render size
// React Native auto-selects @2x/@3x variants based on device density
const iconMap: Record<string, ReturnType<typeof require>> = {
  trophy_gold: require('../../assets/badges/trophy_gold_24.png'),
  star_teal: require('../../assets/badges/star_teal_24.png'),
};

/**
 * BadgeIcon - Renders a badge icon based on iconName and size
 *
 * @param iconName - The badge type's iconName (e.g., 'trophy_gold', 'star_teal')
 * @param size - Render size in pixels (default: 24). Asset scales to fit any size.
 * @param style - Optional additional styles
 */
export function BadgeIcon({ iconName, size = 24, style }: BadgeIconProps) {
  const source = iconMap[iconName];

  // Fallback if iconName not found - render nothing
  if (!source) {
    return null;
  }

  return (
    <Image
      source={source}
      style={[{ width: size, height: size }, style]}
      resizeMode="contain"
    />
  );
}
