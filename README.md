# BHM Hockey

A mobile app for organizing hockey pickup games, organizations, and tournaments. Players discover and register for events; organizers manage rosters, payments, waitlists, and push notifications. This repo is a Yarn-workspaces monorepo containing the React Native app, the .NET API, and shared TypeScript packages.

**Tech stack**

| Layer | Tech |
|---|---|
| Mobile | React Native 0.81 (Expo SDK 54), Expo Router 6, Zustand 5, TypeScript |
| API | .NET 8 (ASP.NET Core Web API), Entity Framework Core 8, PostgreSQL (Npgsql) |
| Shared | `@bhmhockey/shared` (types/constants), `@bhmhockey/api-client` (Axios client with auth) |
| Deploy | DigitalOcean App Platform (API), EAS Build/Update (mobile) |

## Prerequisites

- **Node.js >= 18** and **Yarn** (classic; this repo uses Yarn workspaces)
- **.NET 8 SDK** (`dotnet --version` should show 8.x)
- **EF Core CLI** — `dotnet tool install --global dotnet-ef` (one-time; needed to *create* database migrations — running the API applies them automatically)
- **PostgreSQL** — any of:
  - **OrbStack / Docker** (recommended — matches the checked-in config with zero edits). Local dev config expects Postgres on port **5433**, not the standard 5432.
  - **Homebrew Postgres** (runs on **5432** — you must override the connection string, see First-Time Setup step 3 Option B, or the API fails with "connection refused")
- **Xcode** (iOS Simulator) and/or **Android Studio** (emulator), or the **Expo Go** app on a physical device

## First-Time Setup

### 1. Clone and install

```bash
git clone <repo-url> bhmhockey2
cd bhmhockey2
yarn install
```

### 2. Understand how local config is loaded (30 seconds, saves an hour)

The API does **not** read the root `.env` file — nothing loads it locally (there is no dotenv loader in `Program.cs`). `.env.example` is a reference for the environment variables set in the DigitalOcean console for production. You do **not** need to create a `.env` to run locally.

Local API config comes from `apps/api/BHMHockey.Api/appsettings.Development.json`, which expects:

```
Host=localhost;Port=5433;Database=bhmhockey;Username=bhmhockey;Password=password
```

Resolution order (see `Program.cs`): if a `DATABASE_URL` env var is set it wins; otherwise `ConnectionStrings:DefaultConnection` is used (env var `ConnectionStrings__DefaultConnection` overrides the JSON file). The JWT secret is auto-defaulted in development — no config needed.

### 3. Start a local Postgres

**Option A — OrbStack or Docker (matches defaults, no config edits):**

```bash
docker run -d --name bhmhockey-postgres \
  -e POSTGRES_DB=bhmhockey \
  -e POSTGRES_USER=bhmhockey \
  -e POSTGRES_PASSWORD=password \
  -p 5433:5432 \
  postgres:16
```

Note the port mapping: host **5433** → container 5432, so it lines up with `appsettings.Development.json` as-is.

**Option B — Homebrew Postgres (runs on 5432):**

```bash
brew services start postgresql@16   # or your installed version
psql -d postgres -c "CREATE USER bhmhockey WITH PASSWORD 'password';"
psql -d postgres -c "CREATE DATABASE bhmhockey OWNER bhmhockey;"
```

Then point the API at port 5432 — either export an override in the shell where you run the API (keeps the checked-in file untouched):

```bash
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=bhmhockey;Username=bhmhockey;Password=password"
```

