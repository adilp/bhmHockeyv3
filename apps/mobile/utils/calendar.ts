import { Platform, ActionSheetIOS, Alert, Linking } from 'react-native';
import type { EventDto } from '@bhmhockey/shared';
import { getApiUrl } from '../config/api';
import { googleCalendarUrl, outlookCalendarUrl, type CalendarTarget } from './calendarFormat';

/**
 * Adds a game to the phone's calendar without a native calendar module.
 * Apple opens the server-generated .ics in the browser, which is what
 * triggers iOS's own "Add to Calendar" import - Calendar is not a share
 * sheet destination, so sharing the file goes nowhere. Google and Outlook
 * take pre-filled web links.
 */

export type { CalendarTarget };

/** Public endpoint, no .ics extension: iOS treats a .ics URL as a calendar
 *  subscription feed rather than a single event to import. */
function icsUrl(event: EventDto): string {
  return `${getApiUrl()}/events/${event.id}/calendar`;
}

export async function addToCalendar(event: EventDto, target: CalendarTarget): Promise<void> {
  const url =
    target === 'apple'
      ? icsUrl(event)
      : target === 'google'
        ? googleCalendarUrl(event)
        : outlookCalendarUrl(event);
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
