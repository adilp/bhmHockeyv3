# BHM Hockey - Monorepo

## Status

Phase 4 Complete + Multi-Admin + Notification Center | Ready for Phase 5 (Waitlist)

## Tech Stack

- **Mobile**: React Native (Expo SDK 54), Expo Router, Zustand
- **API**: .NET 8, Entity Framework Core, PostgreSQL
- **Shared**: `@bhmhockey/shared` (types), `@bhmhockey/api-client` (API client)
- **Deploy**: DigitalOcean App Platform
- **Package Manager**: Yarn workspaces

## Quick Start

```bash
yarn install && yarn dev    # Install + auto-start Postgres (Docker) + API + Metro

# Or separately:
yarn db                     # Postgres 16 via docker compose (port 5433)
yarn api                    # API on port 5001 (auto-migrations)
yarn mobile                 # Metro bundler on port 8081

curl http://localhost:5001/health    # Verify API
open http://localhost:5001/swagger   # API docs
```

## Monorepo Structure

```
root/
├── apps/
│   ├── api/                 # .NET 8 API (see apps/api/CLAUDE.md)
│   └── mobile/              # React Native Expo (see apps/mobile/CLAUDE.md)
├── packages/
│   ├── shared/              # Shared TypeScript types matching backend DTOs
│   └── api-client/          # Axios client with auth interceptors
└── package.json             # Yarn workspaces config
```

## Data Flow

```
Mobile Component
  → Zustand Store Action
    → API Client (auto-adds auth header)
      → .NET Service → EF Core → PostgreSQL
        → Response flows back
          → Store updates state → Component re-renders
```

## Shared Packages

### `@bhmhockey/shared`
- TypeScript types that mirror backend DTOs exactly
- Constants: skill levels, positions, validation rules
- Import: `import { User, EventDto, SkillLevel } from '@bhmhockey/shared'`

### `@bhmhockey/api-client`
- Axios instance with auth token interceptor
- Services: `authService`, `eventService`, `organizationService`, `userService`, `notificationService`
- Auto-adds `Authorization: Bearer {token}` to all requests
- 401 responses trigger `onAuthError` callback (auto-logout)

## Cross-Cutting Gotchas

### API URLs by Platform
Set via `EXPO_PUBLIC_API_URL` in `apps/mobile/.env` (gitignored, see `apps/mobile/.env.example`); defaults to production when unset. Restart Metro after changing it.
- iOS Simulator: `http://localhost:5001/api`
- Android Emulator: `http://10.0.2.2:5001/api`
- Physical Device: `http://{YOUR_LOCAL_IP}:5001/api`
- Production (default): `https://bhmhockey-mb3md.ondigitalocean.app/api`

### Version Sync (Mobile)
EAS auto-increments `buildNumber` (iOS) and `versionCode` (Android) on production builds via `eas.json`. Do NOT manually bump those.

Only manually bump `version` (e.g., `1.0.4` → `1.0.5`) in TWO places when the user-visible version changes:
1. `apps/mobile/app.json` - `version`
2. `apps/mobile/ios/BHMHockey/Info.plist` - `CFBundleShortVersionString`

### Environment Variables
- The API does NOT use dotenv `.env` files — local config lives in `appsettings.Development.json`; production env vars are declared in `.do/app.yaml` and set in the DigitalOcean console
- `apps/mobile/.env` - `EXPO_PUBLIC_API_URL` for local dev (gitignored, see `apps/mobile/.env.example`)
- Never commit secrets

### Type Consistency
- Backend DTOs in C# must match TypeScript types in `@bhmhockey/shared`
- When adding fields to entities, update: backend DTO, shared types, any mappers

## App-Specific Docs

- **API patterns, gotchas, commands**: `apps/api/CLAUDE.md`
- **Mobile patterns, gotchas, components**: `apps/mobile/CLAUDE.md`

## OTA Updates

