import { View, Text, TextInput, StyleSheet, TextInputProps } from 'react-native';
import { colors, spacing, radius } from '../theme';

interface FormInputProps extends TextInputProps {
  label: string;
  hint?: string;
}

export function FormInput({ label, hint, style, ...props }: FormInputProps) {
  return (
    <View style={styles.field}>
      <Text style={styles.label} allowFontScaling={false}>{label}</Text>
      <TextInput
        style={[styles.input, style]}
        placeholderTextColor={colors.text.muted}
        allowFontScaling={false}
        {...props}
      />
      {hint && <Text style={styles.hint} allowFontScaling={false}>{hint}</Text>}
    </View>
  );
}

const styles = StyleSheet.create({
  field: {
    marginBottom: spacing.md,
  },
  label: {
    fontSize: 14,
    fontWeight: '600',
    marginBottom: spacing.sm,
    color: colors.text.secondary,
  },
  input: {
    backgroundColor: colors.bg.elevated,
    borderWidth: 1,
    borderColor: colors.border.default,
    borderRadius: radius.md,
    padding: spacing.md,
    fontSize: 16,
    color: colors.text.primary,
  },
  hint: {
    fontSize: 12,
    color: colors.text.muted,
    marginTop: spacing.xs,
  },
});
