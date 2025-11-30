# claude.md - BHM Hockey Development Reference

## 📋 Project Status

**Overall Status**: Phase 4 Complete ✅ + Multi-Admin ✅ | Ready for Phase 5 (Waitlist)
- Full-stack monorepo operational
- Mobile app (Expo SDK 54, React Native) successfully connects to .NET 8 API
- Complete authentication flow working (register, login, logout)
- Organizations with multi-admin support (any admin can manage org and events)
- Events with registration and Venmo payment tracking working
- Push notifications for new events working
- Database: PostgreSQL on port 5433 (OrbStack)
- API running: `http://0.0.0.0:5001`
- **164+ backend tests passing**

## 📊 Completed Phases

**Phase 1: Authentication & User Profile** ✅ DONE (2025-11-25)
- User registration and login flow working
- Profile management (skill level, position, Venmo handle)
- JWT authentication with auto-logout on 401
- Database auto-migrations on API startup
- Route protection at app entry point with proper redirects
- Zustand state management for authentication

**Phase 2: Organizations & Subscriptions** ✅ DONE (2025-11-26)
- Organization CRUD operations
- Subscribe/unsubscribe to organizations
- Discover screen to browse organizations
- My Organizations section in profile
- Organization detail screen

**Phase 3: Events & Registration** ✅ DONE (2025-11-26)
- Event CRUD operations (standalone or org-linked)
- Event visibility (Public, OrganizationMembers, InviteOnly)
- Registration and cancellation
- Event creation screen with full form
- Events tab and event detail screen
- My Upcoming Events in profile

**Push Notifications** ✅ DONE (2025-11-26)
- Push notifications when org creates new event
- Expo Push API integration in backend
- Mobile app registers for push token on auth
- Deep link navigation when notification tapped
- Requires EAS project setup and physical device

**Phase 4: Venmo Payments** ✅ DONE (2025-11-27)
- Payment tracking with PaymentStatus (Pending, MarkedPaid, Verified)
- Venmo deep link integration for payments
- Users mark payment as complete, organizers verify
- Payment status badges in UI
- Organizer registrations screen with verification controls

**Multi-Admin Organizations** ✅ DONE (2025-11-30)
- Organizations support multiple admins (no single owner)
- Any admin can add/remove other admins (except last admin)
- Org admins can manage ALL events under their organization
- `IsCreator` → `IsAdmin` (orgs), `IsCreator` → `CanManage` (events)
- New API endpoints: GET/POST/DELETE `/api/organizations/{id}/admins`
- 33 new tests covering authorization and business rules

---

## 🏗️ Tech Stack

- **Mobile**: React Native (Expo SDK 54), Expo Router, TypeScript, Zustand, @react-native-picker/picker
- **API**: .NET 8, Entity Framework Core, PostgreSQL, JWT auth, BCrypt for passwords
- **Shared**: TypeScript packages (@bhmhockey/shared, @bhmhockey/api-client)
- **Deploy**: Digital Ocean App Platform
- **Package Manager**: Yarn workspaces

## 🚀 Quick Start

```bash
# Full setup (installs all dependencies + starts everything)
yarn install && yarn dev
# This starts API (auto-applies migrations) + Metro bundler simultaneously

# Or run separately:
yarn api              # Starts API on port 5001 (applies migrations first)
cd apps/mobile && npx expo start    # Starts Metro bundler on port 8081

# In another terminal, test API health
curl http://localhost:5001/health
```

## 🎯 Architecture Overview

