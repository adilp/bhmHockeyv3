# DigitalOcean App Platform Deployment Guide

This guide covers deploying the BHM Hockey monorepo to DigitalOcean App Platform.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                  DigitalOcean App Platform                   │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐  │
│  │   Ingress    │───▶│   API        │───▶│  Managed     │  │
│  │   Router     │    │   Service    │    │  PostgreSQL  │  │
│  │              │    │   (.NET 8)   │    │  Database    │  │
│  │  /api/* ────▶│    │   Port 8080  │    │              │  │
│  └──────────────┘    └──────────────┘    └──────────────┘  │
│                                                              │
└─────────────────────────────────────────────────────────────┘

Mobile App (Expo) ─────▶ https://your-app.ondigitalocean.app/api/*
```

## Critical: Ingress Routing

DigitalOcean App Platform routes requests based on path prefixes. Understanding this is crucial:

### Current Setup (Works!)

Your codebase is already configured correctly:

| Component | Configuration | Path |
|-----------|--------------|------|
| Frontend base URL | `http://localhost:5001/api` (dev) | - |
| API service calls | `/auth/login` | - |
| Full request URL | `http://localhost:5001/api/auth/login` | `/api/auth/login` |
| Controller route | `[Route("api/[controller]")]` | `/api/auth` |
| Match | ✓ | ✓ |

For production with DigitalOcean:

| Component | Configuration | Path |
|-----------|--------------|------|
| Frontend base URL | `https://domain.com/api` | - |
| API service calls | `/auth/login` | - |
| Full request URL | `https://domain.com/api/auth/login` | `/api/auth/login` |
| DO ingress (preserve_path_prefix: true) | Forwards as-is | `/api/auth/login` |
| Controller route | `[Route("api/[controller]")]` | `/api/auth` |
| Match | ✓ | ✓ |

### Why `preserve_path_prefix: true` is Essential

```yaml
routes:
  - path: /api
    preserve_path_prefix: true  # REQUIRED for current controller routes
```

- **With** `preserve_path_prefix: true`: `/api/auth/login` → container receives `/api/auth/login` ✓
- **Without**: `/api/auth/login` → container receives `/auth/login` ✗ (no match for `api/[controller]`)

### Alternative: Lesson Learned Approach

If you prefer cleaner controller routes without the `api/` prefix:

1. **Change all controllers** from `[Route("api/[controller]")]` to `[Route("[controller]")]`
2. **Remove** `preserve_path_prefix: true` from app.yaml
3. DO will strip `/api` prefix: `/api/auth/login` → `/auth/login` → matches `[controller]`

This is more flexible if you later add a static frontend site (DO can route `/api/*` to API and `/*` to frontend).

### Files Involved

| File | Current Value | Purpose |
|------|--------------|---------|
| `apps/mobile/config/api.ts` | Base URL with `/api` suffix | Frontend configuration |
| `packages/api-client/src/services/*.ts` | Paths without `/api` prefix | Service call paths |
| `apps/api/.../Controllers/*.cs` | `[Route("api/[controller]")]` | Controller routing |
| `.do/app.yaml` | `preserve_path_prefix: true` | DO ingress config |

---

## Setting Environment Variables in DigitalOcean

### Option 1: Via DigitalOcean Dashboard (Recommended for Secrets)

1. Go to your app in the [DigitalOcean Apps dashboard](https://cloud.digitalocean.com/apps)
2. Click on your app → **Settings** → **Components** → **api** → **Environment Variables**
3. Add/edit variables:

| Key | Type | Value | Notes |
|-----|------|-------|-------|
| `DATABASE_URL` | App Var | `${db.DATABASE_URL}` | Auto-populated from managed DB |
| `Jwt__Secret` | Secret | `<your-32-char-secret>` | Generate with `openssl rand -base64 32` |
| `Jwt__Issuer` | Variable | `https://${APP_DOMAIN}` | Uses your app's domain |
| `Jwt__Audience` | Variable | `https://${APP_DOMAIN}` | Uses your app's domain |
| `Expo__AccessToken` | Secret | `<your-expo-token>` | From expo.dev dashboard |

### Option 2: Via doctl CLI

```bash
# List current env vars
doctl apps list-env YOUR_APP_ID

# Update a variable
doctl apps update YOUR_APP_ID --spec .do/app.yaml

# Or set individual vars (requires app spec update)
```

### Option 3: Via app.yaml (Committed to Repo)

Update `.do/app.yaml` with your values. **Warning:** Don't commit secrets to git!

```yaml
envs:
  # Safe to commit (uses DO variable substitution)
  - key: DATABASE_URL
    value: ${db.DATABASE_URL}
  - key: Jwt__Issuer
    value: https://${APP_DOMAIN}

  # DON'T commit actual secrets - set in dashboard
  - key: Jwt__Secret
    type: SECRET
    value: SET_IN_DASHBOARD
```

### Database Connection (Automatic)

DigitalOcean automatically provides `DATABASE_URL` when you attach a managed database:

```
postgresql://username:password@host:25060/database?sslmode=require
```

The API's `Program.cs` automatically converts this to a .NET connection string format.

### Generating a Secure JWT Secret

```bash
# macOS/Linux
openssl rand -base64 32

# Or use any password generator, minimum 32 characters
```

---

## Pre-Deployment Checklist

### 1. Verify Controller Routes

Your controllers should have `[Route("api/[controller]")]` (current state is correct):

```bash
# Verify routes include api/ prefix
grep -r '\[Route\(' apps/api/BHMHockey.Api/Controllers/
# Expected: [Route("api/[controller]")] for all controllers
```

### 2. Environment Variables Needed

| Variable | Description | Example |
|----------|-------------|---------|
| `DATABASE_URL` | PostgreSQL connection string | `postgresql://user:pass@host:25060/db?sslmode=require` |
| `Jwt__Secret` | JWT signing key (min 32 chars) | `your-super-secret-jwt-key-at-least-32-characters` |
| `Jwt__Issuer` | JWT issuer | `https://your-app.ondigitalocean.app` |
| `Jwt__Audience` | JWT audience | `https://your-app.ondigitalocean.app` |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |
| `Expo__AccessToken` | Expo push notifications (optional) | `your-expo-access-token` |
| `Cors__AllowedOrigins` | Allowed CORS origins | `https://your-app.ondigitalocean.app` |

### 3. Update Mobile App Config

Update `apps/mobile/app.config.js`:

```javascript
export default ({ config }) => {
  const isDev = process.env.NODE_ENV !== 'production';

  return {
    ...config,
    extra: {
      ...config.extra,
      apiUrl: process.env.API_URL || (isDev
        ? 'http://localhost:5001/api'
        : 'https://YOUR-APP-NAME.ondigitalocean.app/api'),  // Update this!
      environment: isDev ? 'development' : 'production',
    }
  };
};
```

---

## Deployment Steps

### Step 1: Create DigitalOcean App

1. Go to [DigitalOcean App Platform](https://cloud.digitalocean.com/apps)
2. Click "Create App"
3. Connect your GitHub repository
4. Select the repository: `bhmhockey2`

### Step 2: Configure App Spec

Create or update `.do/app.yaml` in your repository root:

```yaml
name: bhmhockey
region: nyc
features:
  - buildpack-stack=ubuntu-22

databases:
  - engine: PG
    name: db
    num_nodes: 1
    size: db-s-dev-database
    version: "15"

services:
  - name: api
    github:
      repo: YOUR_GITHUB_USERNAME/bhmhockey2
      branch: main
      deploy_on_push: true
    source_dir: apps/api
    dockerfile_path: apps/api/Dockerfile
    http_port: 8080
    instance_count: 1
    instance_size_slug: basic-xxs
    routes:
      - path: /api
        preserve_path_prefix: true
      - path: /health
      - path: /
    health_check:
      http_path: /health
      initial_delay_seconds: 30
      period_seconds: 10
      timeout_seconds: 5
      success_threshold: 1
      failure_threshold: 3
    envs:
      - key: ASPNETCORE_ENVIRONMENT
        value: Production
      - key: ASPNETCORE_URLS
        value: http://+:8080
      - key: DATABASE_URL
        scope: RUN_TIME
        value: ${db.DATABASE_URL}
      - key: Jwt__Secret
        scope: RUN_TIME
        type: SECRET
        value: REPLACE_WITH_SECURE_SECRET_MIN_32_CHARS
      - key: Jwt__Issuer
        scope: RUN_TIME
        value: https://${APP_DOMAIN}
      - key: Jwt__Audience
        scope: RUN_TIME
        value: https://${APP_DOMAIN}
      - key: Cors__AllowedOrigins
        scope: RUN_TIME
        value: https://${APP_DOMAIN}
```

### Step 3: Update Connection String Handling

Your `Program.cs` needs to handle the `DATABASE_URL` environment variable format that DigitalOcean provides.

Add this helper before database configuration:

```csharp
// In Program.cs, before var connectionString = ...

static string ConvertDatabaseUrl(string databaseUrl)
{
    // DigitalOcean provides: postgresql://user:pass@host:port/database?sslmode=require
    // We need: Host=host;Port=port;Database=database;Username=user;Password=pass;SSL Mode=Require

    if (string.IsNullOrEmpty(databaseUrl)) return databaseUrl;
    if (!databaseUrl.StartsWith("postgresql://")) return databaseUrl;

    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':');
    var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

    var builder = new StringBuilder();
    builder.Append($"Host={uri.Host};");
    builder.Append($"Port={uri.Port};");
    builder.Append($"Database={uri.AbsolutePath.TrimStart('/')};");
    builder.Append($"Username={userInfo[0]};");
    builder.Append($"Password={userInfo[1]};");

    if (query["sslmode"] == "require")
    {
        builder.Append("SSL Mode=Require;Trust Server Certificate=true;");
    }

    return builder.ToString();
}
```

Then update the connection string retrieval:

```csharp
// Get connection string from DATABASE_URL (DigitalOcean) or appsettings
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
var connectionString = !string.IsNullOrEmpty(databaseUrl)
    ? ConvertDatabaseUrl(databaseUrl)
    : builder.Configuration.GetConnectionString("DefaultConnection");
```

### Step 4: Configure Ingress Routes

In the DigitalOcean dashboard or app.yaml:

| Path | Component | Preserve Path |
|------|-----------|---------------|
| `/api` | api | Yes |
| `/health` | api | No |
| `/` | api | No |

**Important:** `preserve_path_prefix: true` keeps `/api` in the path when routing to your container.

### Step 5: Deploy

```bash
# Commit your changes
git add .
git commit -m "Configure DigitalOcean deployment"
git push origin main
```

DigitalOcean will automatically:
1. Build the Docker image
2. Run database migrations (via `db.Database.Migrate()` in Program.cs)
3. Start the API service
4. Route traffic through ingress

---

## Post-Deployment Verification

### 1. Check API Health

```bash
curl https://YOUR-APP.ondigitalocean.app/health
# Expected: {"status":"Healthy"...}

curl https://YOUR-APP.ondigitalocean.app/
# Expected: {"service":"BHM Hockey API","version":"1.0.0","status":"running"}
```

### 2. Test Auth Endpoint

```bash
# Register a test user
curl -X POST https://YOUR-APP.ondigitalocean.app/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test123!","firstName":"Test","lastName":"User"}'
```

### 3. Update Mobile App

After deployment, update your mobile app's production URL:

```javascript
// apps/mobile/app.config.js
apiUrl: 'https://YOUR-APP.ondigitalocean.app/api'
```

Then build your production mobile app:

```bash
cd apps/mobile
eas build --platform all --profile production
```

---

## Troubleshooting

### Issue: 404 on /api/* endpoints

**Cause:** Controller routes have `api/` prefix, creating `/api/api/...`

**Fix:** Change `[Route("api/[controller]")]` to `[Route("[controller]")]` in all controllers

### Issue: Database connection failed

**Cause:** DATABASE_URL format not converted properly

**Fix:**
1. Check DATABASE_URL env var in DO dashboard
2. Verify ConvertDatabaseUrl function is implemented
3. Check logs: `doctl apps logs YOUR_APP_ID --type=run`

### Issue: CORS errors

**Cause:** Mobile app domain not in allowed origins

**Fix:** Update `Cors__AllowedOrigins` to include your app domain

### Issue: JWT validation failed

**Cause:** Secret/Issuer/Audience mismatch between environments

**Fix:** Ensure all Jwt__* env vars are set correctly in DO dashboard

### Viewing Logs

```bash
# Install doctl CLI
brew install doctl

# Authenticate
doctl auth init

# View deployment logs
doctl apps logs YOUR_APP_ID --type=deploy

# View runtime logs
doctl apps logs YOUR_APP_ID --type=run
```

---

## Cost Estimate

| Component | Size | Monthly Cost |
|-----------|------|--------------|
| API Service | basic-xxs | $5/month |
| Database | db-s-dev-database | $15/month |
| **Total** | | **~$20/month** |

For production with more capacity:
- API: basic-xs ($10/month) or basic-s ($20/month)
- Database: db-s-1vcpu-1gb ($15/month) or larger

---

## Security Checklist

- [ ] JWT secret is at least 32 characters and truly random
- [ ] Database SSL mode is enabled (`sslmode=require`)
- [ ] CORS origins are restricted to your domain
- [ ] HTTPS is enforced (App Platform does this automatically)
- [ ] Environment variables marked as SECRET for sensitive values
- [ ] No secrets in git repository

---

## Mobile App Deployment

After API is deployed, build your mobile app for production:

### 1. Update Production API URL

```javascript
// apps/mobile/app.config.js
export default ({ config }) => ({
  ...config,
  extra: {
    ...config.extra,
    apiUrl: 'https://YOUR-APP.ondigitalocean.app/api',
  }
});
```

### 2. Configure EAS Build

```bash
cd apps/mobile

# Initialize EAS (if not already done)
eas init

# Configure build profiles in eas.json
```

### 3. Build for App Stores

```bash
# iOS
eas build --platform ios --profile production

# Android
eas build --platform android --profile production
```

### 4. Submit to App Stores

```bash
eas submit --platform ios
eas submit --platform android
```

---

## Quick Reference

### API Endpoints After Deployment

| Endpoint | Method | URL |
|----------|--------|-----|
| Health | GET | `https://YOUR-APP.ondigitalocean.app/health` |
| Root | GET | `https://YOUR-APP.ondigitalocean.app/` |
| Register | POST | `https://YOUR-APP.ondigitalocean.app/api/auth/register` |
| Login | POST | `https://YOUR-APP.ondigitalocean.app/api/auth/login` |
| Profile | GET | `https://YOUR-APP.ondigitalocean.app/api/users/me` |
| Organizations | GET | `https://YOUR-APP.ondigitalocean.app/api/organizations` |
| Events | GET | `https://YOUR-APP.ondigitalocean.app/api/events` |

### Environment Variable Template

```env
# Required
DATABASE_URL=postgresql://user:pass@host:25060/db?sslmode=require
Jwt__Secret=your-super-secret-jwt-key-at-least-32-characters
Jwt__Issuer=https://your-app.ondigitalocean.app
Jwt__Audience=https://your-app.ondigitalocean.app
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080

# Optional
Cors__AllowedOrigins=https://your-app.ondigitalocean.app
Expo__AccessToken=your-expo-access-token
```
