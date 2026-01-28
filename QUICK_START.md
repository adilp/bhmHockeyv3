# 🚀 BHM Hockey - Quick Start

Your project has been restructured into a **monorepo** with React Native mobile app and C# .NET API!

## ✅ What's Been Set Up

### Monorepo Structure
```
bhmhockey2/
├── apps/
│   ├── mobile/              # React Native Expo app ✅
│   └── api/                 # C# .NET 8 API (moved from backend/) ✅
├── packages/
│   ├── shared/              # Shared TypeScript types ✅
│   └── api-client/          # API client for mobile ✅
├── package.json             # Root workspace config ✅
├── lerna.json              # Monorepo config ✅
└── .do/app.yaml            # Updated for monorepo ✅
```

### API Configuration ✅
- Binds to `0.0.0.0:5001` for network access (React Native compatibility)
- CORS automatically configured for development (allows all origins)
- Development JWT secret auto-generated if not provided
- Swagger UI available at `http://localhost:5001/swagger`

### Mobile App ✅
- Expo Router for navigation
- Platform-specific API URLs (iOS, Android, physical device)
- Bottom tab navigation (Home, Discover, Events, Profile)
- TypeScript with workspace references to shared packages

### Shared Packages ✅
- **@bhmhockey/shared**: Types, constants, utilities
- **@bhmhockey/api-client**: API client with auth, storage, services

## 🎯 Next Steps

### 1. Install Dependencies

```bash
yarn install
```

This installs dependencies for:
- Root workspace
- apps/mobile
- apps/api (npm packages if any)
- packages/shared
- packages/api-client

### 2. Set Up Local Database

```bash
# Create PostgreSQL database
createdb bhmhockey

# Or using psql:
psql postgres
CREATE DATABASE bhmhockey;
CREATE USER bhmhockey WITH PASSWORD 'your-password';
GRANT ALL PRIVILEGES ON DATABASE bhmhockey TO bhmhockey;
\q
```

### 3. Configure Environment

```bash
# Copy environment template
cp .env.example .env

# Edit .env and update database credentials
# (Vim, nano, VS Code, whatever you prefer)
```

### 4. Run Database Migrations

```bash
cd apps/api/BHMHockey.Api

# Install EF Core tools (first time only)
dotnet tool install --global dotnet-ef

# Create migration
dotnet ef migrations add InitialCreate

# Apply migration
dotnet ef database update

# Return to root
cd ../../..
```

### 5. Start Development

```bash
# Option 1: Run both API and mobile together
yarn dev

# Option 2: Run separately (two terminals)
yarn api      # Terminal 1
yarn mobile   # Terminal 2
```

## 🧪 Testing the Setup

### Test API

```bash
# API should be running at http://0.0.0.0:5001

# Health check
curl http://localhost:5001/health
# Expected: {"status":"Healthy"}

# Swagger UI
open http://localhost:5001/swagger

# Test auth endpoint
curl -X POST http://localhost:5001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test123!",
    "firstName": "Test",
    "lastName": "User"
  }'
```

### Test Mobile App

1. **Install Expo Go** on your phone (iOS or Android)

2. **Start mobile app**:
   ```bash
   yarn mobile
   ```

3. **Scan QR code** with Expo Go

4. **App should open** with bottom tab navigation

### Test API Connection from Mobile

The mobile app is pre-configured to connect to your local API:

- **iOS Simulator**: Automatically uses `http://localhost:5001/api`
- **Android Emulator**: Automatically uses `http://10.0.2.2:5001/api`
- **Physical Device**: Update `apps/mobile/config/api.ts` with your computer's IP

To find your IP:
```bash
# macOS/Linux
ifconfig | grep "inet " | grep -v 127.0.0.1

# Windows
ipconfig

# Update apps/mobile/config/api.ts with the IP
```

## 📚 Documentation

- **[MONOREPO_GUIDE.md](./MONOREPO_GUIDE.md)** - Complete setup and development guide
- **[README.md](./README.md)** - Project overview
- **[Phase1.md](./Phase1.md)** - 8-week implementation plan
- **[DEPLOYMENT_GUIDE.md](./DEPLOYMENT_GUIDE.md)** - Production deployment

## ⚙️ Available Commands

