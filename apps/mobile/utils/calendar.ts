import { Platform, ActionSheetIOS, Alert, Linking } from 'react-native';
import { File, Paths } from 'expo-file-system';
import * as Sharing from 'expo-sharing';
import type { EventDto } from '@bhmhockey/shared';
import {
  buildIcs,
  calendarTitle,
  googleCalendarUrl,
  outlookCalendarUrl,
  type CalendarTarget,
} from './calendarFormat';

/**
 * Adds a game to the phone's calendar without a native calendar module:
 * Apple gets an .ics through the share sheet, Google and Outlook get
 * pre-filled web links. All JS, so it ships over the air.
 */

export type { CalendarTarget };

/** Filename-safe slug so the shared file reads as the game, not a guid */
function icsFileName(event: EventDto): string {
  const slug = calendarTitle(event)
    .replace(/[^a-z0-9]+/gi, '-')
    .replace(/^-|-$/g, '')
    .toLowerCase() || 'game';
  return `${slug}.ics`;
}

async function shareIcs(event: EventDto): Promise<void> {
  if (!(await Sharing.isAvailableAsync())) {
    throw new Error('Sharing is not available on this device');
  }
  // expo-file-system v19 API: the legacy helpers throw at runtime
  const file = new File(Paths.cache, icsFileName(event));
  file.create({ overwrite: true });
  file.write(buildIcs(event));
  await Sharing.shareAsync(file.uri, {
    UTI: 'com.apple.ical.ics',
    mimeType: 'text/calendar',
    dialogTitle: 'Add to Calendar',
  });
}

export async function addToCalendar(event: EventDto, target: CalendarTarget): Promise<void> {
  if (target === 'apple') {
    await shareIcs(event);
    return;
  }
  const url = target === 'google' ? googleCalendarUrl(event) : outlookCalendarUrl(event);
  await Linking.openURL(url);
}

const TARGETS: { label: string; target: CalendarTarget }[] = [
  { label: 'Apple Calendar', target: 'apple' },
  { label: 'Google Calendar', target: 'google' },
  { label: 'Outlook', target: 'outlook' },
];

/** Presents the three calendar choices, then adds the game to the chosen one */
export function promptAddToCalendar(event: EventDto): void {
  const run = async (target: CalendarTarget) => {
    try {
      await addToCalendar(event, target);
    } catch {
      Alert.alert('Could Not Add to Calendar', 'Something went wrong opening your calendar. Please try again.');
    }
  };

  if (Platform.OS === 'ios') {
    ActionSheetIOS.showActionSheetWithOptions(
      {
        options: ['Cancel', ...TARGETS.map((t) => t.label)],
        cancelButtonIndex: 0,
        title: 'Add to Calendar',
      },
      (index) => {
        if (index === 0) return;
        run(TARGETS[index - 1].target);
      }
    );
    return;
  }

  Alert.alert('Add to Calendar', undefined, [
    { text: 'Cancel', style: 'cancel' },
    ...TARGETS.map((t) => ({ text: t.label, onPress: () => run(t.target) })),
  ]);
}
