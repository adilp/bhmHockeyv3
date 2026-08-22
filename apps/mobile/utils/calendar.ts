import { Alert } from 'react-native';
import * as Calendar from 'expo-calendar';
import type { EventDto } from '@bhmhockey/shared';

/**
 * Adds a game to the phone's calendar through the OS-provided event sheet.
 *
 * Deliberately uses createEventInCalendarAsync rather than createEventAsync:
 * the system UI does the writing after the user confirms, so the app never
 * needs calendar permission and never gains read access to anyone's events.
 * The user also picks which calendar (iCloud, Gmail, Outlook) in that sheet.
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

/** Opens the system's new-event sheet pre-filled with the game */
export async function promptAddToCalendar(event: EventDto): Promise<void> {
  const { start, end } = eventWindow(event);

  try {
    await Calendar.createEventInCalendarAsync({
      title: calendarTitle(event),
      startDate: start,
      endDate: end,
      location: event.venue ?? undefined,
      notes: calendarNotes(event) || undefined,
    });
    // No confirmation alert: the system sheet already shows the result, and
    // on Android the action is always reported as 'done' regardless of choice
  } catch {
    Alert.alert(
      'Could Not Add to Calendar',
      'Something went wrong opening your calendar. Please try again.'
    );
  }
}
