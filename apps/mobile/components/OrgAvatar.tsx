import { View, Text, StyleSheet } from 'react-native';
import { colors, spacing, radius } from '../theme';

interface OrgAvatarProps {
  name: string;
  size?: 'small' | 'medium' | 'large';
}

// Predefined color palette for org avatars
// Colors are chosen to work well on dark backgrounds
const avatarColors = [
  '#00D9C0', // teal
  '#3FB950', // green
  '#A371F7', // purple
  '#58A6FF', // blue
  '#F78166', // coral/orange
  '#D29922', // gold/yellow
  '#DB61A2', // pink
  '#79C0FF', // light blue
  '#7EE787', // light green
  '#E6B450', // amber
];

/**
 * Generate a consistent color based on org name
 * Same name will always get the same color
 */
function getColorForName(name: string): string {
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  const index = Math.abs(hash) % avatarColors.length;
  return avatarColors[index];
}

/**
 * Get initials from organization name
 * "Central Hockey Club" -> "CH"
 * "CHC" -> "CH" (first two chars if already short)
 * "Saturday Pickup" -> "SP"
 */
function getInitials(name: string): string {
  const words = name.trim().split(/\s+/);

  if (words.length === 1) {
    // Single word - take first 2 characters
    return name.substring(0, 2).toUpperCase();
  }

  // Multiple words - take first letter of first two words
  return (words[0][0] + words[1][0]).toUpperCase();
}

const sizes = {
  small: { container: 28, fontSize: 11 },
  medium: { container: 36, fontSize: 14 },
  large: { container: 48, fontSize: 18 },
};

/**
 * Organization avatar showing initials on a colored background
 * Color is deterministic based on org name for consistency
 */
export function OrgAvatar({ name, size = 'small' }: OrgAvatarProps) {
  const backgroundColor = getColorForName(name);
  const initials = getInitials(name);
  const sizeConfig = sizes[size];

  return (
    <View
      style={[
        styles.container,
        {
          backgroundColor,
          width: sizeConfig.container,
          height: sizeConfig.container,
          borderRadius: sizeConfig.container / 2,
        },
      ]}
    >
      <Text
        style={[
          styles.initials,
          { fontSize: sizeConfig.fontSize },
        ]}
      >
        {initials}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    justifyContent: 'center',
    alignItems: 'center',
  },
  initials: {
    color: colors.bg.darkest,
    fontWeight: '700',
  },
});
