# Codebase Structure

**Analysis Date:** 2026-01-24

## Directory Layout

```
bhmhockey/                          # Monorepo root
├── apps/
│   ├── api/                        # .NET 8 REST API
│   │   └── BHMHockey.Api/
│   │       ├── Controllers/        # HTTP endpoints (Auth, Events, Users, Orgs, Tournaments, Notifications)
│   │       ├── Services/           # Business logic with I*Service interfaces
│   │       │   └── Background/     # WaitlistBackgroundService, NotificationCleanupBackgroundService
│   │       ├── Models/
│   │       │   ├── Entities/       # Database entities (User, Organization, Event, Tournament, etc.)
│   │       │   ├── DTOs/           # Request/response objects by domain
│   │       │   └── Exceptions/     # Custom exceptions (ConcurrentModificationException)
│   │       ├── Data/
│   │       │   └── AppDbContext.cs # EF Core DbContext with all table configs
│   │       ├── Migrations/         # EF Core migration files (auto-applied on startup)
│   │       └── Program.cs          # Startup, DI registration, middleware pipeline
│   └── mobile/                     # React Native Expo app
│       ├── app/                    # Expo Router screens (file-based routing)
│       │   ├── (auth)/             # Auth layout group (login, register)
│       │   ├── (tabs)/             # Tab navigation group (home, discover, events, notifications, profile, tournaments)
│       │   ├── events/             # Event screens ([id], create, edit, detail tabs)
│       │   ├── organizations/      # Organization screens ([id], create, edit, settings)
│       │   ├── tournaments/        # Tournament screens ([id] with bracket, schedule, standings, register)
│       │   │                       # Manage sub-routes (settings, questions, registrations, teams, admins, standings, audit)
│       │   ├── admin/              # Admin panel screens
│       │   ├── settings/           # User settings screens
│       │   ├── index.tsx           # Root entry - auth check and redirect
│       │   └── _layout.tsx         # Root layout - API client init, push notifications, deep links, root Stack navigator
│       ├── components/             # Reusable UI components (imported from index.ts barrel export)
│       │   ├── event-detail/       # EventInfoTab, EventRosterTab, EventChatTab, RegistrationFooter
│       │   ├── tournaments/        # TournamentCard, TournamentGameCard, BracketMatchBox
│       │   ├── badges/             # BadgeIcon, BadgeCelebrationModal, TrophyCase
│       │   ├── bracket/            # Bracket rendering components
│       │   └── animations/         # Reanimated animation components
│       ├── stores/                 # Zustand state management
│       │   ├── authStore.ts        # User auth state, login/logout/checkAuth actions
│       │   ├── eventStore.ts       # Events list, fetch, create, register, cancel
│       │   ├── organizationStore.ts # Organizations list, fetch, create
│       │   ├── tournamentStore.ts  # Tournaments list, bracket, standings, match details
│       │   ├── tournamentTeamStore.ts # Team creation/editing for tournaments
│       │   ├── notificationStore.ts # User notifications/alerts
│       │   └── celebrationStore.ts # Uncelebrated badges queue and modal state
│       ├── theme/                  # Design tokens (colors, spacing, radius, typography)
│       ├── hooks/                  # Custom React hooks (useBadgeCelebration, useOtaUpdates, etc.)
│       ├── utils/                  # Utilities (notifications, deep links, sharing, date formatting)
│       ├── config/                 # Configuration (api.ts with API base URL)
│       ├── assets/                 # Images (badges, app icons, splash)
│       │   └── badges/             # Badge PNG files (288x288 for @3x screens)
│       ├── __tests__/              # Jest unit/integration tests
│       │   └── stores/             # Store tests
│       ├── __mocks__/              # Jest mocks for API client
│       ├── app.json                # Expo app config (version, buildNumber, plugins)
│       └── package.json            # Mobile app dependencies (Expo, React Native, Zustand, axios)
├── packages/
│   ├── shared/                     # Shared TypeScript types
│   │   ├── src/
│   │   │   ├── types/              # TypeScript interfaces (User, Event, Organization, Tournament, DTOs)
│   │   │   ├── constants/          # Enums and constants (SkillLevel, EventVisibility, PaymentStatus)
│   │   │   ├── utils/              # Shared utilities (type guards, formatters)
│   │   │   └── index.ts            # Barrel export
│   │   ├── __tests__/              # Jest tests
│   │   └── package.json
│   └── api-client/                 # Axios HTTP client with auth
│       ├── src/
│       │   ├── services/           # API service classes (authService, eventService, etc.)
│       │   ├── auth/               # Token storage (authStorage) using AsyncStorage
│       │   └── index.ts            # Export initializeApiClient, all services
│       ├── __tests__/              # Jest tests with mocked axios
│       └── package.json
├── docs/                           # Documentation
│   ├── plans/                      # Implementation plans
│   ├── MONOREPO_GUIDE.md
│   ├── PRD.md
│   ├── TOURNAMENT_TESTING_PLAN.md
│   └── ...
├── scripts/                        # Utility scripts (badge assignment, notifications)
│   ├── send-event-push-notifications.js
│   └── assign-badge.sh
├── .planning/                      # GSD planning documents
│   └── codebase/                   # Architecture, structure, conventions, testing, concerns
├── CLAUDE.md                       # Root monorepo documentation (quick start, gotchas)
├── package.json                    # Root workspaces config, shared scripts (yarn dev, yarn api, yarn mobile)
├── yarn.lock                       # Yarn dependencies lock
├── tsconfig.json                   # Root TypeScript config
├── app.json                        # Root Expo config
└── eas.json                        # EAS build config for native builds
```

