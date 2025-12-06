import { View, Text, StyleSheet, ViewStyle, TextStyle } from 'react-native';
import { colors, radius } from '../theme';

export type BadgeVariant =
  | 'default'
  | 'teal'
  | 'green'
  | 'purple'
  | 'warning'
  | 'error';

interface BadgeProps {
  children: React.ReactNode;
  variant?: BadgeVariant;
  style?: ViewStyle;
  textStyle?: TextStyle;
}

const variantStyles: Record<BadgeVariant, { bg: string; text: string }> = {
  default: { bg: colors.bg.hover, text: colors.text.muted },
  teal: { bg: colors.subtle.teal, text: colors.primary.teal },
  green: { bg: colors.subtle.green, text: colors.primary.green },
  purple: { bg: colors.subtle.purple, text: colors.primary.purple },
  warning: { bg: colors.status.warningSubtle, text: colors.status.warning },
  error: { bg: colors.status.errorSubtle, text: colors.status.error },
};

export function Badge({ children, variant = 'default', style, textStyle }: BadgeProps) {
  const variantStyle = variantStyles[variant];

  return (
    <View style={[styles.badge, { backgroundColor: variantStyle.bg }, style]}>
      <Text style={[styles.badgeText, { color: variantStyle.text }, textStyle]}>
        {children}
      </Text>
    </View>
  );
}

// Position badge (G, D, F, C) - more compact styling
export type Position = 'G' | 'D' | 'F' | 'C';

const positionColors: Record<Position, string> = {
  G: colors.primary.teal,
  D: colors.primary.blue,
  F: colors.primary.green,
  C: colors.primary.purple,
};

interface PositionBadgeProps {
  position: Position;
  style?: ViewStyle;
}

export function PositionBadge({ position, style }: PositionBadgeProps) {
  return (
    <View style={[styles.positionBadge, { backgroundColor: positionColors[position] }, style]}>
      <Text style={styles.positionBadgeText}>{position}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  badge: {
    paddingHorizontal: 10,
    paddingVertical: 4,
    borderRadius: radius.sm,
  },
  badgeText: {
    fontSize: 12,
    fontWeight: '600',
  },
  positionBadge: {
    minWidth: 24,
    height: 18,
    paddingHorizontal: 4,
    borderRadius: radius.sm,
    alignItems: 'center',
    justifyContent: 'center',
  },
  positionBadgeText: {
    fontSize: 9,
    fontWeight: '700',
    color: colors.bg.darkest,
    textTransform: 'uppercase',
  },
});
