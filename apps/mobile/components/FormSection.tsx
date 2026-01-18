import { View, Text, StyleSheet } from 'react-native';
import { colors, spacing, radius } from '../theme';

interface FormSectionProps {
  title: string;
  hint?: string;
  children: React.ReactNode;
}

export function FormSection({ title, hint, children }: FormSectionProps) {
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle} allowFontScaling={false}>{title}</Text>
      {hint && <Text style={styles.sectionHint} allowFontScaling={false}>{hint}</Text>}
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  section: {
    backgroundColor: colors.bg.dark,
    borderRadius: radius.lg,
    padding: spacing.md,
    marginBottom: spacing.md,
    borderWidth: 1,
    borderColor: colors.border.default,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: spacing.xs,
    color: colors.text.primary,
  },
  sectionHint: {
    fontSize: 14,
    color: colors.text.muted,
    marginBottom: spacing.md,
  },
});
