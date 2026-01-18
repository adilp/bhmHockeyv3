import { View, Text, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing, radius } from '../../theme';

interface BracketResetIndicatorProps {
  losersTeamName: string;
  isVisible: boolean;
}

export function BracketResetIndicator({ losersTeamName, isVisible }: BracketResetIndicatorProps) {
  if (!isVisible) {
    return null;
  }

  return (
    <View style={styles.container}>
      <Ionicons name="information-circle-outline" size={16} color={colors.status.info} />
      <Text style={styles.text} allowFontScaling={false}>
        If <Text style={styles.teamName} allowFontScaling={false}>{losersTeamName}</Text> wins, a bracket reset match will occur
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: colors.status.infoSubtle,
    borderWidth: 1,
    borderColor: colors.status.info,
    borderRadius: radius.md,
    paddingHorizontal: spacing.sm,
    paddingVertical: spacing.sm,
    marginVertical: spacing.sm,
  },
  text: {
    fontSize: 12,
    color: colors.text.secondary,
    marginLeft: spacing.xs,
    flex: 1,
  },
  teamName: {
    fontWeight: '600',
    color: colors.primary.teal,
  },
});
