/**
 * Nothing the game-results feature renders may throw to the app's root. Every
 * entry point an existing screen or the router reaches must sit inside a
 * SafeBoundary (a render error then hides that section, or shows the screen's
 * own "unavailable" state, and the rest of the app keeps working).
 *
 * This walks the source rather than one file, so a new component or screen is
 * caught as it arrives. A component exempted here is listed with the reason.
 */
import * as fs from 'fs';
import * as path from 'path';

const MOBILE = path.resolve(__dirname, '..', '..');
const DIR = path.join(MOBILE, 'components', 'game-results');

/** Exports rendered only inside a parent that is itself wrapped. */
const INNER_ONLY: Record<string, string> = {
  SafeBoundary: 'the boundary itself',
  LeaderboardTable: 'rendered only by OrgResultsSection and the leaderboard screen, both wrapped',
  StatPicker: 'rendered only by OrgResultsSection and the leaderboard screen, both wrapped',
};

/** The routes this feature added; each default export wraps its screen. */
const NEW_SCREENS = ['app/players/[id].tsx', 'app/organizations/[id]/leaderboard.tsx'];

function walk(dir: string): string[] {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    if (e.name === 'node_modules' || e.name === '__tests__') return [];
    const full = path.join(dir, e.name);
    return e.isDirectory() ? walk(full) : /\.tsx?$/.test(e.name) ? [full] : [];
  });
}

/** The JSX an exported function returns: from its declaration to the next top-level declaration. */
function exportedBodies(src: string): Record<string, string> {
  const out: Record<string, string> = {};
  const re = /^export (?:default )?(?:function|class) (\w+)/gm;
  const starts = [...src.matchAll(re)];
  starts.forEach((m, i) => {
    const end = i + 1 < starts.length ? starts[i + 1].index! : src.length;
    const next = src.slice(m.index!, end).search(/\n(?:const|function|export|interface|type) /);
    out[m[1]] = src.slice(m.index!, next > 0 ? m.index! + next : end);
  });
  return out;
}

describe('every game-results entry point is inside a SafeBoundary', () => {
  const files = fs.readdirSync(DIR).filter((f) => f.endsWith('.tsx'));

  it('each exported component of components/game-results wraps itself (or is listed)', () => {
    const unwrapped: string[] = [];
    let seen = 0;
    for (const f of files) {
      for (const [name, body] of Object.entries(exportedBodies(fs.readFileSync(path.join(DIR, f), 'utf8')))) {
        seen++;
        if (INNER_ONLY[name]) continue;
        if (!/<SafeBoundary\b/.test(body)) unwrapped.push(`${f}: ${name}`);
      }
    }
    expect(seen).toBeGreaterThanOrEqual(8);
    expect(unwrapped).toEqual([]);
  });

  it('the inner-only components are imported nowhere else', () => {
    const offenders: string[] = [];
    const allowed = ['OrgResultsSection.tsx', 'leaderboard.tsx', 'SafeBoundary.tsx'];
    for (const file of walk(MOBILE).filter((f) => !f.includes(`${path.sep}__tests__${path.sep}`))) {
      const src = fs.readFileSync(file, 'utf8');
      for (const name of ['LeaderboardTable', 'StatPicker']) {
        if (new RegExp(`import[^;]*\\b${name}\\b[^;]*from`).test(src) && !allowed.includes(path.basename(file))) {
          offenders.push(`${path.relative(MOBILE, file)}: ${name}`);
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it.each(NEW_SCREENS)('%s wraps its default export', (rel) => {
    const src = fs.readFileSync(path.join(MOBILE, rel), 'utf8');
    const m = /export default function \w+\([^)]*\)\s*\{([\s\S]*?)\n\}/.exec(src);
    expect(m).not.toBeNull();
    expect(m![1]).toMatch(/<SafeBoundary\b/);
  });

  it('no game-results code shows an alert while loading (failures are quiet)', () => {
    // MyStatsSettings may alert on a failed SAVE the user just made; nothing alerts on a read
    const offenders = [...files.map((f) => path.join(DIR, f)), ...NEW_SCREENS.map((r) => path.join(MOBILE, r)),
      path.join(MOBILE, 'stores', 'gameResultStore.ts')]
      .filter((f) => path.basename(f) !== 'MyStatsSettings.tsx' && /Alert\.alert/.test(fs.readFileSync(f, 'utf8')));
    expect(offenders).toEqual([]);
  });
});
