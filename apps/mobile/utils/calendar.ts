import { Alert, Linking } from 'react-native';
import type { EventDto } from '@bhmhockey/shared';

/**
 * Adds a game to the player's calendar via a Google Calendar "event template"
 * link, opened in the browser or the Google Calendar app.
 *
 * This is deliberately a plain https link rather than a native integration:
 * it needs no native module and no calendar permission, so the feature ships
 * over-the-air. The trade-off is that it targets Google Calendar — a user on
 * iCloud or Outlook is routed through Google's web UI to add the event.
 *
 * (An earlier version used expo-calendar's native new-event sheet; it was
 * removed because the native module forced every release to be a store build.)
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

/** Google Calendar expects start/end as compact UTC: YYYYMMDDTHHMMSSZ. */
function toGoogleUtc(date: Date): string {
  return date.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}Z$/, 'Z');
}

/** Builds a Google Calendar "add event" template URL pre-filled with the game. */
export function googleCalendarUrl(event: EventDto): string {
  const { start, end } = eventWindow(event);
  const params = [
    'action=TEMPLATE',
    `text=${encodeURIComponent(calendarTitle(event))}`,
    `dates=${toGoogleUtc(start)}/${toGoogleUtc(end)}`,
  ];
  const notes = calendarNotes(event);
  if (notes) params.push(`details=${encodeURIComponent(notes)}`);
  if (event.venue) params.push(`location=${encodeURIComponent(event.venue)}`);
  return `https://calendar.google.com/calendar/render?${params.join('&')}`;
}

/** Opens the game in Google Calendar's new-event view. */
export async function promptAddToCalendar(event: EventDto): Promise<void> {
  try {
    await Linking.openURL(googleCalendarUrl(event));
  } catch {
    Alert.alert(
      'Could Not Add to Calendar',
      'Something went wrong opening your calendar. Please try again.'
    );
  }
}
