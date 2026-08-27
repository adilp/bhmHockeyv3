import { Platform } from 'react-native';
import { colors } from '../theme';

/**
 * Dark-mode props for @react-native-community/datetimepicker.
 *
 * The picker is a native control: without being told otherwise it follows the
 * device appearance, so on a light-mode phone it renders light chrome inside
 * this app's dark UI. iOS takes themeVariant/textColor directly; Android's
 * dialog follows the app theme instead, which app.json's
 * userInterfaceStyle: "dark" pins for us.
 */
export const datePickerProps = Platform.select({
  ios: { themeVariant: 'dark' as const, textColor: colors.text.primary },
  android: {},
}) ?? {};
