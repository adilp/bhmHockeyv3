/**
 * Over-the-air safety: the JavaScript bundle this branch builds must run on the
 * native binary people have installed today (store version 1.0.5, iOS build 10,
 * Android versionCode 6, built from commit dfced95 on 2026-02-26).
 *
 * An expo-updates update replaces only JavaScript and assets. If the bundle
 * imports a package whose native code is not inside the 1.0.5 binary, the app
 * crashes on launch for everyone who receives it. So this test walks EVERY
 * source file the bundle can contain and requires each bare import to name a
 * package the 1.0.5 build already shipped, and pins the settings that decide
 * which binaries an update reaches (runtimeVersion, update URL, plugins).
 *
 * If this fails because a package is genuinely new, that change needs a store
 * release (a new binary and a new runtimeVersion) - it cannot travel over the air.
 */
import * as fs from 'fs';
import * as path from 'path';

const MOBILE = path.resolve(__dirname, '..', '..');
const REPO = path.resolve(MOBILE, '..', '..');

/** apps/mobile/package.json "dependencies" at dfced95 (store 1.0.5), names and ranges. */
const MOBILE_DEPENDENCIES_AT_1_0_5: Record<string, string> = {
  '@bhmhockey/api-client': '*',
  '@bhmhockey/shared': '*',
  '@react-native-async-storage/async-storage': '2.2.0',
  '@react-native-community/datetimepicker': '8.4.4',
  '@react-native-picker/picker': '2.11.1',
  '@react-navigation/bottom-tabs': '^7.2.0',
  '@react-navigation/native': '^7.0.14',
  '@react-navigation/native-stack': '^7.2.0',
  expo: '~54.0.32',
  'expo-clipboard': '~8.0.8',
  'expo-constants': '~18.0.13',
  'expo-device': '~8.0.10',
  'expo-file-system': '~19.0.21',
  'expo-linking': '~8.0.11',
  'expo-notifications': '~0.32.16',
  'expo-router': '~6.0.22',
  'expo-sharing': '~14.0.8',
  'expo-splash-screen': '~31.0.13',
  'expo-status-bar': '~3.0.9',
  'expo-updates': '~29.0.16',
  react: '19.1.0',
  'react-native': '0.81.5',
  'react-native-gesture-handler': '~2.28.0',
  'react-native-reanimated': '~4.1.1',
  'react-native-safe-area-context': '~5.6.0',
  'react-native-screens': '~4.16.0',
  'react-native-view-shot': '4.0.3',
  'react-native-worklets': '0.5.1',
  zustand: '^5.0.2',
};

/** The workspace packages' own dependencies at dfced95 (bundled into the app through them). */
const WORKSPACE_DEPENDENCIES_AT_1_0_5 = ['axios', '@react-native-async-storage/async-storage', 'react-native'];

/**
 * Packages the 1.0.5 source itself imported without listing them (they arrive
 * through `expo`), so they are inside the 1.0.5 binary and bundle.
 */
const IMPORTED_BY_1_0_5_THROUGH_EXPO: Record<string, string> = {
  '@expo/vector-icons': 'a dependency of expo; imported across the 1.0.5 screens',
  'date-fns': 'pure JavaScript; imported by components/tournaments/PendingInvitationCard.tsx at 1.0.5',
};

const ALLOWED = new Set<string>([
  ...Object.keys(MOBILE_DEPENDENCIES_AT_1_0_5),
  ...WORKSPACE_DEPENDENCIES_AT_1_0_5,
  ...Object.keys(IMPORTED_BY_1_0_5_THROUGH_EXPO),
]);

/** Everything Metro can put in the bundle: the app's own folders and the two workspace packages. */
const SOURCE_ROOTS = [
  path.join(MOBILE, 'app'),
  path.join(MOBILE, 'components'),
  path.join(MOBILE, 'stores'),
  path.join(MOBILE, 'utils'),
  path.join(MOBILE, 'hooks'),
  path.join(MOBILE, 'config'),
  path.join(MOBILE, 'theme'),
  path.join(REPO, 'packages', 'shared', 'src'),
  path.join(REPO, 'packages', 'api-client', 'src'),
];

function sourceFiles(dir: string): string[] {
  if (!fs.existsSync(dir)) return [];
  const out: string[] = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === 'node_modules' || entry.name === '__tests__' || entry.name === '__mocks__') continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...sourceFiles(full));
    else if (/\.(ts|tsx|js|jsx)$/.test(entry.name) && !/\.(test|spec)\.[jt]sx?$/.test(entry.name)) out.push(full);
  }
  return out;
}

