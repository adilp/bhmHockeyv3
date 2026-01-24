# Architecture

**Analysis Date:** 2026-01-24

## Pattern Overview

**Overall:** Layered monorepo with distributed mobile + API tiers.

**Key Characteristics:**
- Clean separation between mobile (React Native/Expo Router) and API (.NET 8)
- Shared types in `@bhmhockey/shared` package prevent DTO/type mismatches
- Zustand state management on mobile with API client layer
- Service-oriented API architecture with dependency injection
- Authentication via JWT with refresh token persistence on mobile

## Layers

**Frontend (Mobile):**
- Purpose: React Native Expo application with navigation and UI
- Location: `apps/mobile/app/` (Expo Router screens), `apps/mobile/components/`, `apps/mobile/stores/`
- Contains: Screen components (using Expo Router layout structure), reusable components, Zustand stores
- Depends on: `@bhmhockey/api-client`, `@bhmhockey/shared`, Expo modules
- Used by: End users on iOS/Android

**Shared Types Package:**
- Purpose: Single source of truth for request/response types and constants
- Location: `packages/shared/src/types/`, `packages/shared/src/constants/`
- Contains: TypeScript interfaces matching backend DTOs exactly, skill level enums, event visibility constants
- Depends on: None (pure TypeScript)
- Used by: Mobile app and API client

**API Client Layer:**
- Purpose: HTTP client wrapper with authentication, token management, API service methods
- Location: `packages/api-client/src/`
- Contains: Axios instance with auth interceptor, `authService`, `eventService`, `organizationService`, `userService`, `notificationService`
- Depends on: `@bhmhockey/shared`, axios, async-storage for token persistence
- Used by: Mobile app stores

**API Backend:**
- Purpose: REST API serving all mobile data and operations
- Location: `apps/api/BHMHockey.Api/`
- Contains: Controllers, Services, Data models, Entity Framework migrations, background jobs
- Depends on: Entity Framework Core, PostgreSQL, JWT auth, Expo Push API
- Used by: Mobile app via HTTP

**Database:**
- Purpose: Persistent storage for users, events, organizations, tournaments, registrations, notifications
- Location: PostgreSQL (hosted on DigitalOcean in production, local via OrbStack in dev)
- Contains: 17 entity tables with relationships
- Depends on: AppDbContext (EF Core)

## Data Flow

**User Authentication:**

1. User enters credentials on `/(auth)/login` screen
2. Component calls `useAuthStore.login()` (Zustand action)
3. Store calls `authService.login()` (from API client)
4. API client makes POST to `/api/auth/login` with credentials
5. API (AuthController → AuthService) validates, returns JWT token and UserDto
6. Client stores token in AsyncStorage via `authStorage.saveToken()`
7. Store updates state: `user`, `isAuthenticated = true`
8. App redirects to `/(tabs)` via auth check in `app/index.tsx`

**Event Registration Flow:**

1. User on event detail screen clicks "Register" button
2. Component calls `eventStore.registerForEvent(eventId)` (Zustand action)
3. Store optimistically updates state: `processingEventId = eventId` (prevents double-click)
4. Store calls `eventService.registerForEvent(eventId)` (API client)
5. API client adds JWT header automatically (auth interceptor)
6. API (EventsController → EventService) checks authorization, creates EventRegistration
7. Response returns updated EventDto with registration status
8. Store updates `events` array with new registration, clears `processingEventId`
9. Component re-renders showing "Registered" state
10. If API call fails, interceptor catches 401 and triggers logout via Zustand

**Push Notification Reception:**

1. App launches and registers push token via `registerForPushNotificationsAsync()` (called in `app/_layout.tsx`)
2. Token sent to API via `savePushTokenToBackend(token)` (EventReminderService stores it)
3. When event reminder triggers or notification event occurs, backend sends via Expo Push API
4. Notification listener (in `_layout.tsx`) receives foreground notifications
5. Handler parses data, routes to appropriate screen (e.g., event detail)

**State Management:**

- Zustand stores are singletons holding async data: users, events, organizations, tournaments
- Components use selector pattern: `useAuthStore(state => state.user)` to prevent unnecessary re-renders
- Stores handle ALL API calls - components never call API client directly
- Async operations show `isLoading` flag; errors are stored and displayed via toast/alerts

## Key Abstractions

**Zustand Store Pattern:**
- Purpose: Centralized state with actions that manage both UI and API state
- Examples: `useAuthStore` (`apps/mobile/stores/authStore.ts`), `useEventStore`, `useTournamentStore`
- Pattern: Create via `create<StateInterface>()`, actions as async functions, selectors to prevent re-renders

