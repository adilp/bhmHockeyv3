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

### Font Scaling (Android Large Text)
Android users can increase system font size or display size, which can break layouts by causing text to overflow or be cut off.

**Global defaults** are set in `_layout.tsx`:
```typescript
Text.defaultProps.allowFontScaling = false;
TextInput.defaultProps.allowFontScaling = false;
```

**However**, these defaults can be unreliable, so we also use a belt-and-suspenders approach:

1. **Explicit `allowFontScaling={false}`** on Text/TextInput in key components:
   - `FormInput` - labels, inputs, hints
   - `FormSection` - titles, hints
   - `PositionSelector` - position labels
   - `TrophyCase` - badge names, dates
   - `DraggableRoster` - player names, skill levels, team headers

2. **Flexible layouts** with `flex: 1` on text labels that share rows with other elements (e.g., Switch + label)

3. **Native pickers** (`@react-native-picker/picker`) can't have font scaling disabled - they use native Android components. Mitigate by giving pickers adequate height (56px on Android vs 50px).

**When adding new components**, always add `allowFontScaling={false}` explicitly to Text/TextInput that could break layouts on large font settings.

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

### Badge Component

Always use the shared `Badge` component for status indicators. Never create custom badge styles.

```typescript
import { Badge } from '../../components';

// Variants: default, teal, green, purple, warning, error
<Badge variant="green">Paid</Badge>
<Badge variant="warning">Pending</Badge>
<Badge variant="error">Unpaid</Badge>
```

**Payment Status Badges:**
| Status | Variant | Text |
|--------|---------|------|
| `Verified` | `green` | "Paid" |
| `MarkedPaid` | `warning` | "Pending" |
| `Pending` | `error` | "Unpaid" |

**Other Common Badges:**
| Use Case | Variant | Text |
|----------|---------|------|
| Registered | `green` | "Registered" |
| Waitlist | `warning` | "Waitlist" or "#N Waitlist" (when published) |
| Organizer | `purple` | "Organizer" |
| Invite Only | `warning` | "Invite Only" |

**Draft Mode Note:** Don't show position numbers or team assignments in badges when `event.isRosterPublished` is false.

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
