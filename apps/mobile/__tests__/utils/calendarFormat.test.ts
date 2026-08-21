import type { EventDto } from '@bhmhockey/shared';
import {
  buildIcs,
  calendarTitle,
  googleCalendarUrl,
  outlookCalendarUrl,
} from '../../utils/calendarFormat';

// Only the fields the calendar helpers read
const makeEvent = (overrides: Partial<EventDto> = {}): EventDto =>
  ({
    id: 'e1f2a3b4-0000-0000-0000-000000000001',
    name: 'Sunday Skate',
    organizationName: 'AMP Hockey',
    eventDate: '2026-07-25T22:30:00Z',
    duration: 90,
    venue: 'Pelham Civic Complex',
    cost: 19,
    description: '',
    ...overrides,
  } as unknown as EventDto);

describe('calendarTitle', () => {
  it('uses the event name', () => {
    expect(calendarTitle(makeEvent())).toBe('Sunday Skate');
  });

  it('falls back to the org name when the event is unnamed', () => {
    expect(calendarTitle(makeEvent({ name: undefined }))).toBe('AMP Hockey Hockey');
  });

  it('falls back to a generic title for unnamed standalone events', () => {
    expect(calendarTitle(makeEvent({ name: undefined, organizationName: undefined })))
      .toBe('Hockey Game');
  });
});

describe('buildIcs', () => {
  it('wraps a single VEVENT in a VCALENDAR', () => {
    const ics = buildIcs(makeEvent());
    expect(ics.startsWith('BEGIN:VCALENDAR')).toBe(true);
    expect(ics.trimEnd().endsWith('END:VCALENDAR')).toBe(true);
    expect(ics).toContain('BEGIN:VEVENT');
    expect(ics).toContain('END:VEVENT');
  });

  it('uses compact UTC stamps and applies the duration to the end time', () => {
    const ics = buildIcs(makeEvent());
    expect(ics).toContain('DTSTART:20260725T223000Z');
    expect(ics).toContain('DTEND:20260726T000000Z'); // +90 minutes
  });

  it('defaults to an hour when duration is missing', () => {
    const ics = buildIcs(makeEvent({ duration: 0 }));
    expect(ics).toContain('DTEND:20260725T233000Z');
  });

  it('keys the entry on the event id so re-adding updates instead of duplicating', () => {
    expect(buildIcs(makeEvent())).toContain('UID:e1f2a3b4-0000-0000-0000-000000000001@bhmhockey');
  });

  it('includes venue and cost', () => {
    const ics = buildIcs(makeEvent());
    expect(ics).toContain('LOCATION:Pelham Civic Complex');
    expect(ics).toContain('Cost: $19.00');
  });

  it('escapes commas, semicolons and newlines per RFC 5545', () => {
    const ics = buildIcs(makeEvent({
      name: 'Skate, Session; One',
      venue: 'Rink A\nDoor 3',
      organizationName: undefined,
      cost: 0,
    }));
    expect(ics).toContain('SUMMARY:Skate\\, Session\; One');
    expect(ics).toContain('LOCATION:Rink A\\nDoor 3');
  });

  it('omits optional lines when there is nothing to put in them', () => {
    const ics = buildIcs(makeEvent({ venue: undefined, organizationName: undefined, cost: 0 }));
    expect(ics).not.toContain('LOCATION:');
    expect(ics).not.toContain('DESCRIPTION:');
  });

  it('uses CRLF line endings', () => {
    expect(buildIcs(makeEvent())).toContain('\r\n');
  });
});

describe('web calendar links', () => {
  it('builds a Google template link with a start/end range', () => {
    const url = googleCalendarUrl(makeEvent());
    expect(url.startsWith('https://calendar.google.com/calendar/render?')).toBe(true);
    expect(url).toContain('action=TEMPLATE');
    expect(url).toContain('dates=20260725T223000Z%2F20260726T000000Z');
    expect(url).toContain('text=Sunday+Skate');
  });

  it('builds an Outlook compose link with ISO times', () => {
    const url = outlookCalendarUrl(makeEvent());
    expect(url.startsWith('https://outlook.live.com/calendar/0/deeplink/compose?')).toBe(true);
    expect(url).toContain('rru=addevent');
    expect(url).toContain('startdt=2026-07-25T22%3A30%3A00.000Z');
    expect(url).toContain('subject=Sunday+Skate');
  });

  it('url-encodes venue text', () => {
    expect(googleCalendarUrl(makeEvent())).toContain('location=Pelham+Civic+Complex');
  });
});
