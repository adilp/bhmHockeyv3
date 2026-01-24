# Codebase Concerns

**Analysis Date:** 2026-01-24

## Security

### Exposed Firebase Service Account Key
- **Risk:** Private key with full admin access to Firebase project committed to Git
- **Files:** `/bhm-hockey-64f1e5330a1c.json` - CONTAINS VALID PRIVATE KEY
- **Impact:** Any attacker with repository access has full Firebase admin privileges
- **Current mitigation:** .gitignore has wildcard pattern for service account files, but file still exists in repo history
- **Recommendations:**
  1. IMMEDIATELY rotate the Firebase service account (use Google Cloud Console)
  2. Remove from repo history: `git rm --cached bhm-hockey-64f1e5330a1c.json && git commit`
  3. Force push or use git-filter to clean history
  4. Store in environment only: load from `FIREBASE_SERVICE_ACCOUNT` env var as JSON string
  5. Add stronger pattern to .gitignore: `bhm-hockey-*.json`

### Hardcoded API URLs in Mobile
- **Risk:** Production API URL hardcoded; development comment shows local IP that could expose infrastructure
- **Files:** `apps/mobile/config/api.ts` (line 19-20)
- **Impact:** Can't easily switch between environments; commented IP address could leak internal infrastructure details
- **Current mitigation:** None
- **Recommendations:**
  1. Use Expo config variables: read from `app.json` `extra.apiUrl`
  2. Different app.json configs for dev/prod builds
  3. Remove commented IP address or use placeholder comments

### Type Safety with "any" Types
- **Risk:** 114+ instances of `any` types, `@ts-ignore`, or unsafe casts throughout codebase
- **Files:** Scattered across mobile and API code
- **Impact:** Type holes allow runtime errors that TypeScript should catch
- **Current mitigation:** Test coverage exists
- **Recommendations:**
  1. Enable `noImplicitAny` in `tsconfig.json`
  2. Enable `strict` mode
  3. Replace `any` with proper types from `@bhmhockey/shared`
  4. Especially critical in API response handling and error objects

## Tech Debt

### Large, Complex Components
- **Issue:** Multiple UI components exceed 800+ lines, mixing concerns
- **Files:**
  - `apps/mobile/app/tournaments/[id]/index.tsx` (916 lines)
  - `apps/mobile/app/tournaments/[id]/manage/admins.tsx` (950 lines)
  - `apps/mobile/stores/tournamentStore.ts` (909 lines)
  - `apps/mobile/app/tournaments/[id]/manage/questions.tsx` (860 lines)
  - `apps/mobile/app/tournaments/[id]/register/captain.tsx` (805 lines)
- **Impact:** Difficult to test, maintain, and reason about; potential performance issues with large re-renders
- **Recommendations:**
  1. Extract tab components to separate files
  2. Break stores into separate slices or custom hooks
  3. Extract form sections as reusable sub-components
  4. Aim for <400 line files

### Incomplete Tournament Team Registration
- **Issue:** Team creation separated from captain registration; captain data not persisted with team creation
- **Files:** `apps/mobile/app/tournaments/[id]/teams/create.tsx` (line 127)
- **TODO:** "After team creation, automatically register the captain with position and custom responses"
- **Impact:** Captain's position and custom answers must be entered separately; potential data consistency issues if captain registration fails
- **Fix approach:**
  1. Backend: Update `CreateTeam` request to accept optional captain registration data
  2. Frontend: Gather captain data before team creation, send together
  3. Atomic operation: Create team + captain registration in single transaction

### N+1 Query Potential
- **Issue:** `.ToList()` called in several loops and filters in services without proper eager loading
- **Files:**
  - `apps/api/BHMHockey.Api/Services/EventReminderService.cs` - multiple `.ToList()` on queries
  - `apps/api/BHMHockey.Api/Services/StandingsService.cs` - multiple match/team queries with `ToList()`
- **Impact:**
  - With 1000 teams: multiple sequential queries for each team (standing calculations)
  - With 1000 events: reminder service queries registrations per event
- **Recommendations:**
  1. Use `.Include()` and `.ThenInclude()` for related entities
  2. Move `.ToList()` to end of chain, not before filters
  3. Add indexes on foreign keys: `TournamentId`, `EventId`, `UserId`
  4. Load matches/teams once, filter in-memory if <1000 items

## Performance Bottlenecks

### Tournament Standings Calculation
- **Problem:** Tiebreaker logic (head-to-head, goal differential, goals) iterates through all completed matches multiple times
- **Files:** `apps/api/BHMHockey.Api/Services/StandingsService.cs`
- **Cause:**
  - `.ToList()` called before filtering in loop (lines 30-35)
  - Multiple `.Where().ToList()` calls for same match set
  - Tied group calculations regenerate sorted lists repeatedly
