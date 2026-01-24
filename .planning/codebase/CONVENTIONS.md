# Coding Conventions

**Analysis Date:** 2026-01-24

## Naming Patterns

**Files:**
- Components: PascalCase (e.g., `EventCard.tsx`, `Badge.tsx`)
- Stores: camelCase with "Store" suffix (e.g., `authStore.ts`, `eventStore.ts`)
- Screens/Pages: kebab-case (e.g., `(tabs)/index.tsx`, `organizations/create.tsx`)
- Utilities: camelCase (e.g., `api.ts`, `config.ts`)
- Tests: `*.test.ts` or `*.test.tsx` (e.g., `authStore.test.tsx`)

**Functions:**
- Hooks: `use` prefix (e.g., `useFocusEffect`, `useShallow`)
- Store actions: camelCase verbs (e.g., `fetchEvents`, `registerForEvent`, `cancelRegistration`)
- Components: PascalCase (e.g., `EventCard`, `PositionSelector`)
- Helper functions: camelCase (e.g., `createMockEvent`, `sortByDate`, `updateEventAsRegistered`)

**Variables:**
- State variables: camelCase (e.g., `isLoading`, `selectedEvent`, `processingEventId`)
- Constants: camelCase in single files, SCREAMING_SNAKE_CASE if exported (rarely)
- Private/internal: prefix with underscore if needed for clarity
- Boolean flags: `is`, `has`, `can`, `should` prefixes (e.g., `isRegistered`, `canManage`, `hasRoom`)

**Types:**
- Interfaces: PascalCase (e.g., `EventState`, `BadgeProps`, `FormProps`)
- Types: PascalCase (e.g., `EventDto`, `LoginRequest`, `EventCardVariant`)
- Type exports from shared package: PascalCase with `Dto` or `Request` suffix (e.g., `EventDto`, `CreateEventRequest`)

## Code Style

**Formatting:**
- ESLint + Prettier via expo config
- Line length: Not enforced but follows standard patterns
- Indent: 2 spaces (TypeScript/JavaScript standard)
- Quotes: Single quotes for strings (enforced by eslint-config-expo)
- Trailing commas: Present in multiline structures

**Linting:**
- Tool: ESLint with TypeScript support
- Config: `apps/mobile/.eslintrc.js`
- Extends: `expo` and `prettier` configs
- Custom rules: `@typescript-eslint/no-unused-vars` warns on unused vars (ignore pattern: `^_`)
- Run: `yarn lint` in mobile app

**TypeScript:**
- Strict mode enabled in `tsconfig.json`
- All files use `strict: true` compiler option
- Type imports explicitly marked with `type` keyword (e.g., `import type { EventDto }`)
- Return types explicitly annotated on functions (especially store actions)

## Import Organization

**Order:**
1. React/React Native imports
2. Expo imports (router, hooks, etc.)
3. Third-party libraries (zustand, date-fns, etc.)
4. Relative imports from shared packages (`@bhmhockey/api-client`, `@bhmhockey/shared`)
5. Relative imports from local files (`../../components`, `../../stores`)

**Path Aliases:**
- `@/*` → `apps/mobile/*` (current directory)
- `@bhmhockey/shared` → `packages/shared/src`
- `@bhmhockey/api-client` → `packages/api-client/src`

**Barrel Exports:**
- Component library uses barrel export: `components/index.ts` exports all components
- Always import from barrel: `import { EventCard, Badge } from '../../components'`
- Never import directly from component files: `import { EventCard } from '../../components/EventCard'` (avoid)

## Error Handling

**Patterns:**
- Try-catch blocks wrap all async API calls in stores
- Error types: `error instanceof Error ? error.message : 'Fallback message'`
- Complex errors: Check `error?.response?.data?.message` for axios response errors
- Store error state: Set `error` in state, clear with `clearError()` action
- Silent failures: Some operations log errors with `console.log()` but don't set error state (e.g., optional data fetching)
- Optimistic updates include rollback logic in catch block

Example from `eventStore.ts`:
```typescript
try {
  await eventService.register(eventId, position);
  // Update state on success
  set({ processingEventId: null });
} catch (error: any) {
  // Rollback optimistic update
  set({
    events,
    selectedEvent,
    processingEventId: null,
    error: error?.response?.data?.message || error?.message || 'Failed to register'
  });
  return null;
}
```

