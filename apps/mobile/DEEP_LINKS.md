# Deep Link Testing Guide

## Supported Deep Links

### Tournament Team Detail
Opens a specific team's detail page within a tournament.

**Format:** `bhmhockey://tournament/{tournamentId}/team/{teamId}`

**Example:** `bhmhockey://tournament/abc123/team/xyz789`

**Navigates to:** `/tournaments/abc123/teams/xyz789`

### Tournament Detail (Future)
Opens a tournament detail page.

**Format:** `bhmhockey://tournament/{tournamentId}`

**Example:** `bhmhockey://tournament/abc123`

**Navigates to:** `/tournaments/abc123`

## Testing Deep Links

### iOS Simulator
```bash
# Option 1: Using xcrun simctl
xcrun simctl openurl booted "bhmhockey://tournament/abc123/team/xyz789"

# Option 2: Using shell command
open "bhmhockey://tournament/abc123/team/xyz789"
```

### Android Emulator
```bash
# Using adb
adb shell am start -W -a android.intent.action.VIEW -d "bhmhockey://tournament/abc123/team/xyz789"
```

### Physical Device
1. Send yourself a test link via Messages/Email/Notes
2. Tap the link
3. App should open and navigate to the correct screen

Or create a test webpage:
```html
<a href="bhmhockey://tournament/abc123/team/xyz789">Open Team in BHM Hockey</a>
```

## Testing States

Test deep links in these app states:

1. **Cold Start** - App completely closed
   - Kill the app
   - Tap deep link
   - App should launch and navigate to the team page

2. **Warm Start** - App in background
   - Put app in background (home button)
   - Tap deep link
   - App should come to foreground and navigate to the team page

3. **Hot Navigation** - App already open
   - App is already running and visible
   - Tap deep link (from another app or notification)
   - App should navigate to the team page

## Debugging

Check console logs for deep link handling:
- `🔗 Initial deep link detected:` - Cold start detection
- `🔗 Deep link received while app running:` - Hot/warm start detection
- `🔗 Parsing deep link:` - URL parsing
- `🔗 Deep link path:` - Extracted path
- `🔗 Navigating to tournament team:` - Successful match and navigation

## Implementation Details

**Configuration:**
- URL scheme `bhmhockey` is configured in:
  - `apps/mobile/app.json` (line 43)
  - `apps/mobile/ios/BHMHockey/Info.plist` (line 30)
  - `apps/mobile/android/app/src/main/AndroidManifest.xml` (line 33)

**Code Files:**
- Deep link parsing: `apps/mobile/utils/deepLinks.ts`
- Integration: `apps/mobile/app/_layout.tsx`
- Follows same pattern as notification handling

**Route Mapping:**
- Deep link pattern: `tournament/{tournamentId}/team/{teamId}`
- App route: `/tournaments/[id]/teams/[teamId]`
- Expo Router automatically handles the file-based routing