- **Improvement path:**
  1. Load teams, matches once with single query
  2. Pre-sort matches by tournament and status
  3. Build standing objects in single pass
  4. Cache standings calculation with invalidation on match completion
  5. Add index on `(TournamentId, Status)` for TournamentMatches

### Large Component Re-renders
- **Problem:** Tournament detail screen (916 lines) likely re-renders entire UI on state changes
- **Files:** `apps/mobile/app/tournaments/[id]/index.tsx`
- **Cause:**
  - Multiple state variables (tournament, teams, matches, standings, etc.)
  - When any state updates, entire component re-renders including tab content
  - No React.memo on sub-components
- **Improvement path:**
  1. Extract tab content to separate components wrapped in React.memo
  2. Use Zustand selectors to prevent re-renders on unrelated state
  3. Implement shouldComponentUpdate-like logic for expensive tabs (bracket, standings)
  4. Profile with React DevTools Profiler

### Badge Loading on Trophy Case
- **Problem:** Trophy case loads all badges on component mount without pagination
- **Files:** `apps/mobile/components/badges/TrophyCase.tsx` (483 lines)
- **Cause:** No pagination or lazy loading of badges
- **Improvement path:**
  1. Implement pagination (load 10 at a time)
  2. Add "Load More" button or infinite scroll
  3. Cache badge list in store to avoid reloads

## Fragile Areas

### Tournament State Machine
- **Issue:** Multiple tournament lifecycle operations (bracket generation, team assignment, match creation) must stay consistent
- **Files:**
  - `apps/api/BHMHockey.Api/Services/TournamentStateMachine.cs`
  - `apps/api/BHMHockey.Api/Services/BracketGenerationService.cs`
  - `apps/api/BHMHockey.Api/Services/TournamentTeamAssignmentService.cs`
- **Why fragile:**
  - Complex state transitions with many branches
  - Bracket generation must match standing calculation logic (tiebreaker rules)
  - Team assignment must validate against tournament format constraints
  - Race condition: multiple bracket requests could create duplicate matches
- **Safe modification:**
  1. Add database constraint: unique `(TournamentId, Round, HomeTeamId, AwayTeamId)`
  2. Add idempotency check: if bracket exists, return existing instead of creating
  3. Test all state transitions with TournamentStateMachineTests
  4. Cover edge cases: 0 teams, odd team counts, tie resolution limits
- **Test coverage:** `apps/api/BHMHockey.Api.Tests/Services/TournamentStateMachineTests.cs` exists but may need expansion

### Mobile App Auth State
- **Issue:** Authentication refresh and token expiration handled in interceptor; logout scattered across screens
- **Files:**
  - `packages/api-client/src/client.ts` (line 41-44: 401 handling)
  - Multiple screens with try-catch that call `authStore.logout()`
- **Why fragile:**
  - 401 error in one request could trigger logout before other pending requests complete
  - No guarantee that logout completes before app renders auth screens
  - Race condition: simultaneous requests both get 401, both call logout
- **Safe modification:**
  1. Add global auth error handler in app root (_layout.tsx)
  2. Use single "is logging out" flag in auth store to prevent multiple logout calls
  3. Clear all pending requests before logout
  4. Add integration tests for 401 responses with pending requests
- **Test coverage:** Basic auth tests exist in `packages/api-client/__tests__/services/auth.test.ts`

### Event Registration with Payment
- **Issue:** Registration, payment status, and waitlist processing are separate operations
- **Files:**
  - `apps/api/BHMHockey.Api/Services/EventService.cs` - register user
  - `apps/api/BHMHockey.Api/Services/WaitlistService.cs` - promote from waitlist
  - Payment verification separate from registration
- **Why fragile:**
  - If payment verification fails, user is already registered
  - If waitlist promotion fails, event slot remains unfilled
  - No transactional guarantee across registration + payment flow
- **Safe modification:**
  1. Wrap registration + payment in database transaction (EF Core SaveChanges)
  2. Add event lock: prevent double registration while processing
  3. Test payment failure scenarios
- **Test coverage:** Tests exist in EventServiceTests, WaitlistServiceTests

## Scaling Limits

### Background Services Single-Threaded
- **Current capacity:** WaitlistBackgroundService runs every 15 minutes; assumes <500 events
- **Limit:** If 1000+ events with large waitlists, background job will overlap with next run
- **Scaling path:**
  1. Add distributed lock (Redis or database-based) to prevent overlapping runs
  2. Partition events: batch processing, resume from checkpoint
  3. Monitor job duration; alert if >12 minutes (running close to 15-min interval)
  4. Consider async background job system: Hangfire or similar
