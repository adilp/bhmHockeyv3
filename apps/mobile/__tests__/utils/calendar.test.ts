import { googleCalendarUrl } from '../../utils/calendar';
import type { EventDto } from '@bhmhockey/shared';

function makeEvent(overrides: Partial<EventDto> = {}): EventDto {
  return {
    name: 'Sunday Skate',
    organizationName: 'AMP Hockey',
    eventDate: '2026-09-15T18:00:00.000Z',
    duration: 90,
    venue: 'Pelham Civic Complex',
    cost: 15,
    description: 'Bring dark and white jerseys',
    ...overrides,
  } as EventDto;
}

describe('googleCalendarUrl', () => {
  it('targets the Google Calendar event-template endpoint', () => {
    expect(googleCalendarUrl(makeEvent())).toContain(
      'https://calendar.google.com/calendar/render?action=TEMPLATE'
    );
  });

  it('uses the event name as the URL-encoded title', () => {
    expect(googleCalendarUrl(makeEvent({ name: 'Sunday Skate' }))).toContain(
      'text=Sunday%20Skate'
    );
  });

  it('falls back to an org-derived title when the event has no name', () => {
    const url = googleCalendarUrl(
      makeEvent({ name: undefined, organizationName: 'AMP Hockey' })
    );
    expect(url).toContain('text=AMP%20Hockey%20Hockey');
  });

  it('spans start to start+duration in compact UTC', () => {
    const url = googleCalendarUrl(
      makeEvent({ eventDate: '2026-09-15T18:00:00.000Z', duration: 90 })
    );
    expect(url).toContain('dates=20260915T180000Z/20260915T193000Z');
  });

  it('defaults to a one-hour window when duration is unset', () => {
    const url = googleCalendarUrl(
      makeEvent({ eventDate: '2026-09-15T18:00:00.000Z', duration: 0 })
    );
    expect(url).toContain('dates=20260915T180000Z/20260915T190000Z');
  });

  it('includes the venue as the encoded location', () => {
    expect(googleCalendarUrl(makeEvent({ venue: 'Pelham Civic Complex' }))).toContain(
      'location=Pelham%20Civic%20Complex'
    );
  });

  it('omits location when the event has no venue', () => {
    expect(googleCalendarUrl(makeEvent({ venue: undefined }))).not.toContain(
      'location='
    );
  });
});
