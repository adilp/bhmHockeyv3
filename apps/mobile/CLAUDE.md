# BHM Hockey Mobile

## Quick Start

```bash
yarn mobile                    # From monorepo root - starts Metro bundler
npx expo start                 # Or directly in apps/mobile
```

## Tech Stack

- React Native with Expo SDK 54
- Expo Router (file-based navigation)
- Zustand for state management
- TypeScript throughout

## Project Layout

- `app/` - Expo Router screens and layouts
- `components/` - Reusable UI components (import from `components/index.ts`)
- `stores/` - Zustand stores (auth, event, organization, notification)
- `theme/` - Design tokens (colors, spacing, radius, typography)
- `config/` - API URL configuration
- `utils/` - Notifications, Venmo deep links, sharing

## Navigation Structure

```
/                    → Auth check, redirects to (tabs) or (auth)
/(auth)/login        → Login screen
/(auth)/register     → Register screen
/(tabs)/home         → Home tab (upcoming events)
/(tabs)/orgs         → Organizations tab
/(tabs)/events       → Events tab
/(tabs)/alerts       → Notifications tab
/(tabs)/profile      → Profile tab
/events/[id]         → Event detail
/events/create       → Create event modal
/organizations/[id]  → Organization detail
```

## Critical Patterns

### State Management (Zustand)
```typescript
// ALWAYS use selectors to prevent re-renders
const user = useAuthStore(state => state.user);        // Good
const { user } = useAuthStore();                       // Bad - re-renders on any change

// Stores handle ALL API calls - never call API from components
await eventStore.fetchEvents();  // Store handles loading, errors, alerts
```

### Theme Usage
```typescript
import { colors, spacing, radius } from '../../theme';

// NEVER hardcode colors or spacing
backgroundColor: colors.bg.darkest,    // Not '#0D1117'
padding: spacing.md,                   // Not 16
borderRadius: radius.lg,               // Not 12
```

### Component Imports
```typescript
// Always import from barrel export
import { EventCard, Badge, EmptyState } from '../../components';
```

### Data Fetching
```typescript
// Use useFocusEffect for screen data, with cleanup
useFocusEffect(
  useCallback(() => {
    fetchData();
    return () => clearData();  // Cleanup on blur
  }, [id])
);
```

### Route Parameters
```typescript
const { id } = useLocalSearchParams<{ id: string }>();
router.push(`/events/${eventId}`);
```

## Common Gotchas

### API URL Hardcoded
`config/api.ts` is currently hardcoded to production. For local dev:
- iOS Simulator: `http://localhost:5001/api`
- Android Emulator: `http://10.0.2.2:5001/api`
- Physical device: `http://{YOUR_IP}:5001/api`

### TextInput Placeholder Color
Always set on TextInputs or they'll be invisible:
```typescript
placeholderTextColor={colors.text.muted}
```

### Version Numbers - Update TWO Places
1. `app.json` - version, ios.buildNumber, android.versionCode
2. `ios/BHMHockey/Info.plist` - CFBundleShortVersionString, CFBundleVersion

### Optimistic Updates
Event registration/cancellation/payment use optimistic UI with rollback on failure.
`processingEventId` state prevents double-clicks.

### Push Notifications
- Require physical device (simulator won't work)
- Setup lives in `_layout.tsx` (stays mounted entire app)
- User must be authenticated with push token saved

### Loading States
- Show spinner only when `isLoading && items.length === 0`
- Pull-to-refresh should NOT show loading spinner

## Design System

### Colors (Sleeper-inspired dark theme)
- Backgrounds: `bg.darkest` (screens), `bg.dark` (cards), `bg.elevated` (inputs)
- Text: `text.primary` (white), `text.secondary`, `text.muted`
- Primary: `primary.teal`, `primary.green`, `primary.purple`
- Status: `status.success`, `status.warning`, `status.error`

### Spacing
`xs` (4), `sm` (8), `md` (16), `lg` (24), `xl` (32)

### Radius
`sm` (4), `md` (8), `lg` (12), `xl` (16), `round` (9999)

## Key Components

| Component | Usage |
|-----------|-------|
| `EventCard` | Event list items with variant-based badges |
| `OrgCard` | Organization list items |
| `Badge` | Status indicators (teal, green, purple, warning, error) |
| `SectionHeader` | Section titles with optional count |
| `EmptyState` | Empty list placeholder |
| `FormInput` | Dark-themed text input with label |
| `EventForm` / `OrgForm` | Create/edit forms |

## Testing

```bash
yarn test              # Run Jest tests
```

Mock `@bhmhockey/api-client` before importing stores in tests.

## OTA Updates

```bash
cd apps/mobile
npx eas-cli update --branch production --message "Description"
```

OTA can update JS/styles/assets. Native changes require new build.

## Native Builds

```bash
npx eas-cli build --platform ios --profile production
npx eas-cli submit --platform ios
```
