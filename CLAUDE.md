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
yarn install && yarn dev    # Install + start API + Metro simultaneously

# Or separately:
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
- iOS Simulator: `http://localhost:5001/api`
- Android Emulator: `http://10.0.2.2:5001/api`
- Physical Device: `http://{YOUR_LOCAL_IP}:5001/api`
- Production: `https://bhmhockey-mb3md.ondigitalocean.app/api`

### Version Sync (Mobile)
EAS auto-increments `buildNumber` (iOS) and `versionCode` (Android) on production builds via `eas.json`. Do NOT manually bump those.

Only manually bump `version` (e.g., `1.0.4` → `1.0.5`) in TWO places when the user-visible version changes:
1. `apps/mobile/app.json` - `version`
2. `apps/mobile/ios/BHMHockey/Info.plist` - `CFBundleShortVersionString`

### Environment Variables
- `.env` at root - database password, JWT secret (gitignored)
- `.env.example` - template for required variables
- Never commit secrets

### Type Consistency
- Backend DTOs in C# must match TypeScript types in `@bhmhockey/shared`
- When adding fields to entities, update: backend DTO, shared types, any mappers

## App-Specific Docs

- **API patterns, gotchas, commands**: `apps/api/CLAUDE.md`
- **Mobile patterns, gotchas, components**: `apps/mobile/CLAUDE.md`

## OTA Updates

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

#### Step 4: Insert into Database
```sql
-- Create the badge type
INSERT INTO "BadgeTypes" (
  "Id", "Code", "Name", "Description", "IconName", "Category", "SortPriority", "CreatedAt"
) VALUES (
  gen_random_uuid(),
  'MY_BADGE',
  'My Badge Name',
  'Description shown in trophy case',
  'my_badge',  -- Must match iconMap key
  'special',
  10,
  NOW()
);

-- Award to a user (CelebratedAt = NULL triggers celebration modal)
INSERT INTO "UserBadges" (
  "Id", "UserId", "BadgeTypeId", "Context", "EarnedAt", "CelebratedAt"
) VALUES (
  gen_random_uuid(),
  'USER_ID_HERE',
  (SELECT "Id" FROM "BadgeTypes" WHERE "Code" = 'MY_BADGE'),
  '{"description": "Context shown in celebration"}',
  NOW(),
  NULL
);
```

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