**Monorepo Structure:**
```
root/
├── apps/
│   ├── api/          # .NET 8 API server
│   │   ├── Controllers/        # HTTP endpoints (AuthController, UsersController, etc.)
│   │   ├── Services/           # Business logic (AuthService, UserService)
│   │   ├── Models/
│   │   │   ├── Entities/       # Database models (User, Organization, Event)
│   │   │   └── DTOs/           # Data transfer objects for API requests/responses
│   │   ├── Data/
│   │   │   └── AppDbContext.cs # EF Core database context
│   │   ├── Migrations/         # EF Core auto-generated migrations
│   │   └── Program.cs          # App startup, DI, auto-migrations
│   └── mobile/       # React Native Expo app
│       ├── app/
│       │   ├── index.tsx       # Entry point with auth check and routing
│       │   ├── (auth)/         # Auth flow screens (login, register)
│       │   └── (tabs)/         # Main app screens (home, discover, events, profile)
│       ├── config/
│       │   └── api.ts          # Platform-specific API URL configuration
│       ├── stores/
│       │   └── authStore.ts    # Zustand authentication state
│       └── assets/             # App icons, splash screens, etc.
├── packages/
│   ├── shared/       # Shared types, constants, utilities
│   │   └── src/
│   │       ├── types/
│   │       ├── constants/
│   │       └── utils/
│   └── api-client/   # API client with auth interceptors
│       └── src/
│           ├── client.ts       # Axios instance with interceptors
│           ├── storage/        # AsyncStorage wrapper for token persistence
│           └── services/       # API methods (authService, userService, etc.)
└── package.json      # Yarn workspaces configuration
```

**Data Flow:**
```
Mobile Component 
  ↓ calls
Zustand Store Action 
  ↓ calls
API Client (with auth token in header)
  ↓ calls
Backend Service (business logic)
  ↓ calls
EF Core → Database
  ↓ response
Store persists data to AsyncStorage
Store updates state, component re-renders
```

**API Routing:**
- `POST   /api/auth/register` - Create account
- `POST   /api/auth/login` - Get JWT token
- `POST   /api/auth/logout` - Invalidate token
- `GET    /api/auth/me` - Get current authenticated user
- `GET    /api/users/me` - Get full user profile (skill level, position, Venmo)
- `PUT    /api/users/me` - Update profile fields
- `GET    /api/organizations` - List organizations
- `POST   /api/organizations/{id}/subscribe` - Subscribe to organization
- `GET    /api/organizations/{id}/admins` - List admins (admin only)
- `POST   /api/organizations/{id}/admins` - Add admin (admin only)
- `DELETE /api/organizations/{id}/admins/{userId}` - Remove admin (admin only)
- `POST   /api/events/{id}/payment/mark-paid` - Mark payment as complete
- `PUT    /api/events/{id}/registrations/{regId}/payment` - Verify payment (organizer)
- `/health` - Health check (root level)
- `/swagger` - API documentation (root level)

---

## 📁 Key File Locations

| Purpose | Location | Note |
|---------|----------|------|
| **API Startup** | `apps/api/BHMHockey.Api/Program.cs` | Auto-applies migrations on startup (lines 105-117). Entry point for all services. |
| **User Entity** | `apps/api/BHMHockey.Api/Models/Entities/User.cs` | Database model with SkillLevel, Position, VenmoHandle fields |
| **Auth Service** | `apps/api/BHMHockey.Api/Services/AuthService.cs` | Generates JWT tokens, hashes passwords |
| **User Service** | `apps/api/BHMHockey.Api/Services/UserService.cs` | Handles profile updates |
| **Auth Controller** | `apps/api/BHMHockey.Api/Controllers/AuthController.cs` | HTTP endpoints for register/login |
| **Users Controller** | `apps/api/BHMHockey.Api/Controllers/UsersController.cs` | HTTP endpoints for profile management |
| **Database Context** | `apps/api/BHMHockey.Api/Data/AppDbContext.cs` | EF Core context, defines DbSets |
| **API Dev Config** | `apps/api/BHMHockey.Api/appsettings.Development.json` | Connection string, JWT settings |
| **Launch Settings** | `apps/api/BHMHockey.Api/Properties/launchSettings.json` | Port 5001, Development environment |
| **Auth Store** | `apps/mobile/stores/authStore.ts` | Zustand state for user + token + auth actions |
| **App Entry Point** | `apps/mobile/app/index.tsx` | Auth check + routing (shows tabs if logged in, auth screens if not) |
| **Login Screen** | `apps/mobile/app/(auth)/login.tsx` | Login form and logic |
| **Register Screen** | `apps/mobile/app/(auth)/register.tsx` | Registration form and logic |
| **Profile Screen** | `apps/mobile/app/(tabs)/profile.tsx` | Edit user profile (skill level, position, Venmo) |
| **API Client** | `packages/api-client/src/client.ts` | Axios instance with auth token interceptor |
| **Auth API Service** | `packages/api-client/src/services/auth.ts` | login(), register(), logout(), getCurrentUser() |
| **User API Service** | `packages/api-client/src/services/users.ts` | getProfile(), updateProfile() |
| **Auth Storage** | `packages/api-client/src/storage/auth.ts` | AsyncStorage wrapper for JWT token persistence |
| **Shared Types** | `packages/shared/src/types/index.ts` | User interface, SkillLevel enum, Position type, DTOs |
| **Shared Constants** | `packages/shared/src/constants/index.ts` | SKILL_LEVELS, POSITIONS, validation rules |
| **API URL Config** | `apps/mobile/config/api.ts` | getApiUrl() - platform-specific URL detection |
| **Notification Utils** | `apps/mobile/utils/notifications.ts` | Push notification registration and handlers |
| **Notification Service** | `apps/api/.../Services/NotificationService.cs` | Sends push notifications via Expo API |
| **Admin Service** | `apps/api/.../Services/OrganizationAdminService.cs` | Multi-admin management (add/remove/check) |
| **Admin Entity** | `apps/api/.../Models/Entities/OrganizationAdmin.cs` | OrganizationAdmin table (org-user-addedBy) |
| **Venmo Utils** | `apps/mobile/utils/venmo.ts` | Venmo deep link helpers |
| **Environment** | `.env` | Database password, JWT secret (NOT committed) |
| **Env Template** | `.env.example` | Environment variables template |