## Directory Purposes

**`apps/api/BHMHockey.Api/Controllers/`:**
- Purpose: HTTP endpoint handlers, request validation, response formatting
- Contains: 6 controller classes (AuthController, EventsController, UsersController, OrganizationsController, TournamentsController, NotificationsController)
- Key files: `AuthController.cs` (login, register, logout), `EventsController.cs` (CRUD + registration), `TournamentsController.cs` (complex tournament management)
- Pattern: Endpoints marked with `[Authorize]` require JWT, extract user ID from claims, call services

**`apps/api/BHMHockey.Api/Services/`:**
- Purpose: Business logic, authorization checks, data transformations, API integration
- Contains: 15+ service files with 1 interface + 1 implementation pattern
- Key files: `AuthService.cs` (user auth, JWT token generation), `EventService.cs` (event CRUD, registration logic), `OrganizationAdminService.cs` (permission checks)
- Pattern: Services are registered as singletons/scoped in DI, depend on DbContext, throw exceptions on errors

**`apps/api/BHMHockey.Api/Models/Entities/`:**
- Purpose: EF Core entity definitions matching database tables
- Contains: 17 entities (User, Organization, Event, EventRegistration, Tournament, TournamentTeam, TournamentMatch, BadgeType, UserBadge, Notification, etc.)
- Key files: `User.cs` (identity + skill levels + positions), `Tournament.cs` (tournament root), `TournamentMatch.cs` (match outcomes)
- Pattern: Each has Id (Guid), timestamps (CreatedAt, UpdatedAt), relationships via navigation properties

**`apps/api/BHMHockey.Api/Models/DTOs/`:**
- Purpose: Request/response objects for API contracts
- Contains: Grouped by domain (UserDTOs.cs, EventDTOs.cs, TournamentDTOs.cs, etc.) with Create/Update/Detail variants
- Key files: `EventDTOs.cs` (EventCreateRequest, EventDto, EventDetailDto with registration status), `TournamentDTOs.cs` (tournament detail with brackets)
- Pattern: DTOs mirror shared types in `@bhmhockey/shared`; mappers in services convert entities to DTOs

**`apps/api/BHMHockey.Api/Data/AppDbContext.cs`:**
- Purpose: EF Core context with all table configurations
- Contains: DbSet declarations for all 17 entities, OnModelCreating configurations (indexes, constraints, JSON columns)
- Key pattern: JSONB columns for Positions dict, JSON for custom tournament questions; InMemory vs PostgreSQL conditional config

**`apps/api/BHMHockey.Api/Migrations/`:**
- Purpose: Track database schema changes
- Contains: Numbered migration files (e.g., `20241205000000_InitialCreate.cs`, `20241220000000_AddTournaments.cs`)
- Key pattern: Auto-applied on startup; always additive (no drops); migrations use PascalCase table/column names

