# Release Checklist

For the next production release. Written 2026-08-27, when `main` had drifted
well past the last store build and one release could no longer be an OTA.

## This release must be a store binary

Not an `eas update`. `expo-calendar` was added on 2026-08-21 as a native
module, and `utils/calendar.ts` imports it at the top of the file - a file
the event detail screen and info tab import directly. `runtimeVersion` is
pinned to the fixed string `"1.0.0"`, so an OTA reaches every build back to
December, including every build without that native module. Those devices
would crash on opening any event.

The last store build predates `expo-calendar`, so this applies to every user.
Do not publish an OTA until a build carrying it has shipped and users are on
it.

## What this release carries

Everything merged since the last store build (app version has read `1.0.5`
since 2026-02-26):

- Org auto-roster, GroupMe chat links, waitlist visibility + payment guidance
- Guest player editing, home list dedup
- **Org legal waivers** + signature fields - already enforced server-side, so
  users on older bundles are blocked from registering with no way to sign.
  This release is what unblocks them.
- Registered-position display, organizer notifications (payment verified,
  player dropped), past-games view
- Calendar integration (the native module above), D-League team affiliation
- Waitlist drag-reorder fix, org privacy with join approval
- Draft events, dark-mode date pickers
- Trophies: five new types, rarity-ranked rows, tap-for-detail
- Waitlist promotion notification
- `js <update id>` on the profile screen (see Verifying below)

## Before building

- [ ] Bump `version` in **both** places (`1.0.5` → `1.0.6`):
      `apps/mobile/app.json` and
      `apps/mobile/ios/BHMHockey/Info.plist` (`CFBundleShortVersionString`)
- [ ] Do **not** touch `buildNumber` / `versionCode` - EAS auto-increments
      those on production builds
- [ ] `yarn workspace @bhmhockey/mobile type-check` and `test` pass
- [ ] API deployed first (see below) - the app expects the current API

## API

Migrations auto-apply on startup, and all of them are additive, so deploying
the API before the app is safe and is the right order. Nine migrations have
accumulated since February, through
`20260826231001_AddOrganizationDefaultStartAsDraft`.

- [ ] Deploy the API and confirm `/health` returns 200
- [ ] Spot-check that the new columns exist (org privacy, draft defaults,
      D-League team, waiver signature fields)

## Build and submit

```bash
cd apps/mobile
npx eas-cli build --platform ios --profile production
npx eas-cli submit --platform ios
```

- [ ] Confirm the build log shows the `ExpoCalendar` pod. The checked-in
      `ios/Podfile.lock` has no entry for it - EAS runs `pod install` and
      Expo autolinking should pick it up, but if it doesn't, the native
      module is missing from the binary and the calendar button crashes.
- [ ] `NSCalendarsWriteOnlyAccessUsageDescription` is in `Info.plist`
      (write-only on purpose - the app uses the OS-provided sheet and never
      reads the calendar)

## After users are on the new build

- [ ] Run `docs/sql/add_badge_types_2026_08.sql` against production. It
      creates the five new badge types. Running it earlier would award
      badges whose icons aren't in the shipped bundle, so holders would see
      a blank space.
- [ ] Remove the "next release must be a store binary" note from `CLAUDE.md`
      and `apps/mobile/CLAUDE.md` - OTAs are safe again once the calendar
      build is out.

## Verifying rollout

The profile screen's version line ends with `js <something>`:

| Reads | Means |
|-------|-------|
| `embedded` | Running the bundle baked into the store build - no OTA applied |
| `a1b2c3d4` | Took an over-the-air update; the id says which one |
| `dev` | Expo Go or a dev build (Metro serves the bundle) |

Ask a user for this line when a shipped feature appears to be missing on
their phone. Version, build number and runtime all come from the manifest
and read identically whether or not an update landed - this doesn't.

Note that OTA updates apply on the *next* cold start when the download takes
longer than `fallbackToCacheTimeout` (5s), and that "restart" has to be a
real force-quit; backgrounding and reopening doesn't re-check.