## Logging

**Framework:** Native `console` object

**Patterns:**
- Info/debug: `console.log('message')` for non-critical info
- Errors: `console.error('message', error)` for actual errors
- Lifecycle: Emojis used sparingly for visibility (e.g., `🗑️ Deleting event`, `🔄 Refreshing`)
- Store operations: Log key transitions (registration, cancellation, payment updates)
- No log levels - just console methods

## Comments

**When to Comment:**
- Complex algorithms (optimistic update rollback, sorting, filtering logic)
- Non-obvious state transitions (e.g., why processingEventId needed)
- JSDoc for component props and public store actions
- Inline comments for "why" not "what" (code should be self-documenting)

**JSDoc/TSDoc:**
- Used sparingly
- Component docstrings optional but recommended for public components
- Store action docstrings recommended for complex operations
- Type definitions include interface comments

Example from `eventStore.ts`:
```typescript
/**
 * EventStore Tests - Protecting event registration state management
 * These tests ensure:
 * - Optimistic updates work correctly for registration/cancellation
 * - Rollback occurs on API failure
 * - processingEventId tracks in-flight operations
 * - Error states are properly managed
 */
```

## Function Design

**Size:**
- Average: 20-40 lines
- Complex store actions: Can be longer (50-100 lines) but split helpers for reusability
- Components: Prefer composition over large single components

**Parameters:**
- Function parameters typed explicitly
- Destructured where helpful (especially in component props)
- Default parameters used (e.g., `organizations = []`)
- No positional boolean parameters - use object with named boolean props

**Return Values:**
- Explicit return type annotations on all functions
- Store actions return `Promise<T | null>` for operations that can fail
- Optimistic updates return `Promise<boolean>` for success/failure
- Components return `React.ReactNode`

Example from `eventStore.ts`:
```typescript
register: (eventId: string, position?: Position) => Promise<RegistrationResultDto | null>;
cancelRegistration: (eventId: string) => Promise<boolean>;
fetchEvents: (organizationId?: string) => Promise<void>;
```

## Module Design

**Exports:**
- Named exports preferred for functions and types
- Default export used for React components
- Type imports use `import type { ... }` syntax
- Interface and type exports on same line as implementation: `export type BadgeVariant = ...`

**Barrel Files:**
- `components/index.ts` exports all components and their types
- Stores exported individually (no barrel)
- Utilities exported from utility files directly

Example barrel export:
```typescript
export { Badge, PositionBadge } from './Badge';
export type { BadgeVariant, Position } from './Badge';
export { EventCard } from './EventCard';
export type { EventCardVariant } from './EventCard';
```

**Component Props Interface:**
- Props interface defined above component: `interface EventFormProps { ... }`
- Optional props use `?:` syntax
- Default values in function signature: `{ mode = 'create' } = {}`

## State Management (Zustand)

**Store Structure:**
- State interfaces defined first: `interface AuthState { ... }`
- State split into: data properties, loading flags, error state, actions
- All actions typed explicitly with return types

**Store Usage Rules:**
- Always use selectors to prevent re-renders: `useAuthStore(state => state.user)`
- Avoid destructuring entire state: `const { user } = useAuthStore()` (anti-pattern)
- Use `useShallow` from zustand when selecting multiple props: `useShallow(state => ({ prop1, prop2 }))`
- Clear errors with dedicated `clearError()` action

## Design System Usage

**Colors:**
- NEVER hardcode colors: `backgroundColor: colors.bg.darkest` not `#0D1117`
- Import from `theme/index.ts`: `import { colors, spacing, radius } from '../theme'`
- Use semantic color names from theme object

**Spacing:**
- Use predefined spacing constants: `spacing.md` (16px), `spacing.lg` (24px)
- Never hardcode pixel values
- Consistent padding/margin throughout app

**Radius:**
- Use predefined radius values: `radius.sm` (4px), `radius.lg` (12px), `radius.round` (9999)

**Font Scaling:**
- Always set `allowFontScaling={false}` on Text/TextInput in key components
- Android users can increase system font size - protect against layout breakage
- Components with explicit scaling disabled: FormInput, FormSection, PositionSelector, TrophyCase, DraggableRoster

---

*Convention analysis: 2026-01-24*
