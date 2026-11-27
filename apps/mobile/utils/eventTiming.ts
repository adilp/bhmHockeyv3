/**
 * Whether a game is over: its start plus its duration is in the past.
 *
 * The ONE rule the event screens use to drop the controls that only make sense
 * before a game (sign up, join the waitlist, add a player, publish, cancel, add to
 * calendar, pay to claim a waitlist spot). It mirrors the server's
 * `EventService.HasEnded`, which refuses a sign-up on an ended game.
 *
 * Never throws. A missing or unreadable start means "not ended", so a bad value
 * leaves the screen exactly as it was; a missing duration is the server's default
 * of 60 minutes.
 */
export const DEFAULT_DURATION_MINUTES = 60;

export function hasEnded(
  event: { eventDate?: string | null; duration?: number | null } | null | undefined,
  now: Date = new Date()
): boolean {
  if (!event || typeof event.eventDate !== 'string') return false;
  const start = new Date(event.eventDate).getTime();
  if (!Number.isFinite(start)) return false;
  const d = event.duration;
  const minutes = typeof d === 'number' && Number.isFinite(d) && d >= 0 ? d : DEFAULT_DURATION_MINUTES;
  return start + minutes * 60_000 < now.getTime();
}