- **Files:**
  - `apps/api/BHMHockey.Api/Services/Background/WaitlistBackgroundService.cs`
  - `apps/api/BHMHockey.Api/Services/Background/NotificationCleanupBackgroundService.cs`

### Notification Persistence
- **Current capacity:** Notifications stored indefinitely, 30-day cleanup background job
- **Limit:** PostgreSQL query performance degrades with >1M rows; no pagination on client
- **Scaling path:**
  1. Add index on `(UserId, CreatedAt desc)` for efficient lookup
  2. Partition notifications: archive/delete >90 days (adjust from 30 days)
  3. Implement pagination on client (limit to 50 per page)
  4. Cache "unread count" separately from full notification list
- **Files:**
  - `apps/api/BHMHockey.Api/Services/NotificationPersistenceService.cs`
  - `apps/api/BHMHockey.Api/Services/Background/NotificationCleanupBackgroundService.cs`

### Database Connection Pool
- **Current:** Npgsql default connection pool (30 connections)
- **Limit:** With 5+ concurrent users, each with 2-3 active requests, pool could saturate
- **Scaling path:**
  1. Monitor connection usage: enable logging in Program.cs
  2. Increase pool size if consistently >80% utilized
  3. Profile long-running queries; optimize those first
  4. Add read-only replica for GET requests (if using DigitalOcean replication)
- **Files:** `apps/api/BHMHockey.Api/Program.cs` (line 49-51: connection pooling)

## Missing Critical Features

### Audit Trail for User Data Changes
- **Problem:** Events, organizations, tournament details can be edited with no history of who changed what
- **Files:**
  - `apps/api/BHMHockey.Api/Services/EventService.cs` - UpdateAsync
  - `apps/api/BHMHockey.Api/Services/OrganizationService.cs` - UpdateAsync
- **Impact:** Can't detect who made inappropriate changes; no rollback capability
- **Recommendation:** Implement audit log similar to TournamentAuditLog for all entities
  - Structure: User, Action (Create/Update/Delete), Field, OldValue, NewValue, Timestamp
  - Model: `AuditLog` entity with `string EntityType` + `string EntityId`

### Environment-Based API Configuration
- **Problem:** Mobile has hardcoded production URL; no way to point to staging/dev without code changes
- **Files:** `apps/mobile/config/api.ts` (hardcoded URL at line 19)
- **Impact:**
  - Can't test against staging environment
  - QA must build from source with modified config
- **Recommendation:**
  1. Store API URL in `app.json` extra config
  2. Create dev/staging/prod build profiles in EAS
  3. Read at app startup: `Constants.expoConfig?.extra?.apiUrl`

## Test Coverage Gaps

### Mobile Store Error Handling
- **What's not tested:** Zustand store error states; only happy paths verified
- **Files:** `apps/mobile/__tests__/stores/eventStore.test.ts`
- **Risk:** Silent failures; errors not propagated to UI correctly
- **Priority:** High
- **Example tests needed:**
  - API error responses (network timeout, 500, validation error)
  - Partial failures (update succeeds, delete fails)
  - Concurrent operations (user rapid-clicks, store gets multiple requests)

### API Authorization Integration Tests
- **What's not tested:** Full admin authorization flows across services
- **Files:** Various controller tests exist but don't test cross-service auth
- **Risk:** Authorization bypass in edge cases (e.g., organization admin accessing tournament admin functions)
- **Priority:** High
- **Example tests needed:**
  - User tries to edit event they don't own
  - Organization member tries to delete organization
  - Tournament participant tries to view admin audit log

### Background Job Failure Scenarios
- **What's not tested:** Background services when database is unavailable, network timeouts
- **Files:**
  - `apps/api/BHMHockey.Api/Services/Background/WaitlistBackgroundService.cs`
  - `apps/api/BHMHockey.Api/Services/Background/EventReminderBackgroundService.cs`
- **Risk:** Unhandled exceptions cause service to stop; no restart mechanism
- **Priority:** Medium
- **Example tests needed:**
  - Database connection fails mid-execution
  - Expo Push API returns 500 error
  - Partial batch succeeds, one item throws

### Tournament Tie Resolution Edge Cases
- **What's not tested:** Complex tie scenarios with 3+ teams tied on same points
- **Files:** `apps/api/BHMHockey.Api/Services/StandingsService.cs`
- **Risk:** Head-to-head logic may not work correctly with circular ties
- **Priority:** Medium
- **Example tests needed:**
  - Team A beats B, B beats C, C beats A (all 1-1 record)
  - Multiple teams with identical goal differential and head-to-head records
  - Verify tiebreaker ordering matches sports league rules

---

*Concerns audit: 2026-01-24*