**`apps/mobile/app/`:**
- Purpose: Expo Router screens organized by domain
- Contains: 49+ TSX files using file-based routing
- Key patterns:
  - `[id]` routes for dynamic segments
  - `_layout.tsx` files define navigation structure and headers
  - Screen components import from `stores` for data and actions
  - All screens use TypeScript for type safety

**`apps/mobile/components/`:**
- Purpose: Reusable UI components with dark theme
- Contains: EventCard, OrgCard, Badge, FormInput, EventForm, OrgForm, DraggableRoster, TournamentCard, BracketMatchBox, etc.
- Key pattern: Export all from `components/index.ts` barrel file; import via `import { ComponentName } from '../../components'`
- Styling: Use `colors`, `spacing`, `radius` from theme; set `placeholderTextColor`, `allowFontScaling={false}` explicitly

**`apps/mobile/stores/`:**
- Purpose: Zustand state management for data + async operations
- Contains: 7 store files (auth, event, organization, tournament, notification, celebration, tournamentTeam)
- Key pattern:
  - Store created via `create<StateInterface>()` hook
  - Actions are async functions calling API client services
  - Selectors used in components: `useAuthStore(state => state.user)` to prevent re-renders
  - Error handling: catch errors, set `errorMessage`, components display via alerts

**`apps/mobile/theme/`:**
- Purpose: Centralized design tokens
- Contains: Colors (bg, text, status, primary), spacing (xs-xl), radius, typography (fonts, sizes)
- Key pattern: Import as `import { colors, spacing, radius } from '../../theme'`, never hardcode values

**`apps/mobile/utils/`:**
- Purpose: Utilities and helpers
- Contains: `notifications.ts` (push notification setup, registration, handlers), `deepLinks.ts` (deep link routing), `sharing.ts`, `venmoLinks.ts`, date formatting
- Key pattern: Utilities are stateless functions or managers; some return refs (e.g., listeners for cleanup)

**`apps/mobile/config/`:**
- Purpose: Environment-specific configuration
- Contains: `api.ts` with `getApiUrl()` function
- Current state: Hardcoded to production URL (gotcha: must change for local dev)
- Key pattern: Should use platform detection (iOS simulator, Android emulator, physical device)

**`packages/shared/src/types/`:**
- Purpose: Single source of truth for all type definitions
- Contains: User, Organization, Event, EventRegistration, Tournament, Badge, Notification, etc.
- Key pattern: Types in shared exactly mirror backend DTOs and C# entities
- Rule: When adding fields to backend entities, update shared types + all DTO mapping sites

**`packages/shared/src/constants/`:**
- Purpose: Enum-like constants and validation rules
- Contains: SkillLevel, EventVisibility, EventStatus, PaymentStatus, Position keys, role names
- Key pattern: Export as type + const values; used for validation and UI dropdowns

**`packages/api-client/src/services/`:**
- Purpose: API methods grouped by domain
- Contains: authService (login, register, logout, getCurrentUser), eventService, organizationService, userService, notificationService
- Key pattern: All methods use axios instance with auto-added JWT header, throw on error
- Error mapping: 401 responses caught by interceptor, triggers `onAuthError` callback

**`packages/api-client/src/auth/`:**
- Purpose: Token storage using AsyncStorage
- Contains: `authStorage` class with getToken, saveToken, removeToken methods
- Key pattern: Called by authService after login, auto-loaded on app boot in `checkAuth()`

## Key File Locations

**Entry Points:**

- `apps/mobile/app/index.tsx`: Mobile app auth check and redirect
- `apps/mobile/app/_layout.tsx`: Root layout with API client init, notification setup, Stack navigator
- `apps/api/BHMHockey.Api/Program.cs`: API server startup, DI registration, middleware

**Configuration:**

- `apps/mobile/app.json`: Expo app version, plugins, iOS/Android config
- `apps/api/BHMHockey.Api/appsettings.Development.json`: Database connection, JWT secret (local dev)
- `apps/api/BHMHockey.Api/appsettings.json`: Production config template
- `packages/shared/src/constants/index.ts`: Skill levels, event visibility, validation rules
- `apps/mobile/config/api.ts`: API base URL configuration