```bash
# Development
yarn dev              # Run API + mobile together
yarn api              # Run API only
yarn mobile           # Run mobile only
yarn mobile:ios       # Run on iOS simulator
yarn mobile:android   # Run on Android emulator

# API
yarn api:build          # Build API
yarn api:migrations     # Create migration (add name)
yarn api:update-db      # Apply migrations

# Code Quality
yarn lint              # Lint mobile app
yarn type-check        # TypeScript checks
```

## 🔧 Common Issues

### "API not accessible from mobile device"

1. Ensure API binds to `0.0.0.0`:
   - Check `apps/api/BHMHockey.Api/Program.cs`
   - Should see: `builder.WebHost.UseUrls("http://0.0.0.0:5001");`

2. Check your firewall allows incoming connections

3. For physical devices, update IP in `apps/mobile/config/api.ts`

### "Database connection failed"

1. Check PostgreSQL is running:
   ```bash
   # macOS
   brew services list | grep postgresql

   # Linux
   systemctl status postgresql
   ```

2. Verify database exists:
   ```bash
   psql -l | grep bhmhockey
   ```

3. Check connection string in `.env`

### "Yarn workspace errors"

```bash
# Clear and reinstall
rm -rf node_modules
rm -rf apps/mobile/node_modules
rm -rf packages/*/node_modules
yarn install
```

### "Expo Metro bundler issues"

```bash
cd apps/mobile
expo start -c  # Clear cache
```

## 🎨 Project Structure

### Apps

**apps/mobile** - React Native Expo app
- `app/` - Expo Router screens
- `config/` - API configuration
- `app.json` - Expo configuration

**apps/api** - C# .NET 8 API
- `BHMHockey.Api/` - Main API project
  - `Controllers/` - API endpoints
  - `Services/` - Business logic
  - `Models/` - Entities and DTOs
  - `Data/` - DbContext and migrations

### Packages

**packages/shared** - TypeScript shared code
- `types/` - User, Event, Organization types
- `constants/` - Skill levels, statuses, etc.
- `utils/` - Validation, formatting helpers

**packages/api-client** - API client for mobile
- `client.ts` - Axios instance with interceptors
- `services/` - Auth, events, organizations
- `storage/` - AsyncStorage for tokens

## 🚀 What's Different from Original Plan?

### Before (Original Plan)
```
bhmhockey2/
└── backend/              # Backend only
    └── BHMHockey.Api/
```

### After (Monorepo)
```
bhmhockey2/
├── apps/
│   ├── mobile/           # NEW: React Native app
│   └── api/              # MOVED: from backend/
└── packages/             # NEW: Shared code
    ├── shared/
    └── api-client/
```

### Key Changes

1. **Backend moved** from `backend/` to `apps/api/`
2. **Mobile app created** in `apps/mobile/`
3. **Shared packages** for code reuse
4. **Yarn workspaces** for dependency management
5. **API configured** for network access (0.0.0.0:5001)
6. **Platform-specific** API URLs in mobile app
7. **Development CORS** allows all origins

## 🎯 Ready to Build?

1. **✅ Install dependencies**: `yarn install`
2. **✅ Set up database**: Follow step 2 above
3. **✅ Configure .env**: Copy and edit .env.example
4. **✅ Run migrations**: Follow step 4 above
5. **✅ Start development**: `yarn dev`

Then follow **[Phase1.md](./Phase1.md)** for the 8-week implementation plan!

## 💡 Pro Tips

- Use `yarn dev` to run everything together
- API Swagger UI is your friend: `http://localhost:5001/swagger`
- Mobile app auto-connects to local API - no config needed for simulator
- For physical device testing, update IP in `apps/mobile/config/api.ts`
- Push to GitHub auto-deploys API to Digital Ocean
- Use EAS for mobile app deployment to app stores

## 📖 Learn More

- **API Details**: [apps/api/README.md](./apps/api/README.md)
- **Monorepo Guide**: [MONOREPO_GUIDE.md](./MONOREPO_GUIDE.md)
- **Product Specs**: [BHM.md](./BHM.md)

---

**Questions?** Check [MONOREPO_GUIDE.md](./MONOREPO_GUIDE.md) for detailed troubleshooting!

**Ready?** Run `yarn dev` and start building! 🏒
