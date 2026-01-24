# Technology Stack

**Analysis Date:** 2026-01-24

## Languages

**Primary:**
- TypeScript 5.3.3 - React Native, Expo Router, Zustand stores, API client, shared types
- C# (8.0) - .NET 8 Web API, Entity Framework Core, services, controllers

**Secondary:**
- JavaScript (ES2020+) - Build scripts, dev tools via npm/yarn

## Runtime

**Environment:**
- Node.js 18.0.0+ (engines constraint in `package.json`)
- .NET 8 runtime (ASP.NET Core Web API)

**Package Manager:**
- Yarn (v4 inferred from workspaces) - Primary monorepo manager
- npm (used by individual packages)
- Lockfile: `yarn.lock` present

## Frameworks

**Core:**
- React 19.1.0 - Shared foundation (both mobile and web)
- React Native 0.81.5 - Mobile app runtime
- Expo SDK 54.0.25 - React Native development platform, hot reload, native modules
- .NET 8 - ASP.NET Core Web API framework

**Mobile Navigation:**
- Expo Router 6.0.15 - File-based routing for React Native (replaces React Navigation manually)
- React Navigation 7.0.14+ - Core navigation components (native-stack, bottom-tabs)

**State Management:**
- Zustand 5.0.2 - Mobile app state management (auth, events, orgs, notifications, tournaments)

**API Communication:**
- Axios 1.6.5 - HTTP client with interceptors for auth tokens

**Build/Dev:**
- Expo CLI 54.0.25 - Development server, builds, OTA updates
- Babel 7.25.0+ - TypeScript transpilation
- Jest 29.7.0 - Unit testing framework
- ts-jest 29.4.5+ - TypeScript support in Jest
- Concurrently 8.2.2 - Run API and Metro bundler simultaneously

**Entity Framework:**
- Entity Framework Core 8.0.0 - ORM for .NET
- Npgsql.EntityFrameworkCore.PostgreSQL 8.0.0 - PostgreSQL provider

**Testing:**
- @testing-library/react-native 12.4.3 - React Native component testing
- xUnit via dotnet test - .NET 8 unit testing

**Linting/Formatting:**
- ESLint 8.57.0 - JavaScript/TypeScript linting
- TypeScript 5.3.3 - Type checking across all packages

## Key Dependencies

**Critical:**
- `@bhmhockey/shared` (workspace) - TypeScript types matching backend DTOs (skill levels, positions, event status)
- `@bhmhockey/api-client` (workspace) - Axios HTTP client with JWT auth interceptors, handles 401 logout
- `@react-native-async-storage/async-storage` 2.2.0 - Secure token persistence (mobile)
- Entity Framework Core 8.0.0 - Database ORM with auto-migrations on startup

**Infrastructure:**
- `expo-notifications` 0.32.13 - Expo Push Notification service integration
- `expo-router` 6.0.15 - File-based routing
- `expo-device` 8.0.9 - Device detection (push notifications only on physical devices)
- `expo-constants` 18.0.10 - App configuration access
- `expo-updates` 29.0.15 - OTA update support
- `react-native-gesture-handler` 2.28.0 - Gesture and navigation support
- `react-native-reanimated` 4.1.1 - Animated interactions
- `BCrypt.Net-Next` 4.0.3 - Password hashing (.NET)
- `System.IdentityModel.Tokens.Jwt` 8.1.2 - JWT token creation/validation (.NET)
- `Microsoft.AspNetCore.Authentication.JwtBearer` 8.0.0 - JWT auth middleware (.NET)

## Configuration

**Environment:**
- Development: Uses local PostgreSQL on port 5433, auto-generates JWT secret
- Production: DigitalOcean App Platform (Docker container)
- Configured via: `.env` file (gitignored), `.env.example` for template
- Mobile API URL: Hardcoded to `https://bhmhockey-mb3md.ondigitalocean.app/api` (can be overridden for local dev)

**Key Configs Required:**
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string
- `Jwt__Secret` - Minimum 32-character secret (auto-generated in dev)
- `Jwt__Issuer` - Token issuer URL
- `Jwt__Audience` - Token audience
- `Jwt__ExpiryMinutes` - Token lifetime (default 10,080 minutes = 7 days)
- `Expo__AccessToken` - Expo project access token (optional, required for push notifications)
- `Cors__AllowedOrigins` - Comma-separated list of allowed origins (auto-all in development)
- `ASPNETCORE_ENVIRONMENT` - "Development" or "Production"
- `DATABASE_URL` - Production only, auto-injected by DigitalOcean

**Build:**
- `tsconfig.json` - TypeScript configuration (path aliases, strict mode)
- `apps/mobile/app.json` - Expo configuration (name, slug, version, plugins, updates)
- `apps/api/BHMHockey.Api.csproj` - .NET project file with package references
- `Dockerfile` - Multi-stage build for .NET 8 runtime deployment

## Platform Requirements

**Development:**
- Node.js 18.0.0 or higher
- .NET 8 SDK (dotnet 8.0.0 or higher)
- PostgreSQL 15 (local development)
- macOS, Windows, or Linux
- Xcode (for iOS development) or Android Studio (for Android development)
- Physical iOS/Android device (recommended for push notifications)

**Production:**
- DigitalOcean App Platform (PaaS)
- Managed PostgreSQL 15 database
- Docker container runtime
- App Platform automatically handles: HTTPS, load balancing, environment variables
- Database auto-injected via `DATABASE_URL` environment variable

## Package Manager & Lockfile

**Workspaces Configuration:**
- Root `package.json` defines 2 workspaces: `apps/*` and `packages/*`
- Allows shared code (`@bhmhockey/shared`, `@bhmhockey/api-client`) used by mobile and API
- `yarn install` installs all workspace dependencies
- Cross-workspace imports: `import { User } from '@bhmhockey/shared'`

**Scripts (Run from Root):**
- `yarn dev` - Runs API and mobile development servers simultaneously
- `yarn api` - .NET 8 API with hot reload on port 5001
- `yarn mobile` - Expo Metro bundler on port 8081
- `yarn test` - All tests (API + frontend)
- `yarn lint` - ESLint for mobile app

---

*Stack analysis: 2026-01-24*