**Core Logic:**

- `apps/mobile/stores/authStore.ts`: Authentication state and actions
- `apps/mobile/stores/eventStore.ts`: Event CRUD and registration
- `apps/mobile/stores/tournamentStore.ts`: Tournament data and bracket management
- `apps/api/BHMHockey.Api/Services/AuthService.cs`: User authentication, JWT generation
- `apps/api/BHMHockey.Api/Services/EventService.cs`: Event creation, registration, status updates
- `apps/api/BHMHockey.Api/Services/OrganizationAdminService.cs`: Admin permission checks

**Testing:**

- `apps/mobile/__tests__/stores/`: Jest tests for Zustand stores
- `apps/mobile/__mocks__/`: Mocked API client for testing
- `packages/shared/__tests__/`: Type and constant tests
- `packages/api-client/__tests__/`: API client integration tests
- `apps/api/BHMHockey.Api.Tests/`: .NET unit tests (213+ tests)

## Naming Conventions

**Files:**

- React components: PascalCase `EventCard.tsx`, `FormInput.tsx`
- Store files: camelCase `authStore.ts`, `eventStore.ts`
- Screens (Expo Router): kebab-case or PascalCase matching route structure
- API services: camelCase `authService.ts`, `eventService.ts`
- C# controllers/services: PascalCase `AuthController.cs`, `EventService.cs`
- C# DTOs: PascalCase with Dto/Request/Response suffix `EventCreateRequest.cs`, `UserDto.cs`

**Directories:**

- Feature groups: kebab-case `event-detail/`, `tournaments/`
- Domain groups: lowercase plural `components/`, `stores/`, `services/`
- Route groups (Expo Router): parentheses `(auth)/`, `(tabs)/`

**TypeScript Variables:**

- Component props: PascalCase `const { eventId, onDismiss } = props`
- Store selectors: camelCase `const user = useAuthStore(state => state.user)`
- API calls: camelCase `const eventDto = await eventService.getById(id)`
- Event handlers: camelCase `const handlePress = () => {}`

**C# Conventions:**

- Classes/Methods: PascalCase `UserService`, `GetByIdAsync`
- Parameters/local variables: camelCase `userId`, `eventId`
- Interfaces: I-prefix `IAuthService`, `IEventService`
- Private fields: `_fieldName`
- DTOs: Suffix `Dto`, `Request`, `Response` (e.g., `EventDetailDto`, `CreateEventRequest`)

## Where to Add New Code

**New Feature (e.g., new domain like "Sponsorships"):**

Backend (API):
1. Create entity: `apps/api/BHMHockey.Api/Models/Entities/Sponsorship.cs`
2. Create DTOs: `apps/api/BHMHockey.Api/Models/DTOs/SponsorshipDTOs.cs` with Create, Update, Detail variants
3. Add DbSet: `apps/api/BHMHockey.Api/Data/AppDbContext.cs` → add `public DbSet<Sponsorship>`
4. Create migration: `yarn api:migrations SponsorshipEntity`
5. Create service interface + implementation: `apps/api/BHMHockey.Api/Services/ISponsorshipService.cs`, `SponsorshipService.cs`
6. Register in DI: `apps/api/BHMHockey.Api/Program.cs` → `builder.Services.AddScoped<ISponsorshipService, SponsorshipService>()`
7. Create controller: `apps/api/BHMHockey.Api/Controllers/SponsorshipsController.cs`
8. Write tests: `apps/api/BHMHockey.Api.Tests/Services/SponsorshipServiceTests.cs`

Frontend (Mobile):
1. Create types: `packages/shared/src/types/sponsorship.ts` with `Sponsorship`, `CreateSponsorshipRequest`, `SponsorshipDto`
2. Export from shared: `packages/shared/src/types/index.ts` → `export * from './sponsorship'`
3. Create API client service: `packages/api-client/src/services/sponsorshipService.ts`
4. Export from api-client: `packages/api-client/src/index.ts` → `export { sponsorshipService } from './services/sponsorshipService'`
5. Create Zustand store: `apps/mobile/stores/sponsorshipStore.ts` (fetch, create, update, delete actions)
6. Create screens: `apps/mobile/app/sponsorships/index.tsx` (list), `[id].tsx` (detail), `create.tsx` (form)
7. Add route to root Stack: `apps/mobile/app/_layout.tsx` → `<Stack.Screen name="sponsorships/[id]" />`
8. Create components: `apps/mobile/components/SponsorshipCard.tsx`, `SponsorshipForm.tsx`
9. Export from components: `apps/mobile/components/index.ts` → `export { SponsorshipCard } from './SponsorshipCard'`
10. Write tests: `apps/mobile/__tests__/stores/sponsorshipStore.test.ts`