**Service/Interface Pairs (API):**
- Purpose: Separate interface from implementation for testability
- Examples: `IEventService`/`EventService`, `IAuthService`/`AuthService`, `ITournamentAdminService`/`TournamentAdminService`
- Pattern: Interface in separate file, implementation inherits from interface, registered in DI container

**Expo Router Layout Structure:**
- Purpose: File-based routing with group layouts for navigation tabs and auth flow
- Examples: `(auth)` group contains login/register, `(tabs)` group for bottom tab navigation
- Pattern: Folders in parentheses define layout groups, `_layout.tsx` files define navigation structure

**Authorization Service Layer:**
- Purpose: Centralize permission checks to avoid logic duplication and inconsistency
- Examples: `IOrganizationAdminService.IsUserAdminAsync()`, `ITournamentAdminService`
- Pattern: Services methods return boolean or throw `UnauthorizedAccessException`

**Badge Celebration Modal:**
- Purpose: Queue uncelebrated badges and show one-at-a-time celebration modal
- Examples: `celebrationStore.ts`, `BadgeCelebrationModal.tsx`, `useBadgeCelebration()` hook
- Pattern: Store tracks `isShowingCelebration` and `currentBadge`, modal dismissal fetches next badge

## Entry Points

**Mobile App Root:**
- Location: `apps/mobile/app/_layout.tsx`
- Triggers: App launch
- Responsibilities: Initialize API client, set up push notification listeners, deep link handlers, theme provider (SafeAreaProvider), global font scaling disable, render Root Stack navigator

**Mobile Auth Check:**
- Location: `apps/mobile/app/index.tsx`
- Triggers: App navigation before any screen renders
- Responsibilities: Call `useAuthStore.checkAuth()` to verify token validity, show loading spinner, redirect to login or tabs based on auth state

**Mobile Tab Navigation:**
- Location: `apps/mobile/app/(tabs)/_layout.tsx`
- Triggers: Authenticated user enters app
- Responsibilities: Define bottom tab bar with Home, Discover, Events, Notifications, Profile, Tournaments tabs

**API Server Startup:**
- Location: `apps/api/BHMHockey.Api/Program.cs`
- Triggers: `dotnet watch run` or deployment
- Responsibilities: Configure EF Core context with PostgreSQL, register services in DI, configure JWT auth, set up CORS, apply migrations, start Kestrel server on port 5001

**API Swagger Documentation:**
- Location: Automatically generated from controller XML comments
- Triggers: GET `/swagger`
- Responsibilities: Interactive API documentation for all endpoints

## Error Handling

**Strategy:** Services throw exceptions which propagate to controllers; controllers catch and map to HTTP responses.

**Patterns:**

- **InvalidOperationException**: Business rule violations (event full, insufficient balance, etc.) → 400 Bad Request
- **UnauthorizedAccessException**: Permission denied (not org admin, tournament admin, etc.) → 401 Unauthorized
- **ArgumentException**: Invalid input parameters → 400 Bad Request
- **API Client 401**: Caught by axios interceptor → triggers `onAuthError` callback → clears Zustand auth state → user logged out
- **API Client Network Error**: Caught by stores → `errorMessage` set in state → component displays toast/alert
- **Mobile Component Errors**: React error boundary (if implemented) or fallback to login on unhandled auth errors

## Cross-Cutting Concerns

**Logging:**
- Backend: Console.WriteLine for startup/migrations (no structured logging framework)
- Mobile: console.log with emoji prefixes (🔍, 🔒, 🔗, 📦, 🎉) for visibility during dev

**Validation:**
- Backend: Null checks, max length constraints on entities, custom validators in controllers
- Mobile: FormInput components with validators, PositionSelector for constrained choices
- Shared: Constants for valid enum values (skill levels, event visibility, payment status)

**Authentication:**
- Backend: JWT bearer token validation in Program.cs, `[Authorize]` attributes on protected endpoints
- Mobile: Token stored in AsyncStorage, auto-added to all requests via API client interceptor
- Refresh: Not explicitly implemented; token expires in 60 minutes and logout clears state

**Database Migrations:**
- EF Core migrations in `apps/api/BHMHockey.Api/Migrations/`
- Auto-applied on API startup (Program.cs calls `Database.Migrate()`)
- Only additive migrations; never drop columns

**Push Notifications:**
- Backend: EventReminderService stores push tokens, sends via Expo Push API
- Mobile: Listeners in `_layout.tsx` stay mounted entire app lifecycle, handlers route notifications to screens

---

*Architecture analysis: 2026-01-24*
