import React from 'react';
import { Image, ImageStyle, StyleProp } from 'react-native';

type BadgeSize = 16 | 24;

interface BadgeIconProps {
  iconName: string;
  size?: BadgeSize;
  style?: StyleProp<ImageStyle>;
}

// Icon map: iconName -> size -> require()
// React Native auto-selects @2x/@3x variants based on device density
const iconMap: Record<string, Record<BadgeSize, ReturnType<typeof require>>> = {
  trophy_gold: {
    16: require('../../assets/badges/trophy_gold_16.png'),
    24: require('../../assets/badges/trophy_gold_24.png'),
  },
  star_teal: {
    16: require('../../assets/badges/star_teal_16.png'),
    24: require('../../assets/badges/star_teal_24.png'),
  },
};

/**
 * BadgeIcon - Renders a badge icon based on iconName and size
 *
 * @param iconName - The badge type's iconName (e.g., 'trophy_gold', 'star_teal')
 * @param size - Icon size: 16 (for roster cards) or 24 (for trophy case)
 * @param style - Optional additional styles
 */
export function BadgeIcon({ iconName, size = 16, style }: BadgeIconProps) {
  const iconSources = iconMap[iconName];

  // Fallback if iconName not found - render nothing
  if (!iconSources) {
    return null;
  }

  const source = iconSources[size];

  return (
    <Image
      source={source}
      style={[{ width: size, height: size }, style]}
      resizeMode="contain"
    />
  );
}
