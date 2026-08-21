import type { EventDto } from '@bhmhockey/shared';

/**
 * Calendar link building for Google and Outlook. Kept free of
 * react-native and expo imports so it can be unit tested directly - the
 * platform side (share sheet, deep links) lives in ./calendar.
 */

export type CalendarTarget = 'apple' | 'google' | 'outlook';

/** ICS/Google compact UTC stamp: 20260725T223000Z */
function toStamp(date: Date): string {
  return date.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
}

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

export function calendarDescription(event: EventDto): string {
  const parts: string[] = [];
  if (event.organizationName) parts.push(event.organizationName);
  if (event.cost > 0) parts.push(`Cost: $${event.cost.toFixed(2)}`);
  if (event.description) parts.push(event.description);
  return parts.join('\n');
}

export function googleCalendarUrl(event: EventDto): string {
  const { start, end } = eventWindow(event);
  const params = new URLSearchParams({
    action: 'TEMPLATE',
    text: calendarTitle(event),
    dates: `${toStamp(start)}/${toStamp(end)}`,
  });
  if (event.venue) params.set('location', event.venue);
  const details = calendarDescription(event);
  if (details) params.set('details', details);
  return `https://calendar.google.com/calendar/render?${params.toString()}`;
}

export function outlookCalendarUrl(event: EventDto): string {
  const { start, end } = eventWindow(event);
  const params = new URLSearchParams({
    path: '/calendar/action/compose',
    rru: 'addevent',
    subject: calendarTitle(event),
    startdt: start.toISOString(),
    enddt: end.toISOString(),
  });
  if (event.venue) params.set('location', event.venue);
  const body = calendarDescription(event);
  if (body) params.set('body', body);
  return `https://outlook.live.com/calendar/0/deeplink/compose?${params.toString()}`;
}
