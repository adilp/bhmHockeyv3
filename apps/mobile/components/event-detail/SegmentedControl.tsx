import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { colors, spacing, radius } from '../../theme';

export type TabKey = 'info' | 'roster' | 'chat' | 'results';

interface SegmentedControlProps {
  selectedTab: TabKey;
  onTabChange: (tab: TabKey) => void;
  /** Which segments to show, in order. Default: Info, Roster, Chat (unchanged). */
  tabs?: { key: TabKey; label: string }[];
}

export const DEFAULT_TABS: { key: TabKey; label: string }[] = [
  { key: 'info', label: 'Info' },
  { key: 'roster', label: 'Roster' },
  { key: 'chat', label: 'Chat' },
];

export function SegmentedControl({ selectedTab, onTabChange, tabs = DEFAULT_TABS }: SegmentedControlProps) {
  return (
    <View style={styles.container}>
      <View style={styles.tabsContainer}>
        {tabs.map((tab) => {
          const isSelected = selectedTab === tab.key;
          return (
            <TouchableOpacity
              key={tab.key}
              style={[styles.tab, isSelected && styles.tabSelected]}
              onPress={() => onTabChange(tab.key)}
              activeOpacity={0.7}
            >
              <Text
                style={[styles.tabText, isSelected && styles.tabTextSelected]}
                allowFontScaling={false}
              >
                {tab.label}
              </Text>
            </TouchableOpacity>
          );
        })}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    backgroundColor: colors.bg.dark,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: colors.border.default,
  },
  tabsContainer: {
    flexDirection: 'row',
    backgroundColor: colors.bg.elevated,
    borderRadius: radius.md,
    padding: spacing.xs,
  },
  tab: {
    flex: 1,
    paddingVertical: spacing.sm,
    alignItems: 'center',
    borderRadius: radius.sm,
  },
  tabSelected: {
    backgroundColor: colors.primary.teal,
  },
  tabText: {
    fontSize: 14,
    fontWeight: '600',
    color: colors.text.muted,
  },
  tabTextSelected: {
    color: colors.bg.darkest,
  },
});
