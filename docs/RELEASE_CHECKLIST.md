# Release Checklist

Written 2026-08-27 for the release that followed the `1.0.5` store build;
updated 2026-08-28 when `expo-calendar` was replaced with a Google Calendar web
link, which removed the only native module blocking an over-the-air release.

**That release has shipped as an OTA.** What follows is the record of it and
the pattern to reuse next time - re-read the OTA caveat below before the next
one, because whether an OTA is safe at all depends on which native modules the
current store build already contains.

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

- [x] Badge types created in production - all seven of them. This was done
      with two throwaway SQL scripts, since nothing in the API could write a
      `BadgeTypes` row at the time; both have been deleted. New types now go
      in through `POST /organizations/{id}/badge-types`, so there is no SQL
      step to repeat.

      The ordering still matters for any *new* badge, however it is created:
      a badge whose icon isn't in a bundle devices have actually pulled shows
      up as a blank space in its holder's trophy case. Register the icon in
      `components/badges/BadgeIcon.tsx`, ship the bundle, confirm it landed
      (the `js` line below), and only then create the type and award it.

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
