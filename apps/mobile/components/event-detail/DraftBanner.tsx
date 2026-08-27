import { View, Text, StyleSheet } from 'react-native';
import { colors, spacing, radius } from '../../theme';

/**
 * Shown to managers on both the Info and Roster tabs while an event is a
 * draft - the roster is where they build the game, so the reminder that
 * nobody else can see it yet belongs there too.
 */
export function DraftBanner() {
  return (
    <View style={styles.draftBanner}>
      <Text style={styles.draftBannerTitle} allowFontScaling={false}>
        Draft - not visible to members
      </Text>
      <Text style={styles.draftBannerText} allowFontScaling={false}>
        Add players now if you want. Publishing notifies members and opens signups.
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  draftBanner: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginBottom: spacing.md,
    borderWidth: 1,
    borderColor: colors.status.warning,
  },
  draftBannerTitle: {
    fontSize: 15,
    fontWeight: '700',
    color: colors.status.warning,
  },
  draftBannerText: {
    fontSize: 13,
    color: colors.text.secondary,
    marginTop: spacing.xs,
  },
});