/** Every module specifier: static import/export ... from, side-effect import, require(), import(). */
function specifiers(source: string): string[] {
  const noComments = source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|[^:'"`])\/\/.*$/gm, '$1');
  const out: string[] = [];
  const patterns = [
    /\b(?:import|export)\s[^'"`;]*?\sfrom\s*['"]([^'"]+)['"]/g,
    /\bimport\s*['"]([^'"]+)['"]/g,
    /\brequire\s*\(\s*['"]([^'"]+)['"]\s*\)/g,
    /\bimport\s*\(\s*['"]([^'"]+)['"]\s*\)/g,
  ];
  for (const re of patterns) for (const m of noComments.matchAll(re)) out.push(m[1]);
  return out;
}

/** 'expo-file-system/legacy' -> 'expo-file-system'; '@scope/pkg/x' -> '@scope/pkg'; relative and '@/' -> null. */
export function packageOf(spec: string): string | null {
  if (spec.startsWith('.') || spec.startsWith('/') || spec.startsWith('@/')) return null;
  const parts = spec.split('/');
  return spec.startsWith('@') ? parts.slice(0, 2).join('/') : parts[0];
}

describe('the app change can travel over the air to the 1.0.5 binary', () => {
  const files = SOURCE_ROOTS.flatMap(sourceFiles);

  it('walks the whole bundle source (and would notice if it found nothing)', () => {
    expect(files.length).toBeGreaterThan(100);
    expect(files.some((f) => f.endsWith(path.join('stores', 'gameResultStore.ts')))).toBe(true);
    expect(files.some((f) => f.endsWith(path.join('app', 'players', '[id].tsx')))).toBe(true);
  });

  it('every bare import names a package the 1.0.5 build already shipped', () => {
    const offenders: string[] = [];
    for (const file of files) {
      for (const spec of specifiers(fs.readFileSync(file, 'utf8'))) {
        const pkg = packageOf(spec);
        if (pkg && !ALLOWED.has(pkg)) offenders.push(`${path.relative(REPO, file)}: '${spec}'`);
      }
    }
    expect(offenders).toEqual([]);
  });

  it('the dependency list is exactly the 1.0.5 one (no new native module)', () => {
    const pkg = JSON.parse(fs.readFileSync(path.join(MOBILE, 'package.json'), 'utf8'));
    expect(pkg.dependencies).toEqual(MOBILE_DEPENDENCIES_AT_1_0_5);
    for (const ws of ['shared', 'api-client']) {
      const deps = Object.keys(JSON.parse(fs.readFileSync(path.join(REPO, 'packages', ws, 'package.json'), 'utf8')).dependencies ?? {});
      const unknown = deps.filter((d) => !d.startsWith('@bhmhockey/') && !ALLOWED.has(d));
      expect(unknown).toEqual([]);
    }
  });

  it('no source reaches a native module directly', () => {
    const direct = files.filter((f) => /\b(requireNativeModule|requireOptionalNativeModule|TurboModuleRegistry|NativeModules)\b/.test(
      fs.readFileSync(f, 'utf8')));
    expect(direct.map((f) => path.relative(REPO, f))).toEqual([]);
  });

  it('the settings that decide which binaries receive an update are those of 1.0.5', () => {
    const { expo } = JSON.parse(fs.readFileSync(path.join(MOBILE, 'app.json'), 'utf8'));
    expect(expo.runtimeVersion).toBe('1.0.0');
    expect(expo.updates).toEqual({
      url: 'https://u.expo.dev/f6225f7a-0181-4b8c-b44a-cea3e0017cfa',
      enabled: true,
      checkOnLaunch: 'ALWAYS',
      fallbackToCacheTimeout: 5000,
    });
    expect(expo.extra.eas.projectId).toBe('f6225f7a-0181-4b8c-b44a-cea3e0017cfa');
    expect(expo.plugins).toEqual([
      'expo-router',
      ['expo-notifications', { icon: './assets/notification-icon.png', color: '#00D9C0' }],
    ]);
    // The store build listens on the production channel
    const eas = JSON.parse(fs.readFileSync(path.join(MOBILE, 'eas.json'), 'utf8'));
    expect(eas.build.production.channel).toBe('production');
    // The committed iOS project carries the same runtime version
    const expoPlist = fs.readFileSync(path.join(MOBILE, 'ios', 'BHMHockey', 'Supporting', 'Expo.plist'), 'utf8');
    expect(expoPlist).toMatch(/<key>EXUpdatesRuntimeVersion<\/key>\s*<string>1\.0\.0<\/string>/);
  });

  it('packageOf reads scoped, deep and relative specifiers', () => {
    expect(packageOf('expo-file-system/legacy')).toBe('expo-file-system');
    expect(packageOf('@expo/vector-icons')).toBe('@expo/vector-icons');
    expect(packageOf('zustand/react/shallow')).toBe('zustand');
    expect(packageOf('../utils/gameResults')).toBeNull();
    expect(packageOf('@/theme')).toBeNull();
  });
});