**New Component:**

Location: `apps/mobile/components/{FeatureName}.tsx` or `components/{category}/{FeatureName}.tsx` if part of larger feature
Pattern:
```typescript
import { View, Text, StyleSheet, Pressable } from 'react-native';
import { colors, spacing, radius } from '../theme';

interface Props {
  title: string;
  onPress?: () => void;
}

export function MyComponent({ title, onPress }: Props) {
  return (
    <Pressable onPress={onPress} style={styles.container}>
      <Text style={styles.title}>{title}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  container: { padding: spacing.md, backgroundColor: colors.bg.dark, borderRadius: radius.lg },
  title: { color: colors.text.primary, fontSize: 16 },
});
```
Export from: `apps/mobile/components/index.ts` → `export { MyComponent } from './MyComponent'`
Import in screens: `import { MyComponent } from '../../components'`

**New Screen:**

Location: `apps/mobile/app/{feature}/{screen}.tsx`
Pattern: Use Expo Router `useLocalSearchParams<{ paramName: string }>()` for route params, `useFocusEffect` for data fetching, Zustand store actions for state
Register in root Stack: `apps/mobile/app/_layout.tsx` → `<Stack.Screen name="feature/screen" />`

**New Zustand Store:**

Location: `apps/mobile/stores/{domain}Store.ts`
Pattern:
```typescript
import { create } from 'zustand';
import type { Entity } from '@bhmhockey/shared';
import { entityService } from '@bhmhockey/api-client';

interface DomainState {
  items: Entity[];
  isLoading: boolean;
  errorMessage: string | null;
  fetch: () => Promise<void>;
}

export const useDomainStore = create<DomainState>((set) => ({
  items: [],
  isLoading: false,
  errorMessage: null,
  fetch: async () => {
    try {
      set({ isLoading: true });
      const data = await entityService.list();
      set({ items: data, errorMessage: null });
    } catch (error) {
      set({ errorMessage: (error as Error).message });
    } finally {
      set({ isLoading: false });
    }
  },
}));
```
Test pattern: Mock API client before importing store; test actions and state updates

**New API Endpoint:**

Location: Add method to existing controller in `apps/api/BHMHockey.Api/Controllers/` or create new controller
Pattern:
```csharp
[HttpPost("{id}/action")]
[Authorize]
public async Task<ActionResult<ResultDto>> Action(Guid id)
{
    var userId = GetCurrentUserId();
    var result = await _service.ActionAsync(id, userId);
    if (result == null) return NotFound();
    return Ok(result);
}
```
Add service method: Implement in service, add to interface
Add tests: Unit test in `BHMHockey.Api.Tests/`

## Special Directories

**`apps/mobile/.expo/`:**
- Purpose: Expo CLI cached data
- Generated: Yes
- Committed: No (gitignored)

**`apps/mobile/android/` and `apps/mobile/ios/`:**
- Purpose: Native code generated by `eas prebuild` or `expo run` commands
- Generated: Yes (Prebuild.json in .expo tracks this)
- Committed: No (gitignored)

**`apps/api/BHMHockey.Api/bin/` and `obj/`:**
- Purpose: .NET build artifacts and cache
- Generated: Yes (during dotnet build)
- Committed: No (gitignored)

**`apps/api/BHMHockey.Api.Tests/`:**
- Purpose: Unit test project for API
- Contains: Controller tests, service tests with mocked dependencies
- Test command: `yarn test:api`
- Pattern: InMemory EF Core database, XUnit assertions

**`.planning/codebase/`:**
- Purpose: GSD planning documents for architecture, testing, conventions, concerns
- Generated: By GSD commands
- Committed: Yes (part of planning infrastructure)

---

*Structure analysis: 2026-01-24*