---

## 🔐 Authentication System

**Complete Flow:**

1. **Registration**: User enters email, password, name
   - Backend hashes password with BCrypt
   - Creates User in database
   - Returns JWT token valid for 7 days

2. **Login**: User enters email, password
   - Backend verifies password hash
   - Generates JWT token
   - Returns token to client

3. **Token Storage**: 
   - Frontend stores token in AsyncStorage (via authStorage module)
   - AsyncStorage persists across app restarts

4. **API Requests**:
   - API client interceptor automatically adds `Authorization: Bearer {token}` header
   - Every request includes token

5. **Validation**:
   - Backend validates token signature on every request
   - If invalid or expired → returns 401
   - API client interceptor catches 401 → auto-logout

6. **Logout**:
   - Frontend clears AsyncStorage (token deleted)
   - Zustand store reset to initial state
   - App redirects to login screen via `index.tsx`

**Key Files:**
- Backend: `AuthService.cs` generates tokens, `AuthController.cs` handles register/login/logout
- Frontend: `authStore.ts` manages auth state, `authStorage.ts` persists token, `apiClient.ts` has auth interceptor
- Entry: `app/index.tsx` checks `authStore.user` and shows correct navigator

**Protected Routes:**
- Handled in `app/index.tsx` entry point
- If `authStore.user` exists → show `(tabs)` navigator (main app)
- If `authStore.user` is null → show `(auth)` navigator (login/register)
- Automatically redirects on logout or 401 error

**Token Lifecycle:**
- Generated on register/login
- Valid for 7 days
- Sent with every API request
- Auto-refreshes on 401 error (handled in api-client interceptor)
- Cleared on logout

---

## 📦 Zustand Store Pattern

**Auth Store Structure** (`apps/mobile/stores/authStore.ts`):
```typescript
interface AuthState {
  // State
  user: User | null;
  token: string | null;
  isLoading: boolean;
  
  // Async Actions
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, firstName: string, lastName: string) => Promise<void>;
  logout: () => Promise<void>;
  loadStoredAuth: () => Promise<void>;  // Call on app startup
  
  // Sync Actions
  setAuthUser: (user: User | null) => void;  // Call after profile update
}
```

**Usage Pattern in Components:**

