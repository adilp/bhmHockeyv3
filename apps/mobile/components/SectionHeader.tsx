import { View, Text, StyleSheet, TouchableOpacity } from 'react-native';
import { colors, spacing, radius } from '../theme';

interface SectionHeaderProps {
  title: string;
  count?: number;
  action?: string;
  onActionPress?: () => void;
}

export function SectionHeader({ title, count, action, onActionPress }: SectionHeaderProps) {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>{title}</Text>
      {count !== undefined && (
        <View style={styles.countBadge}>
          <Text style={styles.countText}>{count}</Text>
        </View>
      )}
      {action && onActionPress && (
        <TouchableOpacity onPress={onActionPress} style={styles.actionButton}>
          <Text style={styles.actionText}>{action}</Text>
        </TouchableOpacity>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 14,
  },
  title: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.text.muted,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  countBadge: {
    marginLeft: 10,
    backgroundColor: colors.subtle.teal,
    paddingHorizontal: 10,
    paddingVertical: 3,
    borderRadius: radius.round,
  },
  countText: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.primary.teal,
  },
  actionButton: {
    marginLeft: 'auto',
  },
  actionText: {
    fontSize: 13,
    fontWeight: '600',
    color: colors.primary.teal,
  },
});
