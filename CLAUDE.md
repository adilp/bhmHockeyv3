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
Update TWO places when bumping versions:
1. `apps/mobile/app.json` - version, buildNumber, versionCode
2. `apps/mobile/ios/BHMHockey/Info.plist` - CFBundleShortVersionString, CFBundleVersion

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
