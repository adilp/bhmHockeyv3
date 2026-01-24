# External Integrations

**Analysis Date:** 2026-01-24

## APIs & External Services

**Expo Push Notifications:**
- Service: Expo Push Service (https://exp.host/--/api/v2/push/send)
- What it's used for: Send push notifications to mobile devices (events, registrations, payments, system announcements)
- SDK/Client: `expo-notifications` 0.32.13 (client), `expo-device` 8.0.9 (device detection)
- Auth: `Expo:AccessToken` (environment variable, set in `.env` or DigitalOcean console)
- Implementation: `apps/api/BHMHockey.Api/Services/NotificationService.cs`
- Token Format: ExponentPushToken (format: `ExponentPushToken[xxx]`)
- Note: Requires physical device; simulators cannot receive push notifications

**Venmo (Future Integration):**
- Service: Venmo deep linking for payment
- What it's used for: Event payment processing via Venmo
- Implementation planned in: `apps/mobile/utils/` (venmo deep links)
- Config: Venmo URL scheme registered in `app.json` LSApplicationQueriesSchemes

## Data Storage

**Databases:**
- **PostgreSQL 15**
  - Connection: `ConnectionStrings__DefaultConnection` environment variable
  - Local development: `postgres://bhmhockey:password@localhost:5433/bhmhockey`
  - Production: `DATABASE_URL` auto-injected by DigitalOcean (converted from `postgresql://` to .NET format)
  - Client: Npgsql 8.0 (via Entity Framework Core)
  - ORM: Entity Framework Core 8.0
  - Port: 5433 (local development), managed port (production)
  - Database auto-migrates on API startup via `AppDbContext`

**Migrations:**
- `dotnet ef migrations add MigrationName` - Create new migration
- `dotnet ef database update` - Apply migrations manually
- Auto-applied on API startup (see `Program.cs` database initialization)
- All dates stored as UTC in database, converted to Central Time for display

**File Storage:**
- Local filesystem only (no S3, Azure Blob, etc.)
- Badge assets: `apps/mobile/assets/badges/` (PNG images, 288x288px)
- App icons: `apps/mobile/assets/` (icon.png, splash.png, adaptive-icon.png)
- No cloud storage integration

**Caching:**
- In-memory caching only (Zustand stores on mobile, in-memory C# objects on API)
- No Redis, Memcached, or distributed cache
- Token persistence: AsyncStorage (secure storage on device)

## Authentication & Identity

**Auth Provider:**
- Custom JWT-based authentication
  - User registration via email/password
  - Password hashing: BCrypt.Net-Next 4.0.3
  - Token generation: System.IdentityModel.Tokens.Jwt 8.1.2
  - Token storage: AsyncStorage on mobile (`@react-native-async-storage/async-storage`)
  - Token expiry: Configurable via `Jwt:ExpiryMinutes` (default 10,080 minutes = 7 days)
  - Refresh expiry: Configurable via `Jwt:RefreshExpiryDays` (default 3,650 days)

**Implementation:**
- Backend: `apps/api/BHMHockey.Api/Services/AuthService.cs`
- Frontend: `apps/mobile/stores/authStore.ts` (Zustand store)
- API Client: `packages/api-client/src/services/auth.ts`
- Interceptor: Automatic Bearer token added to all HTTP requests via axios interceptor
- 401 Response Handling: Automatic logout, token cleared from storage

**Secrets Location:**
- Development: `.env` file (gitignored, use `.env.example` as template)
- Production: DigitalOcean App Platform console (environment variables marked as `SECRET` type)
- Never commit `.env` file or actual secrets

## Monitoring & Observability

**Error Tracking:**
- Not integrated (no Sentry, DataDog, etc.)
- Errors logged to console/stdout
- .NET logging configured in `appsettings.json`

**Logs:**
- Backend: Structured logging via ILogger<T> (.NET built-in)
  - Log levels configured in `appsettings.json` (Default: Information, AspNetCore: Warning, EF: Warning)
  - Console output during development, App Platform captures stdout in production
- Mobile: Console logging (React Native debugger, Expo CLI)
- Health check endpoint: `GET /health` (returns 200 if database is accessible)

**Metrics:**
- No metrics collection (Prometheus, CloudWatch, etc.)
- Manual monitoring via App Platform dashboard

## CI/CD & Deployment

**Hosting:**
- Production: DigitalOcean App Platform (PaaS)
- Database: DigitalOcean Managed PostgreSQL 15
- Build: Automatic from GitHub on push to `main` branch (via `.do/app.yaml`)
- Dockerfile: `apps/api/Dockerfile` (multi-stage .NET 8 build)

**Git Integration:**
- Repository: GitHub (repo: adilp/bhmHockeyv3)
- Branch: Deploy on push to `main`
- Configuration: `.do/app.yaml` (DigitalOcean App Platform spec)
- Deploy command: `doctl apps create --spec .do/app.yaml` or `doctl apps update [APP_ID] --spec .do/app.yaml`

**Mobile Deployment:**
- OTA Updates: Expo EAS (Build and Submit)
  - Config: `apps/mobile/app.json` (runtimeVersion, updates URL)
  - Expo project ID: `f6225f7a-0181-4b8c-b44a-cea3e0017cfa`
  - Updates enabled: Always check for updates on launch
  - Non-native changes deployable via: `npx eas-cli update --branch production`
- Native Builds: Requires new build via EAS
  - iOS: `npx eas-cli build --platform ios --profile production && npx eas-cli submit --platform ios`
  - Android: `npx eas-cli build --platform android --profile production`
  - Managed by: Expo Application Services (EAS)

**API Deployment Process:**
1. Push code to GitHub `main` branch
2. DigitalOcean App Platform auto-detects via `.do/app.yaml`
3. Builds Docker image using `apps/api/Dockerfile`
4. Runs migrations automatically on startup
5. Health check validates `/health` endpoint
6. Load balancer routes traffic to container

## Environment Configuration

**Required Environment Variables:**

Development (.env file):
```
ConnectionStrings__DefaultConnection=Host=localhost;Port=5433;Database=bhmhockey;Username=bhmhockey;Password=yourpassword
Jwt__Secret=dev-secret-key-for-local-development-only-min-32-chars
Jwt__Issuer=http://localhost:5001
Jwt__Audience=http://localhost:5001
Jwt__ExpiryMinutes=60
Jwt__RefreshExpiryDays=7
Expo__AccessToken=<your-expo-access-token>
ASPNETCORE_ENVIRONMENT=Development
Logging__LogLevel__Default=Information
```

Production (DigitalOcean Console):
```
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
DATABASE_URL=${bhmhockey.DATABASE_URL}  # Auto-injected from managed database
Jwt__Secret=<generate: openssl rand -base64 32>
Jwt__Issuer=https://${APP_DOMAIN}
Jwt__Audience=https://${APP_DOMAIN}
Jwt__ExpiryMinutes=10080  # 7 days
Cors__AllowedOrigins=https://${APP_DOMAIN}
Expo__AccessToken=<optional, required for push notifications>
```

**Secrets Location:**
- Development: `.env` file (never commit)
- Production: DigitalOcean console environment variables (marked as `SECRET` type for encryption at rest)
- Template: `.env.example` shows all required variables

## Webhooks & Callbacks

**Incoming Webhooks:**
- None currently implemented
- Health check endpoint: `GET /health` (App Platform only, no external webhooks)

**Outgoing Webhooks:**
- None currently implemented
- All communication is request/response based (Axios HTTP calls)

**Push Notification Callbacks:**
- Mobile app receives push notifications from Expo service
- On receipt, notification data is persisted to `Notifications` table
- User can view notification history in-app via `notificationStore`

## API Endpoints & Base URLs

**Development:**
- API: `http://localhost:5001` (or configurable via `config/api.ts`)
- Swagger docs: `http://localhost:5001/swagger`
- Health: `http://localhost:5001/health`
- Web routes: `http://localhost:5001/api/[controller]`

**Production:**
- API: `https://bhmhockey-mb3md.ondigitalocean.app` (App Platform domain)
- All traffic over HTTPS
- Path prefix: `/api` is preserved during ingress routing
- Controller routes: `POST /api/auth/login`, `GET /api/users/{id}`, etc.

**Mobile App Configuration:**
- Hardcoded API URL: `getApiUrl()` returns `https://bhmhockey-mb3md.ondigitalocean.app/api`
- Local dev override: Uncomment line in `apps/mobile/config/api.ts` and set local IP
- Platform-specific endpoints:
  - iOS Simulator: `http://localhost:5001/api`
  - Android Emulator: `http://10.0.2.2:5001/api` (special IP for host)
  - Physical Device: `http://{YOUR_LOCAL_IP}:5001/api`

---

*Integration audit: 2026-01-24*
