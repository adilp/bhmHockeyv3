import { Platform, ActionSheetIOS, Alert, Linking } from 'react-native';
import * as Calendar from 'expo-calendar';
import type { EventDto } from '@bhmhockey/shared';

/**
 * Adds a game to the phone's calendar with expo-calendar, which writes to the
 * device's calendar store directly - no browser hop, no .ics file. Accounts
 * the user has set up on the phone (iCloud, Gmail, Outlook) all appear here,
 * so "which calendar" is a real choice rather than three guesses.
 */

function eventWindow(event: EventDto): { start: Date; end: Date } {
  const start = new Date(event.eventDate);
  // Duration is in minutes and defaults to an hour when unset
  const end = new Date(start.getTime() + (event.duration || 60) * 60 * 1000);
  return { start, end };
}

export function calendarTitle(event: EventDto): string {
  if (event.name) return event.name;
  return event.organizationName ? `${event.organizationName} Hockey` : 'Hockey Game';
}

export function calendarNotes(event: EventDto): string {
  const parts: string[] = [];
  if (event.organizationName) parts.push(event.organizationName);
  if (event.cost > 0) parts.push(`Cost: $${event.cost.toFixed(2)}`);
  if (event.description) parts.push(event.description);
  return parts.join('\n');
}

/** Calendars the user can actually write to, newest accounts included */
async function writableCalendars(): Promise<Calendar.Calendar[]> {
  const calendars = await Calendar.getCalendarsAsync(Calendar.EntityTypes.EVENT);
  return calendars.filter((c) => c.allowsModifications);
}

async function createEvent(event: EventDto, calendarId: string): Promise<void> {
  const { start, end } = eventWindow(event);
  await Calendar.createEventAsync(calendarId, {
    title: calendarTitle(event),
    startDate: start,
    endDate: end,
    location: event.venue ?? undefined,
    notes: calendarNotes(event) || undefined,
    timeZone: 'UTC', // start/end are absolute instants; the OS renders them locally
  });
}

function confirmAdded(calendarTitleName?: string) {
  Alert.alert(
    'Added to Calendar',
    calendarTitleName ? `The game was added to "${calendarTitleName}".` : 'The game was added to your calendar.'
  );
}

function permissionDenied() {
  Alert.alert(
    'Calendar Access Needed',
    'Allow calendar access in Settings to add games to your calendar.',
    [
      { text: 'Not Now', style: 'cancel' },
      { text: 'Open Settings', onPress: () => Linking.openSettings() },
    ]
  );
}

/**
 * Asks for calendar permission, then adds the game. When the phone has more
 * than one writable calendar the user picks which one.
 */
export async function promptAddToCalendar(event: EventDto): Promise<void> {
  try {
    const { status } = await Calendar.requestCalendarPermissionsAsync();
    if (status !== 'granted') {
      permissionDenied();
      return;
    }

    const calendars = await writableCalendars();
    if (calendars.length === 0) {
      Alert.alert(
        'No Calendar Available',
        'This device has no calendar that can be written to. Add an account in the Calendar app first.'
      );
      return;
    }

    if (calendars.length === 1) {
      await createEvent(event, calendars[0].id);
      confirmAdded(calendars[0].title);
      return;
    }

    const labels = calendars.map((c) => c.title || c.source?.name || 'Calendar');
    const pick = async (index: number) => {
      try {
        await createEvent(event, calendars[index].id);
        confirmAdded(labels[index]);
      } catch {
        Alert.alert('Could Not Add to Calendar', 'Something went wrong saving the game. Please try again.');
      }
    };

    if (Platform.OS === 'ios') {
      ActionSheetIOS.showActionSheetWithOptions(
        { options: ['Cancel', ...labels], cancelButtonIndex: 0, title: 'Add to which calendar?' },
        (index) => {
          if (index === 0) return;
          pick(index - 1);
        }
      );
      return;
    }

    Alert.alert('Add to which calendar?', undefined, [
      { text: 'Cancel', style: 'cancel' },
      ...labels.map((label, i) => ({ text: label, onPress: () => pick(i) })),
    ]);
  } catch {
    Alert.alert('Could Not Add to Calendar', 'Something went wrong reaching your calendar. Please try again.');
  }
}