The next production release can ship as an OTA. "Add to Calendar" uses a
Google Calendar web link (`apps/mobile/utils/calendar.ts`), not a native
module, so the JS bundle imports only native modules already present in the
current store build (1.0.5). An `eas update` at the pinned `runtimeVersion`
"1.0.0" therefore reaches older builds without crashing them.

> Before adding a native module or changing native config, remember
> `runtimeVersion` is a fixed string — an OTA reaches every build that shares
> it, so a JS bundle importing a native module absent from a user's binary
> crashes it on load. Ship those changes as a store build first, and only OTA
> once users are on it. (This is exactly why `expo-calendar` was removed in
> favor of the web link.) Full steps: `docs/RELEASE_CHECKLIST.md`.

```bash
cd apps/mobile
npx eas-cli update --branch production --message "Description"
```

OTA updates JS/styles/assets. Native changes need new build.

## Native Builds

```bash
cd apps/mobile
npx eas-cli build --platform ios --profile production
npx eas-cli submit --platform ios
```

## Development Workflow

1. Backend first - model, migration, service, controller
2. Test via Swagger
3. Frontend - Zustand store action, then UI
4. Test on physical device for networking issues

## Badge Assets

### Adding a New Badge (Full Workflow)

#### Step 1: Create Source Image
- Create badge at **512x512 or 1024x1024** in your design tool
- Export as PNG with **transparent background**
- Save source to `apps/mobile/assets/badges/` (e.g., `my_badge_source.png`)

#### Step 2: Resize for App
```bash
convert apps/mobile/assets/badges/my_badge_source.png \
    -resize 288x288 \
    -gravity center \
    -background none \
    -extent 288x288 \
    apps/mobile/assets/badges/my_badge.png
```

#### Step 3: Register in Code
Add to icon map in `apps/mobile/components/badges/BadgeIcon.tsx`:
```typescript
const iconMap: Record<string, ReturnType<typeof require>> = {
  founding_member: require('../../assets/badges/founding_member.png'),
  my_badge: require('../../assets/badges/my_badge.png'),  // Add here
};
```

#### Step 4: Create the Type and Award It (API)

Both are org-admin endpoints - no SQL, in any environment. `{orgId}` is any
organization the caller administers; badge types are global, so which one you
pass only establishes that you're an admin.

```bash
# Create the type. iconName MUST match an iconMap key (Step 3), and the bundle
# carrying the artwork must already be on devices - otherwise every holder
# sees a blank space where the badge should be.
curl -X POST "$API/organizations/$ORG_ID/badge-types" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"code":"MY_BADGE","name":"My Badge Name",
       "description":"Shown in the trophy case","iconName":"my_badge",
       "category":"special","sortPriority":10}'

# Award it. Any active, non-ghost account is eligible, up to 200 per call.
# occasion is the line shown under the badge in the celebration and detail views.
curl -X POST "$API/organizations/$ORG_ID/badges/award" \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"badgeCode":"MY_BADGE","userIds":["<guid>","<guid>"],
       "occasion":"AMP Ironman Game - July 25, 2026"}'
```

Awarding is idempotent and partial by design: the response reports each id as
`awarded`, `already_held` or `unknown_user` (guests and deleted accounts read
as unknown) instead of failing the whole batch over one bad id. New awards land
with `CelebratedAt = NULL`, which is what triggers the celebration modal.

Mistakes come off with `DELETE /organizations/{orgId}/badges/{userBadgeId}`.

#### Step 5: Clear Cache & Test
```bash
cd apps/mobile && npx expo start --clear
```

### Why 288x288?
| Location | Display Size | On @3x Device |
|----------|--------------|---------------|
| Celebration Modal | 96px | 288px needed |
| Trophy Case | 48px | 144px needed, crisp |
| Badge Row (roster) | 24px | Downscaled, crisp |

288px ensures crisp display at the largest size (96px celebration modal on @3x retina screens).