or edit `Port=5433` → `Port=5432` in `apps/api/BHMHockey.Api/appsettings.Development.json` (just don't commit that change).

### 4. Run it

```bash
yarn dev        # API + Metro bundler together
# or in two terminals:
yarn api        # .NET API on http://localhost:5001 (binds 0.0.0.0:5001)
yarn mobile     # Expo/Metro bundler on port 8081
```

EF Core **migrations apply automatically on API startup** — no `dotnet ef database update` needed. First startup is slower because of this.

### 5. Verify

```bash
curl http://localhost:5001/health     # → Healthy
open http://localhost:5001/swagger    # interactive API docs
```

Then press `i` (iOS simulator) or `a` (Android emulator) in the Metro terminal, or scan the QR code with Expo Go.

## Mobile App → API Connection

The API base URL comes from the `EXPO_PUBLIC_API_URL` env var, read from `apps/mobile/.env` (gitignored). When unset — e.g. in release builds — the app defaults to the **production** API. To develop against your local API:

```bash
cd apps/mobile
cp .env.example .env    # then uncomment the line for your platform
```

| Where the app runs | EXPO_PUBLIC_API_URL |
|---|---|
| iOS Simulator | `http://localhost:5001/api` |
| Android Emulator | `http://10.0.2.2:5001/api` |
| Physical device | `http://<your-Mac-LAN-IP>:5001/api` (find it: `ipconfig getifaddr en0`) |

Env values are inlined when the JS bundle is built — restart Metro (`npx expo start --clear`) after changing `.env`. In dev builds, the in-app `EnvBanner` shows which API you're connected to.

## Running Tests

```bash
yarn test              # everything: API tests, then frontend tests
yarn test:api          # .NET unit tests (xUnit) — uses EF Core InMemory, no Postgres required
yarn test:frontend     # Jest in @bhmhockey/shared, @bhmhockey/api-client, and mobile
yarn test:shared       # just packages/shared
yarn test:api-client   # just packages/api-client
yarn test:mobile       # just apps/mobile
yarn test:watch        # mobile Jest in watch mode
```

Type-checking and linting:

```bash
yarn workspace @bhmhockey/mobile type-check   # tsc --noEmit
yarn lint                                     # ESLint on the mobile app
```

More detail in [docs/TESTING.md](./docs/TESTING.md).

## Monorepo Structure

```
bhmhockey2/
├── apps/
│   ├── api/                  # .NET 8 API
│   │   ├── BHMHockey.Api/        # Controllers, Services, Models, Data (EF Core)
│   │   └── BHMHockey.Api.Tests/  # xUnit tests
│   └── mobile/               # React Native Expo app
│       ├── app/                  # Expo Router screens
│       ├── components/           # Shared UI components
│       ├── stores/               # Zustand stores (all API calls go through stores)
│       └── config/               # API URL config
├── packages/
│   ├── shared/               # TS types mirroring backend DTOs + constants
│   └── api-client/           # Axios client, auth interceptors, per-domain services
├── docs/                     # Guides, PRD, deployment docs
└── .do/app.yaml              # DigitalOcean App Platform spec
```

## Documentation Index

| Doc | What's in it |
|---|---|
| [CLAUDE.md](./CLAUDE.md) | Working notes for the repo: data flow, cross-cutting gotchas, version-bump rules, current project status |
| [apps/api/CLAUDE.md](./apps/api/CLAUDE.md) | API patterns: auth/roles, migrations, validation rules, error handling, gotchas |
| [apps/mobile/CLAUDE.md](./apps/mobile/CLAUDE.md) | Mobile patterns: Zustand usage, theme/design system, navigation, gotchas |
| [docs/MONOREPO_GUIDE.md](./docs/MONOREPO_GUIDE.md) | Deeper monorepo setup and workspace mechanics |
| [docs/APP_DEPLOYMENT.md](./docs/APP_DEPLOYMENT.md) | Mobile app deployment (EAS builds, OTA updates, store submission) |
| [docs/DIGITALOCEAN_DEPLOYMENT.md](./docs/DIGITALOCEAN_DEPLOYMENT.md) | API + database deployment on DigitalOcean |
| [docs/TESTING.md](./docs/TESTING.md) | Testing strategy and how to write tests |
| [docs/PRD.md](./docs/PRD.md) | Product requirements |
| [docs/Implementation.md](./docs/Implementation.md) | Original phased implementation plan (historical reference) |
| [docs/design-reference-rows.html](./docs/design-reference-rows.html) | Design-system reference (open in a browser) — the mobile theme mirrors it |

## Git Workflow

1. Branch off `main`: `git checkout -b your-feature main`
2. Open a PR into `main`
3. CI must pass before merge — GitHub Actions ([.github/workflows/ci.yml](./.github/workflows/ci.yml)) runs the API test suite and the mobile typecheck/tests
4. Keep backend DTOs and `packages/shared` types in sync in the same PR (see "Making a Backend Change" below)

Note: pushes to `main` auto-deploy the API to DigitalOcean, so don't merge anything you wouldn't ship.

## Making a Backend Change

There is no codegen — the C# DTO ↔ TypeScript type contract is maintained **by hand**. Any API change that touches data shapes should land in one PR containing all of:

1. **Entity** — add/update in `apps/api/BHMHockey.Api/Models/Entities/`
2. **Migration** — `yarn api:migrations AddMyField` (needs the EF Core CLI from Prerequisites). Migrations are **additive-only — never drop columns**. They apply automatically the next time the API starts.
3. **DTO** — update in `Models/DTOs/`. Careful with `UserDto`: it is constructed in several services and every site must be updated, or fields come back null (see [apps/api/CLAUDE.md](./apps/api/CLAUDE.md) → UserDto Updates).
4. **Shared types** — mirror the DTO change in `packages/shared/src/types/` so the mobile app sees the same shape.
5. **Service + controller logic**, with tests (patterns in [docs/TESTING.md](./docs/TESTING.md) and existing test files).
6. **Verify** via Swagger (`http://localhost:5001/swagger`), then run `yarn test`.

## Deployment

- **API**: auto-deploys from `main` via DigitalOcean App Platform (`.do/app.yaml`). Details: [docs/DIGITALOCEAN_DEPLOYMENT.md](./docs/DIGITALOCEAN_DEPLOYMENT.md)
- **Mobile**: EAS builds and store submission. Details: [docs/APP_DEPLOYMENT.md](./docs/APP_DEPLOYMENT.md)
- **OTA update** (JS/styles/assets only — native changes need a new build):

```bash
cd apps/mobile
npx eas-cli update --branch production --message "Description"
```

## Troubleshooting

**API fails at startup with database "connection refused"**
Almost always the 5432-vs-5433 port mismatch. The checked-in config expects Postgres on **5433** (OrbStack/Docker mapping); Homebrew Postgres listens on **5432**. Fix per First-Time Setup step 3 Option B. Check what's actually listening: `lsof -iTCP -sTCP:LISTEN | grep 543`.

**Android emulator can't reach the API**
`localhost` inside the emulator is the emulator itself. Set `EXPO_PUBLIC_API_URL=http://10.0.2.2:5001/api` in `apps/mobile/.env` and restart Metro.

**Physical device can't reach the API**
Set `EXPO_PUBLIC_API_URL` to your Mac's LAN IP (`ipconfig getifaddr en0`) in `apps/mobile/.env` and restart Metro, make sure phone and Mac are on the same Wi-Fi, and allow incoming connections through the macOS firewall. The API already binds `0.0.0.0:5001` in development.

**Push notifications don't work**
They require a physical device — simulators/emulators can't receive Expo push notifications.

**Port already in use**
Something is holding 5001 (API) or 8081 (Metro). Identify it before killing — unrelated apps sometimes squat these ports:

```bash
lsof -nP -iTCP:5001 -iTCP:8081 -sTCP:LISTEN   # see what's listening
kill <PID>                                     # only if it's yours
```

**Metro acting weird after dependency or asset changes**

```bash
cd apps/mobile && npx expo start --clear
```

## Project Status

Phases 1–4 are complete (auth, organizations, events, registrations, payments, push notifications, multi-admin, notification center); next up is Phase 5 (waitlist). The canonical, always-current status line lives at the top of [CLAUDE.md](./CLAUDE.md) — trust that over anything else, including this paragraph.
