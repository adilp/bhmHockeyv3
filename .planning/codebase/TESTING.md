# Testing Patterns

**Analysis Date:** 2026-01-24

## Test Framework

**Runner:**
- Jest 29.7.0
- Configured per-workspace with ts-jest preset
- Config files: `apps/mobile/jest.config.js`, `packages/shared/jest.config.js`, `packages/api-client/jest.config.js`

**Assertion Library:**
- Jest built-in assertions (`expect`)
- Testing Library not used (mocking instead)

**Run Commands:**
```bash
yarn test                 # Run all tests (API + frontend)
yarn test:api            # Run .NET API tests
yarn test:frontend       # Run mobile + packages tests
yarn test:shared         # Run @bhmhockey/shared tests only
yarn test:api-client     # Run @bhmhockey/api-client tests only
yarn test:mobile         # Run mobile app tests only
yarn test:watch          # Watch mode for mobile
yarn test:coverage       # Generate coverage reports
```

## Test File Organization

**Location:**
- Co-located alongside source code in `__tests__` directories
- Mobile: `apps/mobile/__tests__/`
- Packages: `packages/*/src/__tests__/` or `packages/*/__tests__/`

**Naming:**
- Pattern: `*.test.ts` or `*.test.tsx`
- Examples: `authStore.test.tsx`, `auth.test.ts`, `sample.test.tsx`

**Structure:**
```
apps/mobile/__tests__/
├── stores/
│   ├── authStore.test.tsx
│   ├── eventStore.test.ts
│   └── organizationStore.test.ts
└── sample.test.tsx

packages/shared/__tests__/
├── sample.test.ts
├── validation.test.ts
└── ...

packages/api-client/__tests__/
├── sample.test.ts
└── services/
    └── auth.test.ts
```

## Test Structure

**Suite Organization:**
```typescript
describe('authStore', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    // Reset store state
    useAuthStore.setState({
      user: null,
      isLoading: true,
      isAuthenticated: false,
    });
  });

  describe('login', () => {
    it('sets user and isAuthenticated on successful login', async () => {
      // Test implementation
    });

    it('calls authService.login with credentials', async () => {
      // Test implementation
    });
  });

  describe('logout', () => {
    it('clears user and isAuthenticated', async () => {
      // Test implementation
    });
  });
});
```

**Patterns:**
- One `describe` per store/service
- Nested `describe` blocks per action/method
- `beforeEach` for setup and mocking
- Descriptive `it` statements as assertions
- Setup happens before mocking (see "Mocking" section)

## Mocking

**Framework:** Jest built-in mocking with `jest.mock()`

**Mocking Strategy:**
- Mock dependencies BEFORE importing the module under test
- Store mock functions at module level (before jest.mock)
- Call `jest.clearAllMocks()` in beforeEach
- Reset store state in beforeEach

**Pattern for API Client Tests:**
```typescript
// Step 1: Define mock functions at top level
const mockSetItem = jest.fn<Promise<void>, [string, string]>(() => Promise.resolve());
const mockGetItem = jest.fn<Promise<string | null>, [string]>(() => Promise.resolve(null));

// Step 2: Mock the module
jest.mock('@react-native-async-storage/async-storage', () => ({
  setItem: mockSetItem,
  getItem: mockGetItem,
  // ... etc
}));

// Step 3: Import after mocking
import { authStorage } from '../../src/storage/auth';

// Step 4: In beforeEach, reset all mocks
beforeEach(() => {
  jest.clearAllMocks();
});

// Step 5: Use in tests
mockGetItem.mockResolvedValueOnce('stored-token');
const result = await authStorage.getToken();
expect(mockGetItem).toHaveBeenCalledWith('@bhmhockey:authToken');
```

**Pattern for Zustand Store Tests:**
```typescript
// Mock the API client
jest.mock('@bhmhockey/api-client', () => ({
  eventService: {
    getAll: mockGetAll,
    getById: mockGetById,
    register: mockRegister,
  },
}));

// Import store after mocking
import { useEventStore } from '../../stores/eventStore';

// Reset store state in beforeEach
beforeEach(() => {
  jest.clearAllMocks();
  useEventStore.setState({
    events: [],
    selectedEvent: null,
    isLoading: false,
    error: null,
  });
});
```

**What to Mock:**
- External APIs and services (`eventService`, `authService`, `organizationService`)
- React Native modules (`@react-native-async-storage/async-storage`, `react-native`)
- Axios and HTTP clients
- Date functions when testing time-dependent logic

**What NOT to Mock:**
- Store actions themselves (test them with real implementation)
- Helper functions internal to stores
- Theme and design system constants
- Type definitions and interfaces

## Fixtures and Factories

**Test Data:**
```typescript
// Helper to create mock registration result
const createMockRegistrationResult = (overrides: Partial<RegistrationResultDto> = {}): RegistrationResultDto => ({
  status: 'Registered',
  waitlistPosition: null,
  message: 'Successfully registered for the event',
  ...overrides,
});

// Helper to create mock event
const createMockEvent = (overrides: Partial<EventDto> = {}): EventDto => ({
  id: 'event-1',
  organizationId: 'org-1',
  organizationName: 'Test Org',
  name: 'Test Event',
  eventDate: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
  maxPlayers: 10,
  registeredCount: 5,
  cost: 25,
  isRegistered: false,
  canManage: false,
  // ... additional defaults
  ...overrides,
});

// Usage in tests
const mockEvents = [
  createMockEvent({ id: 'event-1' }),
  createMockEvent({ id: 'event-2', name: 'Event 2' }),
];
```

