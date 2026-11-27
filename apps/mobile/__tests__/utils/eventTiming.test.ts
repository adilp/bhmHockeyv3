/**
 * A game is over once its start plus its duration has passed. The event screens
 * drop every before-the-game control on that one rule; these tests pin the rule
 * and that the screens use it (and no second copy of it).
 */
import * as fs from 'fs';
import * as path from 'path';
import { hasEnded, DEFAULT_DURATION_MINUTES } from '../../utils/eventTiming';

const NOW = new Date('2026-12-01T20:00:00Z');
const at = (minutesAgo: number) => new Date(NOW.getTime() - minutesAgo * 60_000).toISOString();

describe('hasEnded', () => {
  it('is start plus duration against now', () => {
    expect(hasEnded({ eventDate: at(61), duration: 60 }, NOW)).toBe(true);
    expect(hasEnded({ eventDate: at(59), duration: 60 }, NOW)).toBe(false);   // still being played
    expect(hasEnded({ eventDate: at(60), duration: 60 }, NOW)).toBe(false);   // ends exactly now
    expect(hasEnded({ eventDate: at(-60 * 24), duration: 60 }, NOW)).toBe(false); // tomorrow
    expect(hasEnded({ eventDate: at(100), duration: 90 }, NOW)).toBe(true);
    expect(hasEnded({ eventDate: at(100), duration: 120 }, NOW)).toBe(false);
  });

  it('a missing or odd duration is the default; a missing or bad start is "not ended" (the screen as before)', () => {
    expect(DEFAULT_DURATION_MINUTES).toBe(60);
    expect(hasEnded({ eventDate: at(61) }, NOW)).toBe(true);
    expect(hasEnded({ eventDate: at(59), duration: null }, NOW)).toBe(false);
    expect(hasEnded({ eventDate: at(61), duration: NaN }, NOW)).toBe(true);
    expect(hasEnded({ eventDate: at(61), duration: -5 }, NOW)).toBe(true);
    expect(hasEnded({ eventDate: 'not a date', duration: 60 }, NOW)).toBe(false);
    expect(hasEnded({ eventDate: null }, NOW)).toBe(false);
    expect(hasEnded(null, NOW)).toBe(false);
    expect(hasEnded(undefined, NOW)).toBe(false);
  });
});

describe('the event screens drop the before-the-game controls on that rule', () => {
  const MOBILE = path.resolve(__dirname, '..', '..');
  const read = (rel: string) => fs.readFileSync(path.join(MOBILE, rel), 'utf8');

  it('the event screen computes it once and hands it to the footer and both tabs', () => {
    const screen = read('app/events/[id]/index.tsx');
    expect(screen).toMatch(/import \{ hasEnded \} from '..\/..\/..\/utils\/eventTiming'/);
    expect(screen).toMatch(/const ended = hasEnded\(selectedEvent\)/);
    expect((screen.match(/hasEnded=\{ended\}/g) ?? []).length).toBe(3);   // footer, info, roster
  });

  it('the footer shows neither Register nor Join Waitlist once ended', () => {
    expect(read('components/event-detail/RegistrationFooter.tsx')).toMatch(/if \(isDraft \|\| hasEnded\) \{\s*return null;/);
  });

  it('the roster hides Add Player, Publish and the waitlist moves once ended', () => {
    const roster = read('components/event-detail/EventRosterTab.tsx');
    expect(roster).toMatch(/\{!hasEnded && \(\s*<TouchableOpacity\s*style=\{styles\.addPlayerButton\}/);
    expect(roster).toMatch(/\(isDraft \|\| !event\.isRosterPublished\) && !hasEnded/);
    expect(roster).toMatch(/onMoveToRoster=\{canManage && !hasEnded/);
    expect(roster).toMatch(/onMoveToWaitlist=\{canManage && !hasEnded/);
  });

  it('the info tab hides the waitlist pay prompt, cost preview, calendar and cancel once ended', () => {
    const info = read('components/event-detail/EventInfoTab.tsx');
    expect(info).toMatch(/showWaitlistPaymentCard = !hasEnded/);
    expect(info).toMatch(/showCostPreview = !hasEnded/);
    expect(info).toMatch(/event\.isRegistered && !hasEnded && \(\s*<View style=\{styles\.calendarSection\}/);
    expect(info).toMatch(/\(event\.isRegistered \|\| event\.amIWaitlisted\) && !hasEnded && \(\s*<View style=\{styles\.cancelSection\}/);
  });

  it('there is no second "is it over" rule in the app', () => {
    const files: string[] = [];
    const walk = (d: string) => fs.readdirSync(d, { withFileTypes: true }).forEach((e) => {
      if (['node_modules', '__tests__', 'ios', 'android', 'assets'].includes(e.name)) return;
      const f = path.join(d, e.name);
      if (e.isDirectory()) walk(f); else if (/\.tsx?$/.test(e.name)) files.push(f);
    });
    walk(MOBILE);
    const copies = files.filter((f) => /isPastEvent|function hasEnded/.test(fs.readFileSync(f, 'utf8'))
      && !f.endsWith(path.join('utils', 'eventTiming.ts')));
    expect(copies.map((f) => path.relative(MOBILE, f))).toEqual([]);
  });
});
