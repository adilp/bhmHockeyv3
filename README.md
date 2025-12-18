# 🏒 BHM Hockey App

A React Native mobile app and C# .NET API for managing hockey organizations, pickup games, and tournaments.

## Quick Start

```bash
# Install dependencies
yarn install

# Start both API and mobile app
yarn dev

# Or run separately:
yarn api      # Start API on http://0.0.0.0:5001 have to run from the api folder
yarn mobile   # Start Expo dev server, have to run in the root foldr

# to run non dev simulator, have to run in the mobile folder:
npx expo run:ios
```

## Project Structure

This is a **monorepo** using Yarn workspaces:

```
bhmhockey2/
├── apps/
│   ├── mobile/     # React Native Expo app
│   └── api/        # C# .NET 8 API
├── packages/
│   ├── shared/     # Shared TypeScript types
│   └── api-client/ # API client for mobile
└── .do/
    └── app.yaml    # Digital Ocean deployment config
```

## Tech Stack

- **Mobile**: React Native (Expo 52), TypeScript, Expo Router
- **Backend**: C# .NET 8, ASP.NET Core Web API, PostgreSQL
- **State**: Zustand
- **API Client**: Axios with AsyncStorage
- **Deployment**: Digital Ocean App Platform (API), Expo Application Services (Mobile)

## Prerequisites

- Node.js 18+ and Yarn
- .NET 8 SDK
- PostgreSQL 15+
- Expo Go app (for mobile testing)

## Setup

### 1. Install Dependencies

```bash
yarn install
```

### 2. Set Up Database

```bash
# Create PostgreSQL database
createdb bhmhockey

# Run migrations
cd apps/api/BHMHockey.Api
dotnet ef database update
cd ../../..
```

### 3. Configure Environment

```bash
cp .env.example .env
# Edit .env with your database credentials
```

### 4. Start Development

```bash
# Start both API and mobile app
yarn dev
```

## Available Commands

### Development

```bash
yarn dev              # Run API + mobile together
yarn api              # Run API only (http://0.0.0.0:5001)
yarn mobile           # Run mobile only (Expo)
yarn mobile:ios       # Run on iOS simulator
yarn mobile:android   # Run on Android emulator
```

### API

```bash
yarn api:build          # Build API
yarn api:migrations     # Create new migration
yarn api:update-db      # Apply migrations
```

### Testing

```bash
yarn lint               # Lint mobile app
yarn type-check         # TypeScript type checking
```

## Documentation

- **[MONOREPO_GUIDE.md](./MONOREPO_GUIDE.md)** - Complete monorepo setup and development guide
- **[BHM.md](./BHM.md)** - Product requirements and specifications
- **[Phase1.md](./Phase1.md)** - Phase 1 MVP implementation plan (8 weeks)
- **[DEPLOYMENT_GUIDE.md](./DEPLOYMENT_GUIDE.md)** - Digital Ocean deployment instructions
- **[GETTING_STARTED.md](./GETTING_STARTED.md)** - Quick start guide
- **[apps/api/README.md](./apps/api/README.md)** - Backend API documentation

## API Access

The API is configured for network access to support React Native development:

- **iOS Simulator**: `http://localhost:5001/api`
- **Android Emulator**: `http://10.0.2.2:5001/api`
- **Physical Device**: `http://YOUR_COMPUTER_IP:5001/api`

API URLs are automatically configured in `apps/mobile/config/api.ts`.

## Features (Phase 1 MVP)

- ✅ User authentication (register/login)
- ✅ Organization creation and management
- ✅ Event creation and publishing
- ✅ Organization subscriptions
- ✅ Event registration
- ✅ Push notifications
- ✅ Mobile app with bottom tab navigation

## Deployment

### API (Digital Ocean App Platform)

```bash
# Push to GitHub - auto-deploys
git push origin main
```

Configuration in `.do/app.yaml`

### Mobile (Expo Application Services)

```bash
# Build for app stores
cd apps/mobile
eas build --platform all

# Submit to stores
eas submit --platform ios
eas submit --platform android
```

## Project Status

### Completed ✓
- [x] Monorepo structure with Yarn workspaces
- [x] API configured for network access (0.0.0.0:5001)
- [x] React Native Expo app with Expo Router
- [x] Shared packages (types and API client)
- [x] Database models (Users, Organizations, Events)
- [x] Authentication service (JWT)
- [x] CORS configuration for development
- [x] Health checks and auto-migrations
- [x] Digital Ocean App Platform configuration

### In Progress 🚧
- [ ] Complete API controllers (Organizations, Events, Users)
- [ ] Mobile app screens and components
- [ ] API integration in mobile app
- [ ] Push notification setup

### Upcoming (Phase 1)
- [ ] Event registration flow
- [ ] Organization discovery
- [ ] User profile management
- [ ] Payment tracking

## Development Workflow

1. **Make changes** to mobile app or API
2. **Test locally** with `yarn dev`
3. **Commit and push** to GitHub
4. **API auto-deploys** to Digital Ocean
5. **Mobile app** deployed via EAS when ready

## Common Issues

### API Not Accessible

- Ensure API binds to `0.0.0.0` (not `localhost`)
- Check firewall allows incoming connections
- Verify device is on same WiFi network

### CORS Errors

- Development mode automatically allows all origins
- For production, set `Cors__AllowedOrigins` in environment

### Migration Errors

```bash
# Reset database (development only)
cd apps/api/BHMHockey.Api
dotnet ef database drop
dotnet ef database update
```

See [MONOREPO_GUIDE.md](./MONOREPO_GUIDE.md) for detailed troubleshooting.

## Architecture

### Backend (apps/api/)

```
BHMHockey.Api/
├── Controllers/    # API endpoints
├── Services/       # Business logic
├── Models/
│   ├── Entities/   # Database models
│   └── DTOs/       # Data transfer objects
└── Data/           # DbContext & migrations
```

### Mobile (apps/mobile/)

```
mobile/
├── app/            # Expo Router screens
│   ├── (tabs)/     # Tab navigation screens
│   └── (auth)/     # Auth screens
└── config/         # App configuration (API URLs, etc.)
```

### Shared (packages/)

- **shared**: TypeScript types, constants, utilities
- **api-client**: API client with auth, storage, and services

## Contributing

1. Follow the Phase 1 implementation plan
2. Use TypeScript for all new code
3. Follow existing code structure and patterns
4. Test locally before pushing
5. Update documentation as needed

## License

Private project - All rights reserved

## Support

For questions or issues:
1. Check [MONOREPO_GUIDE.md](./MONOREPO_GUIDE.md)
2. Review [Phase1.md](./Phase1.md) for implementation details
3. See [GETTING_STARTED.md](./GETTING_STARTED.md) for setup help

---

**Ready to start?** Run `yarn dev` and start building! 🏒
