# Game results: getting the app change to phones without a store release

Written 2026-11-27 on `game-results-app`. Nothing here has been
published; every command below is the owner's to run, after the server is live.

## The answer

The app change is JavaScript only, so it can ship as an **expo-updates
over-the-air (OTA) update** on the `production` branch at runtime version
`1.0.0`, which the installed store build (1.0.5) accepts. No App Store or Play
Store release is needed. The server must be deployed and verified first; until
it is, and even after the OTA lands, every new surface stays hidden wherever its
data is absent.

## The evidence (from this repository's own configuration)

| question | what the files say | where |
|---|---|---|
| Which update library? | `expo-updates ~29.0.16`, already in the 1.0.5 dependency list | `apps/mobile/package.json` |
| Updates on in the store build? | `updates.enabled: true`, `checkOnLaunch: ALWAYS`, `fallbackToCacheTimeout: 5000`, URL `https://u.expo.dev/f6225f7a-...` | `apps/mobile/app.json`; iOS: `ios/BHMHockey/Supporting/Expo.plist` (`EXUpdatesEnabled`, `EXUpdatesCheckOnLaunch ALWAYS`, `EXUpdatesLaunchWaitMs 5000`) |
| Runtime version policy | a FIXED string, `"runtimeVersion": "1.0.0"` (not a fingerprint or appVersion policy). An update reaches every binary built with runtime `1.0.0`, which includes 1.0.5. `app.config.js` keeps `environment: 'production'` constant "for fingerprint consistency" | `app.json`, `Expo.plist` `EXUpdatesRuntimeVersion 1.0.0`, `app.config.js` |
| Which channel does the store build listen to? | the `production` build profile sets `"channel": "production"` | `apps/mobile/eas.json` |
| Has this path worked before? | yes: the release after 1.0.5 "has shipped as an OTA" (`eas update --branch production`), and the profile screen shows `js <update id>` to confirm it on a phone | `docs/RELEASE_CHECKLIST.md` |
| In-app behavior | `useOtaUpdates` checks on launch and on foreground, downloads, then shows a "Restart Now" alert | `apps/mobile/hooks/useOtaUpdates.ts` |
| Does the change need native code? | no. Every bare import in the bundle names a package the 1.0.5 build shipped; the dependency list is exactly 1.0.5's; no source touches a native module directly; runtime version, update URL, plugins and channel are pinned | `apps/mobile/__tests__/ota/otaCompatibility.test.ts` (fails if any of that changes; checked to catch a probe import of `lottie-react-native` / `react-native-maps`) |

Not verifiable offline, and worth the owner's one look before publishing:

- that the channel `production` still points at the branch `production`
  (`eas channel:view production`);
- which commit the live OTA was built from (`eas update:list --branch production`).
  An OTA replaces the WHOLE JavaScript bundle: it ships everything on the commit
  it is built from, not only this feature. Build it from the owner's current
  main with this branch merged, never from an older checkout.

## What an OTA may and may not carry

May: JavaScript and TypeScript changes, new screens and routes (expo-router
routes are JavaScript), new images or fonts bundled as assets, new calls to the
API, changes to `extra` values read at runtime.

May not (each needs a new store binary AND a new `runtimeVersion`, or the app
crashes or ignores it):

- a new package with native code, or a version change of one (expo modules,
  `react-native-*`, React Native itself, Expo SDK);
- a config plugin change, a permission, `Info.plist` / `AndroidManifest`
  entries, the icon, splash, bundle id, `userInterfaceStyle` (main's change of
  it to `Dark` has no effect through an OTA - noted in `RELEASE_CHECKLIST.md`);
- a change to `runtimeVersion` itself (a binary with `1.0.0` would never see it).

This change carries none of the second list. The test above keeps it that way.

## The fallback if an OTA were not possible

The server change is invisible to the installed app (it never calls the new
routes; the 137-endpoint contract test and the PostgreSQL compatibility check
prove the old calls answer byte for byte as before). So the fallback costs
nothing: the server ships, the app features stay dark, and they arrive with the
next store build (`eas build --profile production` for both platforms, then
review), with `version` bumped in `app.json` and `Info.plist`.

## How the app stays safe whatever the server says

- The new app on the OLD server (the OTA arriving first, or the server rolled
  back under it): every game-results call answers 404, measured by
  `scripts/game-results/startup-compat-check.mjs` step E; the store reads a 404
  as "nothing here" and shows nothing. None answers 401, which the api-client
  treats as a sign-out.
- 403 / 400 read as "nothing"; 500, a timeout (30 s client timeout) or a network
  error read as "error": the section hides or shows a quiet retry, never an
  alert. No store action rejects (`__tests__/stores/gameResultStore.test.ts`).
- Every server answer goes through a sanitizer that never throws; a fuzz test
  replaces every field of every answer with nulls, wrong types, NaN, unknown
  enums and junk objects (`__tests__/utils/gameResultsFuzz.test.ts`).
- Every new section and both new screens sit inside `SafeBoundary`: a render
  error hides that section only (`__tests__/components/gameResultsBoundaries.test.ts`
  walks the components and fails on a new unwrapped one).
- Not proven offline: an actual render of the screens. There is no React
  renderer in `node_modules` (`react-test-renderer@19.1.0` would be the
  download). A run on a simulator or a phone before publishing is the
  remaining check (below).

## Switches

- Server, no app update: `GameResults__ReadsEnabled=false` (DigitalOcean app
  environment variable) makes every read answer 404, including the Profile
  tab's stats-visibility; every new surface disappears on the next load.
  `GameResults__WritesEnabled=false` refuses new results.
- App: `GAME_RESULTS_ENABLED` in `stores/gameResultStore.ts` (needs an OTA to flip).

## Publishing (owner)

1. Server live and verified (`docs/GAME_RESULTS_RUNBOOK.md`), the first result
   published and read back on the API.
2. Merge this branch into current main locally; `tsc --noEmit` and `jest` pass.
3. Run the bundle once on a device or simulator against production
   (`expo start` or a preview build on the `preview` channel): open a past
   game with a result, a player sheet, the player screen, an organization,
   the leaderboard, the Profile tab.
4. `cd apps/mobile && npx eas-cli update --branch production --message "Game results"`.
   If the eas-cli version in use supports a staged rollout for updates
   (check `eas update --help`), start at a small percentage.
5. Confirm on a phone: force-quit twice, the profile line reads `js <new id>`.

## Rolling back a bad update (owner)

- Republish the previous update group to the same branch
  (`eas update:republish --group <previous group id>`, ids from
  `eas update:list --branch production`), or roll back to the binary's embedded
  bundle (`eas update:roll-back-to-embedded`, if the CLI in use has it). Phones
  pick it up on their next launch check.
- Faster for the feature alone: `GameResults__ReadsEnabled=false` on the server
  hides everything without touching the app.
- A bundle that crashes AT STARTUP is the one case the server switch cannot
  help: expo-updates falls back to the embedded bundle after a launch crash on
  that device, and a republish reaches the rest. This is why step 3 runs the
  bundle on a device before publishing.
