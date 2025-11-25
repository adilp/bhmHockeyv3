# claude.md - BHM Hockey Development Reference

## 📋 Project Status
**Status**: ✅ Infrastructure complete. Full-stack monorepo operational.
- Mobile app (Expo SDK 54, React Native) successfully connects to .NET 8 API
- Database: PostgreSQL on port 5433 (OrbStack)
- API running: `http://0.0.0.0:5001`

## 🏗️ Tech Stack
- **Mobile**: React Native (Expo 54), Expo Router, TypeScript, Zustand
- **API**: .NET 8, Entity Framework Core, PostgreSQL, JWT auth
- **Shared**: TypeScript packages (@bhmhockey/shared, @bhmhockey/api-client)
- **Deploy**: Digital Ocean App Platform

## 🚀 Quick Start Commands
```bash
# Install all dependencies
yarn install

# Start everything (API + Metro bundler)
yarn dev

# Just API
yarn api

# Just mobile
cd apps/mobile && npx expo start

# Run database migrations
cd apps/api/BHMHockey.Api && dotnet ef database update

# Test API health
curl http://localhost:5001/health
```

## 📁 Key File Locations

| Purpose | Location |
|---------|----------|
| Workspace config | `package.json` (root) |
| API entry point | `apps/api/BHMHockey.Api/Program.cs` |
| API dev config | `apps/api/BHMHockey.Api/appsettings.Development.json` |
| Mobile router | `apps/mobile/app/_layout.tsx` |
| API URL config | `apps/mobile/config/api.ts` |
| Shared types | `packages/shared/src/types/index.ts` |
| API client | `packages/api-client/src/client.ts` |
| Auth service | `packages/api-client/src/services/auth.ts` |

## 🎯 Architecture Overview

**Monorepo Structure:**
```
root/
├── apps/
│   ├── api/          # .NET 8 API server
│   └── mobile/       # React Native Expo app
├── packages/
│   ├── shared/       # Shared types, constants, utils
│   └── api-client/   # API client with auth interceptors
└── package.json      # Yarn workspaces config
```

**API Routing:**
- `/health` - Root level health check
- `/swagger` - Root level API docs
- `/api/*` - All controller endpoints (auth, organizations, events)

**Mobile Routing:**
- `(tabs)` - Main app tabs (home, discover, events, profile)
- `(auth)` - Auth flow (login, register) - NOT YET IMPLEMENTED

## ⚠️ Critical Gotchas

**Network Connectivity:**
- Physical device needs dev machine's local IP, NOT `localhost` (use `ifconfig | grep "inet "`)
- iOS Simulator: `localhost` works directly
- Android Emulator: Use `10.0.2.2` as host alias
- All devices must be on same WiFi network
- API must bind to `0.0.0.0` (all interfaces), not `127.0.0.1`

**Database:**
- PostgreSQL runs on port 5433 (not default 5432) in OrbStack
- Connection string in `appsettings.Development.json`
- User `bhmhockey` must own `public` schema
- Run migrations with: `dotnet ef database update`

**Expo/Metro:**
- Yarn version must be 1.22+ (check with `yarn --version`)
- Expo SDK (54) must match device's Expo Go version
- Missing assets (icon.png, splash.png) will block Metro bundler
- SDK version changes require full app restart, not just hot reload

**TypeScript Workspaces:**
- Import workspace packages as `"@bhmhockey/package-name"`
- Must configure both package.json AND tsconfig.json paths
- Path mapping alone doesn't work; must install package

**Don't:**
- ❌ Hardcode API URLs in components (use `getApiUrl()`)
- ❌ Commit .env files (they're .gitignored)
- ❌ Assume `postgres` user exists (check with `\du` in psql)
- ❌ Bind API to localhost for mobile development
- ❌ Call `/api/health` endpoint (it's at `/health` root level)

## 🔑 Key Patterns

**API Client Usage:**
```typescript
// api-client auto-initializes in mobile root layout
import { authService, eventService } from '@bhmhockey/api-client';

// Services handle auth headers and 401 auto-logout
const user = await authService.login(email, password);
```

**Environment Configuration:**
- Development: `appsettings.Development.json` + `launchSettings.json`
- Production: Digital Ocean App Platform environment variables
- Mobile: Detects `__DEV__` and Platform.OS automatically

**Platform-Specific URLs:**
```typescript
// config/api.ts handles all platform detection
import { getApiUrl, getBaseUrl } from '../config/api';
// getApiUrl() → http://[platform-specific]:5001/api
// getBaseUrl() → http://[platform-specific]:5001 (for /health, /swagger)
```

## 📊 Current API Endpoints (Work in Progress)

```
POST   /api/auth/register
POST   /api/auth/login
POST   /api/auth/logout
GET    /api/auth/me
GET    /api/organizations
POST   /api/organizations/{id}/subscribe
GET    /api/events
POST   /api/events/{id}/rsvp
```

Test endpoints via: `http://localhost:5001/swagger`

## 🎯 Immediate Next Steps

1. **Database Migrations** (5 min)
   ```bash
   cd apps/api/BHMHockey.Api
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

2. **Test Auth via Swagger** (10 min)
   - Open `http://localhost:5001/swagger`
   - Try POST `/api/auth/register` with test data
   - Copy JWT token, click "Authorize", paste token
   - Try GET `/api/auth/me` to verify

3. **Build Auth Screens** (2-3 hours next)
   - Create `apps/mobile/app/(auth)/login.tsx`
   - Create `apps/mobile/app/(auth)/register.tsx`
   - Add Zustand auth state store
   - Protect tab routes with auth check

4. **Then: Phase 1 Features**
   - Organization discovery + list
   - Events feed
   - RSVP functionality

## 🔗 Important Context Files

- `MONOREPO_GUIDE.md` - Full development guide (if you need deep details)
- `QUICK_START.md` - Fast setup for new developers
- `README.md` - Project overview

## 🧪 Validation Commands

```bash
# Verify workspace setup
yarn list @bhmhockey/shared
yarn list @bhmhockey/api-client

# Verify API is running and accessible
curl -v http://localhost:5001/health

# Verify database connection
psql -U bhmhockey -p 5433 -d bhmhockey -c "SELECT version();"

# Verify physical device connectivity (from phone browser)
http://[YOUR_LOCAL_IP]:5001/health

# Verify TypeScript compilation
yarn workspaces foreach run build  # (if build script exists)
```