```typescript
// Get state with selector (prevents unnecessary re-renders)
const user = useAuthStore(state => state.user);
const login = useAuthStore(state => state.login);
const isLoading = useAuthStore(state => state.isLoading);

// Call async action
try {
  await login(email, password);  // Handles token storage + error alerts
  // Component automatically redirected by index.tsx on success
} catch (error) {
  // Action already showed Alert.alert to user
}

// Update user after profile change
const { setAuthUser } = useAuthStore();
await updateProfile(profileData);
setAuthUser(updatedUser);  // Sync store with API response
```

**Key Patterns:**
- Load stored auth on app startup in `index.tsx` with `useEffect`
- All API calls happen in store actions, not in components
- Components only read state and call actions (separation of concerns)
- Store handles all side effects: API calls, error alerts, storage, redirects
- Use selectors to read specific state properties (better performance)

---

## 🔑 Code Patterns to Follow

**Backend (C# .NET):**
- **Naming**: PascalCase for classes/methods (UserService, RegisterAsync), camelCase for parameters (email, password)
- **Controllers**: Thin controllers that delegate to services, return `ActionResult<T>`
- **Services**: Interface + Implementation pattern (IUserService → UserService)
- **DTOs**: Separate request/response DTOs, use records for immutability
  - `RegisterRequest` / `AuthResponse` for auth
  - `UpdateUserProfileRequest` / `UserDto` for users
- **Error Handling**: try-catch in controllers, throw exceptions in services
- **Dependency Injection**: Register all services in Program.cs with appropriate lifetime
  - `AddScoped` for per-request services
  - `AddSingleton` for app-wide services
- **Migrations**: Always additive (never drop columns) for zero-downtime deployment

**Frontend (React Native/TypeScript):**
- **Naming**: camelCase for functions/variables (loginUser, isLoading), PascalCase for components (LoginScreen)
- **Components**: Functional components with hooks, style objects co-located
- **State Management**: Zustand for global state, useState for local UI state only
- **API Calls**: Always in Zustand store actions or separate service files, never directly in components
- **Error Handling**: try-catch with `Alert.alert()` for user-facing errors
- **Types**: Import from `@bhmhockey/shared` for consistency with backend DTOs
- **Async Operations**: Always use `async/await`, handle loading states in store

**Architecture Pattern:**
- **Backend**: `Controller → Service → Repository (implicit with EF Core DbContext) → Database`
- **Frontend**: `Component → Zustand Store Action → API Client → Backend`
- **Shared Types**: TypeScript types in `packages/shared` mirror C# DTOs exactly

**File Naming:**
- TypeScript: kebab-case for files (auth-store.ts), PascalCase for components (LoginScreen.tsx)
- C#: PascalCase for all files (AuthService.cs, UserController.cs)
- Directories: lowercase with hyphens (components/, services/, stores/)

---

## ⚠️ Critical Gotchas and Pitfalls

**Migration System:**
- ⚠️ Migrations auto-run on API startup (Program.cs lines 105-117) - first run may be slow
- ⚠️ All User entity changes require checking all `new UserDto(` instantiations throughout codebase for updates
- ⚠️ EventService.cs creates UserDto in multiple places - verify all are updated when User entity changes
- ⚠️ Migrations must be additive-only for zero-downtime deployment (no dropping columns or reversals)

**Authentication:**
- ⚠️ AsyncStorage is async - always `await` authStorage calls, don't forget await
- ⚠️ Token stored in AsyncStorage is NOT truly secure (use SecureStore/Keychain in production)
- ⚠️ 401 response auto-triggers logout via api-client interceptor - this is by design
- ⚠️ Token refresh happens automatically on 401 (wrapped in try-catch)

**Zustand Store:**
- ⚠️ Use selectors to prevent unnecessary re-renders: `useAuthStore(state => state.user)`
- ⚠️ Store actions handle errors internally - they throw `Alert.alert()` to user
- ⚠️ Profile updates must call `setAuthUser()` to sync Zustand store with API response
- ⚠️ Multiple stores may need to be initialized (create auth store, org store, etc. separately)

**Mobile/Physical Device:**
- ⚠️ iOS Simulator: `localhost:5001` works directly (shares network with host)
- ⚠️ Android Emulator: Use `10.0.2.2:5001` (special alias for host)
- ⚠️ Physical Device: Use computer's local IP from `ifconfig | grep "inet "` (e.g., 192.168.1.100)
- ⚠️ Both phone and computer must be on same WiFi network - test with Safari/Chrome first
- ⚠️ Firewalls may block port 5001 - test connectivity before debugging app

**Database:**
- ⚠️ PostgreSQL port is 5433 in OrbStack (not default 5432)
- ⚠️ User `bhmhockey` must own `public` schema or migrations fail
- ⚠️ Connection string in `appsettings.Development.json` has Port=5433

**Hot Reload:**
- ⚠️ Changes to `config/api.ts` or `app.config.js` may require full app restart (stop Metro, `npx expo start`)
- ⚠️ Changes to environment variables require API restart
- ⚠️ Package.json changes require `yarn install` and full restart

**Push Notifications:**
- ⚠️ Requires EAS project setup (`eas init`) for valid projectId
- ⚠️ Physical device required - simulator won't receive push notifications
- ⚠️ Expo Go has limitations - use development build for production
- ⚠️ User must be authenticated and push token saved before receiving notifications
- ⚠️ OrganizationSubscription.NotificationEnabled must be true for user to receive org notifications

**Don't Do:**

- ❌ Call API directly in components (use Zustand store actions instead)
- ❌ Store user data in component state (use Zustand store)
- ❌ Hardcode API URLs (use `getApiUrl()` from config/api.ts)
- ❌ Pass auth token manually (api-client interceptor adds it automatically)
- ❌ Forget to update Zustand store when profile changes (call `setAuthUser()`)
- ❌ Manually apply database migrations (they auto-run on API startup)
- ❌ Use User entity directly in API responses (always use DTOs)
- ❌ Store sensitive data like passwords plaintext (hash with BCrypt only)
- ❌ Bind API to localhost for mobile development (use 0.0.0.0 so devices can reach it)
- ❌ Commit .env files (they contain passwords and JWT secrets - must be .gitignored)
- ❌ Assume `postgres` user exists in database (check `\du` in psql)

---

## 📅 Implementation Plan Timeline

| Phase | Feature | Status | Est. Time |
|-------|---------|--------|-----------|
| 1 | Auth & User Profile | ✅ DONE | Completed |
| 2 | Organizations & Subscriptions | ✅ DONE | Completed |
| 3 | Events & Registration | ✅ DONE | Completed |
| 4 | Venmo Payments | ✅ DONE | Completed |
| - | Multi-Admin Organizations | ✅ DONE | Completed |
| 5 | Waitlist & Auto-Promotion | ⏳ NEXT | 2-3 hours |
| 6 | Push Notifications | ✅ DONE (partial) | Completed |
| 7 | Real-time Updates | Future phase | TBD |
| 8 | Payment Reminders | Future phase | TBD |

**MVP Definition:** Phases 1-4 complete ✅
- ✅ Phase 1: User authentication and profile
- ✅ Phase 2: Find organizations to join
- ✅ Phase 3: Find and join events
- ✅ Phase 4: Pay via Venmo
- ✅ Bonus: Multi-admin organizations

**After MVP:** Phases 5-8 add polish and features

**Push Notifications Status:**
- ✅ New event notifications to org subscribers
- ⏳ Payment reminders (Phase 8)
- ⏳ Waitlist promotions (Phase 5)
- ⏳ Registration confirmations

---

## 🎯 Current Sprint: Phase 5 - Waitlist & Auto-Promotion

**Before Starting Phase 5:**

1. **Test Multi-Admin end-to-end** (5-10 min):
   ```bash
   # Terminal 1
   yarn api

   # Terminal 2 (different terminal)
   cd apps/mobile && npx expo start

   # Test multi-admin:
   # - Create an organization (you become admin)
   # - Use API to add another user as admin
   # - Verify both can edit org/events
   # - Test payment flow with Venmo
   ```

2. **Review Phase 5 requirements** in `docs/Implementation.md`

**Phase 5 Tasks (In Sequence):**

1. **Add Waitlist to EventRegistration**
   - Fields: WaitlistPosition, PromotedAt, PaymentDeadline
   - When event full, new registrations go to waitlist

2. **Implement auto-promotion**
   - When someone cancels, promote next from waitlist
   - Send push notification to promoted user
   - Start 2-hour payment deadline timer

3. **Background service for deadline enforcement**
   - Check every 15 minutes for expired deadlines
   - Auto-cancel and promote next person

**Key Files:**
- `apps/api/.../Models/Entities/EventRegistration.cs` - Add waitlist fields
- `apps/api/.../Services/WaitlistService.cs` - New service
- `apps/mobile/components/RegistrationStatus.tsx` - Waitlist UI

**Estimated Time:** 2-3 hours

---

## 🧪 Testing Checklist

**Phase 1 Validation (Before Phase 2):**
- [ ] Run API with `yarn api` (watch console for auto-migration message)
- [ ] Run mobile with `yarn mobile`
- [ ] Register new user with valid email and password
- [ ] Navigate to profile tab
- [ ] Update skill level via picker
- [ ] Update position via picker
- [ ] Enter Venmo handle
- [ ] Verify all fields display correctly
- [ ] Logout button works
- [ ] Login with same credentials
- [ ] Verify profile fields persisted
- [ ] Check console for any errors or warnings
- [ ] No network errors in Expo console

**API Testing:**
```bash
# Health check
curl http://localhost:5001/health

# Register a test user (replace email with unique value)
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!","firstName":"John","lastName":"Doe"}'

# Copy the returned token and test protected endpoint
curl http://localhost:5001/api/auth/me \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"

# Test profile endpoint
curl http://localhost:5001/api/users/me \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"
```

**Browser Testing:**
```bash
# Test health endpoint from browser
http://localhost:5001/health

# View API documentation with Swagger
http://localhost:5001/swagger
```

---

## 🔗 Important Context Files

**Project Documentation:**
- `docs/PRD.md` - Complete product requirements document
- `docs/Implementation.md` - Detailed 11-phase implementation plan
- `MONOREPO_GUIDE.md` - Full monorepo development workflow
- `QUICK_START.md` - Fast setup instructions for new developers
- `README.md` - Project overview and high-level info

**Code References:**
- `apps/api/README.md` - Backend-specific documentation
- `.env.example` - Environment variables template

---

## 📚 External Resources & Documentation

**Official Documentation:**
- [Expo Router Docs](https://docs.expo.dev/router/introduction/) - File-based routing for React Native
- [Zustand Documentation](https://docs.pmnd.rs/zustand/getting-started/introduction) - State management library
- [React Native Docs](https://reactnative.dev/docs/getting-started) - Core framework
- [.NET 8 Documentation](https://learn.microsoft.com/en-us/aspnet/core/) - Backend framework
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) - ORM and migrations
- [Axios Documentation](https://axios-http.com/) - HTTP client

**Reference Code in Project:**
- Auth flow: `apps/mobile/app/(auth)/login.tsx` and `register.tsx`
- Store pattern: `apps/mobile/stores/authStore.ts`
- Service pattern: `apps/api/BHMHockey.Api/Services/AuthService.cs`
- API client: `packages/api-client/src/client.ts`
- URL configuration: `apps/mobile/config/api.ts`

---

## 🎭 Mental Model & Mindset

**Core Philosophy:**
- Keep it simple - no overengineering for edge cases
- Monolith is fine for this scale (100 users initially)
- Good enough is better than perfect (ship early, iterate)
- Database migrations are immutable - always additive for backward compatibility

**Development Rhythm:**
1. Backend first - implement model, migration, service, controller
2. Test via Swagger - ensure API works correctly
3. Frontend second - implement Zustand store action, UI screen
4. Test end-to-end on physical device - catch networking issues early
5. Document what you did in handoff for next session

**When Starting a New Session:**
1. Read this entire claude.md file
2. Check the status of current phase
3. Run the testing checklist
4. Proceed with next sprint tasks

---

**Last Updated**: 2025-11-30
**Next Session**: Begin Phase 5 Waitlist & Auto-Promotion (or build Admin Management UI)
