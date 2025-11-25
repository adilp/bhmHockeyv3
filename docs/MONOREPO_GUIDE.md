# BHM Hockey - Monorepo Development Guide

Complete guide for developing the BHM Hockey app using a monorepo structure with React Native (Expo) and C# (.NET 8 API).

## Table of Contents

1. [Monorepo Structure](#monorepo-structure)
2. [Local Development Setup](#local-development-setup)
3. [API Configuration](#api-configuration)
4. [React Native App Setup](#react-native-app-setup)
5. [Shared Packages](#shared-packages)
6. [Development Workflow](#development-workflow)
7. [Testing](#testing)
8. [Deployment](#deployment)
9. [Troubleshooting](#troubleshooting)

## Monorepo Structure

```
bhmhockey2/
├── apps/
│   ├── mobile/              # React Native Expo app
│   │   ├── app/            # Expo Router screens
│   │   ├── config/         # App configuration
│   │   ├── app.json        # Expo config
│   │   ├── app.config.js   # Dynamic Expo config
│   │   └── package.json
│   └── api/                # C# ASP.NET Core API
│       ├── BHMHockey.Api/  # Main API project
│       │   ├── Controllers/
│       │   ├── Services/
│       │   ├── Models/
│       │   ├── Data/
│       │   └── Program.cs
│       ├── Dockerfile
│       └── README.md
├── packages/
│   ├── shared/             # Shared types/utilities
│   │   ├── src/
│   │   │   ├── types/
│   │   │   ├── constants/
│   │   │   └── utils/
│   │   └── package.json
│   └── api-client/         # Shared API client
│       ├── src/
│       │   ├── services/
│       │   ├── storage/
│       │   └── client.ts
│       └── package.json
├── .do/
│   └── app.yaml            # Digital Ocean deployment config
├── package.json            # Root workspace config
├── lerna.json             # Lerna monorepo config
└── yarn.lock

```

## Local Development Setup

### Prerequisites

- **Node.js** 18+ and **Yarn** 1.22+
- **.NET 8 SDK**
- **PostgreSQL** 15+
- **Expo CLI** (optional, bundled with expo)
- **iOS Simulator** (macOS) or **Android Emulator**

### Initial Setup

#### 1. Install Dependencies

```bash
# Install root dependencies and workspace packages
yarn install

# This will install dependencies for:
# - Root workspace
# - apps/mobile
# - packages/shared
# - packages/api-client
```

#### 2. Set Up Database

Create a local PostgreSQL database:

```bash
# macOS (using Homebrew)
brew install postgresql@15
brew services start postgresql@15

# Create database and user
psql postgres
CREATE DATABASE bhmhockey;
CREATE USER bhmhockey WITH PASSWORD 'your-password';
GRANT ALL PRIVILEGES ON DATABASE bhmhockey TO bhmhockey;
\q
```

#### 3. Configure Environment Variables

Create `.env` file in the root:

```bash
cp .env.example .env
```

Edit `.env`:

```env
# Database
ConnectionStrings__DefaultConnection=Host=localhost;Database=bhmhockey;Username=bhmhockey;Password=your-password

# JWT (auto-generated in dev mode, but you can set it)
Jwt__Secret=dev-secret-key-for-local-development-only-min-32-chars
Jwt__Issuer=http://localhost:5001
Jwt__Audience=http://localhost:5001

# CORS (not needed in dev - automatically allows all)
# Cors__AllowedOrigins=

# Expo Push Notifications (optional for Phase 1)
# Expo__AccessToken=
```

#### 4. Run Database Migrations

```bash
# Navigate to API directory
cd apps/api/BHMHockey.Api

# Install EF Core tools (first time only)
dotnet tool install --global dotnet-ef

# Create initial migration
dotnet ef migrations add InitialCreate

# Apply migration
dotnet ef database update

# Return to root
cd ../../..
```

## API Configuration

### Network Access for React Native

The API is configured to bind to `0.0.0.0:5001` in development mode, allowing access from:
- iOS Simulator: `http://localhost:5001`
- Android Emulator: `http://10.0.2.2:5001`
- Physical Device: `http://YOUR_COMPUTER_IP:5001`

This is configured in `apps/api/BHMHockey.Api/Program.cs`:

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://0.0.0.0:5001");
}
```

### CORS Configuration

CORS is automatically configured for development:
- **Development**: Allows all origins (for Expo)
- **Production**: Uses `Cors__AllowedOrigins` from environment

### Find Your Computer's IP

For testing on physical devices:

```bash
# macOS/Linux
ifconfig | grep "inet " | grep -v 127.0.0.1

# Windows
ipconfig

# Look for your local network IP (usually 192.168.x.x)
```

## React Native App Setup

### Platform-Specific API URLs

The mobile app automatically detects the platform and uses the correct API URL:

**File**: `apps/mobile/config/api.ts`

```typescript
export function getApiUrl(): string {
  if (__DEV__) {
    if (Platform.OS === 'android') {
      return 'http://10.0.2.2:5001/api';  // Android emulator
    } else if (Platform.OS === 'ios') {
      return 'http://localhost:5001/api';  // iOS simulator
    } else {
      // For physical devices
      return 'http://192.168.1.XXX:5001/api';  // TODO: Replace with your IP
    }
  }
  // Production
  return 'https://your-app.ondigitalocean.app/api';
}
```

### Testing on Physical Device

1. **Find your computer's IP** (see above)
2. **Update** `apps/mobile/config/api.ts`:
   ```typescript
   // Replace this line:
   return 'http://192.168.1.XXX:5001/api';
   // With your actual IP:
   return 'http://192.168.1.100:5001/api';
   ```
3. **Restart** the Expo development server

### API Client Initialization

The API client is initialized automatically in `apps/mobile/app/_layout.tsx`:

```typescript
initializeApiClient({
  baseURL: getApiUrl(),
});
```

## Shared Packages

### @bhmhockey/shared

Shared TypeScript types, constants, and utilities used by both frontend and backend.

**Usage:**

```typescript
import { User, Event, formatDate, isValidEmail } from '@bhmhockey/shared';
```

**Contents:**
- Types: User, Organization, Event, etc.
- Constants: SKILL_LEVELS, EVENT_STATUSES, etc.
- Utilities: formatDate, formatCurrency, validation helpers

### @bhmhockey/api-client

API client for React Native with authentication, storage, and service methods.

**Usage:**

```typescript
import { authService, eventService, organizationService } from '@bhmhockey/api-client';

// Login
const response = await authService.login({
  email: 'user@example.com',
  password: 'password123'
});

// Get events
const events = await eventService.getAll();
```

**Features:**
- Automatic token management (AsyncStorage)
- Interceptors for auth headers
- Error handling and 401 auto-logout
- Services: auth, events, organizations

## Development Workflow

### Option 1: Run Everything Together

```bash
# Start both API and mobile app
yarn dev
```

This runs:
- API: `http://0.0.0.0:5001`
- Mobile: Expo dev server

### Option 2: Run Separately

```bash
# Terminal 1: Start API
yarn api

# Terminal 2: Start mobile app
yarn mobile
```

### Common Commands

```bash
# API commands
yarn api                    # Start API in watch mode
yarn api:build             # Build API
yarn api:migrations         # Create new migration (add name)
yarn api:update-db         # Apply migrations

# Mobile commands
yarn mobile                # Start Expo dev server
yarn mobile:ios            # Start on iOS simulator
yarn mobile:android        # Start on Android emulator

# Workspace commands
yarn lint                  # Lint mobile app
yarn type-check            # Check TypeScript types
```

### Development Flow

1. **Start the API**:
   ```bash
   yarn api
   ```
   Output: `Now listening on: http://0.0.0.0:5001`

2. **Test API from browser/curl**:
   ```bash
   curl http://localhost:5001/health
   # Should return: {"status":"Healthy"}
   ```

3. **Start mobile app**:
   ```bash
   yarn mobile
   ```

4. **Scan QR code** with Expo Go app (iOS/Android)

5. **Make API calls** from the mobile app - it will automatically connect to your local API

### API Testing

Use the Swagger UI:
```
http://localhost:5001/swagger
```

Or test with curl:

```bash
# Health check
curl http://localhost:5001/health

# Register user
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "firstName": "Test",
    "lastName": "User"
  }'

# Login
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!"
  }'
```

## Testing

### Mobile App Testing

```bash
cd apps/mobile

# Run on iOS Simulator (macOS only)
yarn ios

# Run on Android Emulator
yarn android

# Run on physical device
# 1. Install Expo Go app
# 2. Scan QR code from `yarn mobile`
```

### API Testing

```bash
cd apps/api/BHMHockey.Api

# Run API
dotnet run

# Test endpoints with Swagger
open http://localhost:5001/swagger
```

## Deployment

### API Deployment (Digital Ocean App Platform)

The API is deployed to Digital Ocean App Platform using the monorepo configuration.

**Configuration**: `.do/app.yaml`

```yaml
services:
  - name: api
    source_dir: /apps/api
    dockerfile_path: Dockerfile
```

**Deployment Steps**:

1. **Update `.do/app.yaml`** with your database cluster name

2. **Push to GitHub**:
   ```bash
   git add .
   git commit -m "Your message"
   git push origin main
   ```

3. **App Platform** auto-deploys:
   - Builds Docker image from `apps/api/Dockerfile`
   - Runs database migrations
   - Deploys with zero downtime

4. **Verify**:
   ```bash
   curl https://your-app.ondigitalocean.app/health
   ```

### Mobile App Deployment (Expo Application Services)

React Native apps are NOT deployed to App Platform. Use EAS instead:

#### 1. Install EAS CLI

```bash
npm install -g eas-cli
```

#### 2. Configure EAS

```bash
cd apps/mobile
eas build:configure
```

#### 3. Update Production API URL

Edit `apps/mobile/app.config.js`:

```javascript
extra: {
  apiUrl: process.env.API_URL || (isDev
    ? 'http://localhost:5001/api'
    : 'https://your-app.ondigitalocean.app/api'  // ← Update this
  ),
}
```

#### 4. Build for App Stores

```bash
# Build for iOS
eas build --platform ios

# Build for Android
eas build --platform android

# Build for both
eas build --platform all
```

#### 5. Submit to Stores

```bash
# Submit to Apple App Store
eas submit --platform ios

# Submit to Google Play Store
eas submit --platform android
```

#### 6. Over-the-Air Updates (Optional)

For JS-only changes (no native code), use OTA updates:

```bash
eas update --branch production --message "Bug fixes"
```

### Environment Variables for Production

**API** (Digital Ocean Console):
- `Jwt__Secret`: Generate with `openssl rand -base64 32`
- `Expo__AccessToken`: Get from expo.dev
- `Cors__AllowedOrigins`: Your mobile app URL

**Mobile** (app.config.js):
- `API_URL`: Your Digital Ocean app URL

## Troubleshooting

### API Not Accessible from Mobile Device

**Problem**: Mobile app can't connect to API

**Solutions**:

1. **Check API is running**:
   ```bash
   curl http://localhost:5001/health
   ```

2. **Verify API binds to 0.0.0.0** (not localhost):
   - Check `apps/api/BHMHockey.Api/Program.cs`
   - Should see: `builder.WebHost.UseUrls("http://0.0.0.0:5001");`

3. **Check firewall** (macOS):
   ```bash
   # Allow incoming connections
   # System Preferences → Security & Privacy → Firewall → Firewall Options
   # Make sure your terminal/VS Code is allowed
   ```

4. **Verify device is on same network** as your computer

### Android Emulator Connection Issues

**Problem**: Android emulator can't reach API at `10.0.2.2:5001`

**Solution**:
```bash
# Verify emulator can reach host
adb shell ping 10.0.2.2

# If ping fails, try restarting emulator
```

### CORS Errors

**Problem**: CORS policy blocks requests

**Solutions**:

1. **Development**: CORS should be disabled (allows all origins)
   - Check `Program.cs` has development CORS config

2. **Production**: Add your app URL to allowed origins:
   ```env
   Cors__AllowedOrigins=https://yourdomain.com,exp://your-app
   ```

### Database Migration Errors

**Problem**: Migrations fail to apply

**Solutions**:

1. **Check connection string** in `.env`

2. **Ensure database exists**:
   ```bash
   psql -U postgres -c "CREATE DATABASE bhmhockey;"
   ```

3. **Reset database** (development only):
   ```bash
   cd apps/api/BHMHockey.Api
   dotnet ef database drop
   dotnet ef database update
   ```

### Yarn Workspace Issues

**Problem**: Packages not found or import errors

**Solutions**:

1. **Reinstall dependencies**:
   ```bash
   # From root
   rm -rf node_modules
   rm -rf apps/mobile/node_modules
   rm -rf packages/*/node_modules
   yarn install
   ```

2. **Clear Expo cache**:
   ```bash
   cd apps/mobile
   expo start -c
   ```

### Physical Device Can't Connect

**Problem**: App can't reach API on physical device

**Solutions**:

1. **Update IP in** `apps/mobile/config/api.ts`:
   ```typescript
   return 'http://YOUR_ACTUAL_IP:5001/api';
   ```

2. **Find your IP**:
   ```bash
   ifconfig | grep "inet " | grep -v 127.0.0.1
   ```

3. **Ensure both device and computer are on same WiFi**

4. **Restart Expo dev server**:
   ```bash
   yarn mobile
   ```

## Key Differences: Web vs React Native

| Aspect | Web App | React Native App (BHM Hockey) |
|--------|---------|-------------------------------|
| Frontend Deploy | App Platform (static site) | EAS Build → App Stores |
| API Access | Same domain, /api prefix | External domain, CORS required |
| Storage | localStorage | AsyncStorage |
| Hot Reload | Vite HMR | Expo Fast Refresh |
| Routing | File-based | Expo Router |
| Updates | Instant (refresh page) | OTA updates or app store |
| Local Dev URL | localhost:5173 | Expo Go app on phone |
| Network Config | Standard localhost | 0.0.0.0 binding required |

## Common Pitfalls

1. **API not accessible**: Must use `0.0.0.0`, not `localhost`
2. **Hardcoded IPs**: Use environment variables for API URLs
3. **CORS issues**: Must configure `AllowAll` in dev
4. **Port conflicts**: Avoid port 5000 (AirPlay on macOS)
5. **Wrong IP for Android**: Use `10.0.2.2` for emulator
6. **Forgetting to update production API URL** in app.config.js
7. **Not restarting Expo** after changing API configuration

## Quick Reference

### Start Development

```bash
# Option 1: Everything
yarn dev

# Option 2: Separate terminals
yarn api       # Terminal 1
yarn mobile    # Terminal 2
```

### API Endpoints

- Health: `http://localhost:5001/health`
- Swagger: `http://localhost:5001/swagger`
- API: `http://localhost:5001/api/*`

### Mobile Access URLs

- iOS Simulator: `http://localhost:5001/api`
- Android Emulator: `http://10.0.2.2:5001/api`
- Physical Device: `http://YOUR_IP:5001/api`

### Useful Commands

```bash
# Create migration
yarn api:migrations AddFeatureName

# Apply migrations
yarn api:update-db

# Clear Expo cache
cd apps/mobile && expo start -c

# Type check all packages
yarn type-check
```

## Next Steps

1. **Read Phase 1 Plan**: See [Phase1.md](./Phase1.md) for implementation roadmap
2. **Review API Docs**: See [apps/api/README.md](./apps/api/README.md)
3. **Start Building**: Follow the 8-week plan in Phase1.md

## Support

- **Product Requirements**: [BHM.md](./BHM.md)
- **Deployment Guide**: [DEPLOYMENT_GUIDE.md](./DEPLOYMENT_GUIDE.md)
- **Getting Started**: [GETTING_STARTED.md](./GETTING_STARTED.md)

---

Happy coding! 🏒