**Location:**
- Defined in test files themselves (not extracted to separate fixtures)
- Placed at top of test file after imports and mocks
- Reused across test suites within same file

## Coverage

**Requirements:** No enforced minimum

**View Coverage:**
```bash
yarn test:coverage      # Generates coverage reports
```

**Coverage Exclusions:**
```javascript
// From jest.config.js
collectCoverageFrom: [
  'apps/mobile/**/*.{ts,tsx}',
  '!**/node_modules/**',
  '!**/.expo/**',
  '!**/coverage/**',
  '!**/*.d.ts',        // Exclude type definitions
  '!**/index.ts',      // Exclude barrel exports
],
```

## Test Types

**Unit Tests:**
- Scope: Individual store actions, helper functions
- Approach: Mock all external dependencies (API services)
- Location: `__tests__/stores/`, `__tests__/services/`
- Example: Test that `authStore.login()` calls `authService.login()` and updates state

**Integration Tests:**
- Scope: Store + API client interaction
- Approach: Mock axios but test full request/response flow
- Location: `__tests__/services/`
- Example: Test that `authService.login()` makes POST request and stores token in AsyncStorage

**E2E Tests:**
- Framework: Not used currently
- Notes: Could be added later with Detox or Playwright for mobile

## Common Patterns

**Async Testing:**
```typescript
it('with valid token fetches and sets user', async () => {
  mockGetToken.mockResolvedValue('valid-token');
  mockGetCurrentUser.mockResolvedValue(mockUser);

  await useAuthStore.getState().checkAuth();

  expect(useAuthStore.getState().isAuthenticated).toBe(true);
  expect(useAuthStore.getState().user).toEqual(mockUser);
});
```

**Error Testing:**
```typescript
it('does not update state on API error', async () => {
  mockLogin.mockRejectedValue(new Error('Invalid credentials'));

  await expect(
    useAuthStore.getState().login({
      email: 'test@example.com',
      password: 'wrong',
    })
  ).rejects.toThrow('Invalid credentials');

  expect(useAuthStore.getState().isAuthenticated).toBe(false);
  expect(useAuthStore.getState().user).toBeNull();
});
```

**Optimistic Update Testing:**
```typescript
it('optimistically updates UI before API response when room available', async () => {
  const event = createMockEvent({ id: 'event-1', isRegistered: false, registeredCount: 5, maxPlayers: 10 });
  useEventStore.setState({ events: [event] });

  let resolveRegister: (value: RegistrationResultDto) => void;
  mockRegister.mockReturnValue(
    new Promise((resolve) => {
      resolveRegister = resolve;
    })
  );

  // Start registration (don't await)
  const registerPromise = useEventStore.getState().register('event-1');

  // Check optimistic update happened immediately
  const updatedEvent = useEventStore.getState().events.find((e) => e.id === 'event-1');
  expect(updatedEvent?.isRegistered).toBe(true);
  expect(updatedEvent?.registeredCount).toBe(6);

  // processingEventId should be set
  expect(useEventStore.getState().processingEventId).toBe('event-1');

  // Resolve the promise
  resolveRegister!(createMockRegistrationResult());
  await registerPromise;
});
```

**Rollback Testing:**
```typescript
it('rolls back optimistic update on API failure', async () => {
  const event = createMockEvent({ id: 'event-1', isRegistered: false, registeredCount: 5 });
  useEventStore.setState({ events: [event] });

  mockRegister.mockRejectedValue(new Error('Network error'));

  await useEventStore.getState().register('event-1');

  // State should be rolled back
  const rolledBackEvent = useEventStore.getState().events.find((e) => e.id === 'event-1');
  expect(rolledBackEvent?.isRegistered).toBe(false);
  expect(rolledBackEvent?.registeredCount).toBe(5);
  expect(useEventStore.getState().error).toContain('Network error');
});
```

## Jest Configuration Details

**Mobile App Config** (`apps/mobile/jest.config.js`):
- Preset: `ts-jest`
- Test environment: `node`
- Root directory: Monorepo root (to access packages)
- Module name mapper: Handles path aliases and React Native mocks
- Setup file: `jest.setup.js` (mocks AsyncStorage globally)
- Transform: All `.ts` and `.tsx` files via ts-jest

**Shared Package Config** (`packages/shared/jest.config.js`):
- Simple setup: Preset + testEnvironment + testMatch
- No module name mapper (minimal dependencies)

**API Client Config** (`packages/api-client/jest.config.js`):
- Maps `@bhmhockey/shared` to shared package
- No React Native mocking needed

## Mocking React Native

**Global React Native Mock** (`apps/mobile/__mocks__/react-native.js`):
```javascript
module.exports = {
  Platform: {
    OS: 'ios',
    select: (obj) => obj.ios || obj.default,
  },
  StyleSheet: {
    create: (styles) => styles,
    flatten: (styles) => styles,
  },
  // ... component stubs ...
};
```

**AsyncStorage Mock** (`apps/mobile/jest.setup.js`):
```javascript
jest.mock('@react-native-async-storage/async-storage', () => ({
  setItem: jest.fn(() => Promise.resolve()),
  getItem: jest.fn(() => Promise.resolve(null)),
  // ... etc
}));
```

---

*Testing analysis: 2026-01-24*
