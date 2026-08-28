# Release Checklist

For the next production release. Written 2026-08-27; updated 2026-08-28 when
`expo-calendar` was replaced with a Google Calendar web link, which removed the
only native module that was blocking an over-the-air release.

## This release can be an OTA

`expo-calendar` (added 2026-08-21) was the one native module added since the
last store build, and `utils/calendar.ts` imported it at the top of a file the
event detail screen and info tab pull in directly — so an OTA would have
crashed every device on opening an event. It has been replaced with a Google
Calendar web link (`Linking.openURL`); no native module, no calendar
permission.

With it gone, the JS bundle imports only native modules already in the current
store build (`1.0.5`, shipped 2026-02-26): the roster-share modules
(`expo-file-system`, `expo-sharing`, `expo-clipboard`, `react-native-view-shot`,
added 2026-02-23) predate it. So an `eas update` at the pinned `runtimeVersion`
`"1.0.0"` is safe for everyone on `1.0.5`.

A store binary is still fine if you'd rather cut a fresh baseline — it's just no
longer required. If you do build natively, note the app.json/Info.plist
`userInterfaceStyle` was switched to `Dark` on this branch; confirm that's
intended before it ships (it has no effect on an OTA).

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
- Add-to-calendar via Google Calendar web link, D-League team affiliation
- Waitlist drag-reorder fix, org privacy with join approval
- Draft events, dark-mode date pickers
- Trophies: seven new types, rarity-ranked rows, tap-for-detail
- Waitlist promotion notification
- `js <update id>` on the profile screen (see Verifying below)

## Before shipping

- [ ] `yarn workspace @bhmhockey/mobile type-check` and `test` pass
- [ ] API deployed first (see below) - the app expects the current API
- [ ] OTA only: no `version` bump needed - the update id is how you tell
      builds apart (see Verifying). Bump `version` in **both** `app.json` and
      `ios/BHMHockey/Info.plist` (`CFBundleShortVersionString`) only if you cut
      a store binary; never touch `buildNumber` / `versionCode` (EAS
      auto-increments those).

## API

Migrations auto-apply on startup, and all of them are additive, so deploying
the API before the app is safe and is the right order. Nine migrations have
accumulated since February, through
`20260826231001_AddOrganizationDefaultStartAsDraft`.

- [ ] Deploy the API and confirm `/health` returns 200
- [ ] Spot-check that the new columns exist (org privacy, draft defaults,
      D-League team, waiver signature fields)

## Ship the update

```bash
cd apps/mobile
npx eas-cli update --branch production --message "Description"
```

Or, if cutting a store binary instead:

```bash
npx eas-cli build --platform ios --profile production
npx eas-cli submit --platform ios
```

## After the update has propagated

- [ ] Run both badge scripts against production:
      `docs/sql/add_badge_types_2026_08.sql` (five types) and
      `docs/sql/add_badge_types_holiday_games.sql` (Christmas, Halloween).
      Running them earlier would award badges whose icons aren't in the
      bundle yet, so holders would see a blank space. The icons ride the OTA
      as assets, so wait until devices have pulled the update (check the `js`
      line below) before running the SQL.

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
