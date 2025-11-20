# BHM Hockey API - Backend

ASP.NET Core 8 Web API for the BHM Hockey mobile application.

## Tech Stack

- **Framework**: ASP.NET Core 8 Web API
- **Database**: PostgreSQL 15 with Entity Framework Core
- **Authentication**: JWT Bearer tokens
- **Hosting**: Digital Ocean App Platform
- **Notifications**: Expo Push Notification Service

## Project Structure

```
BHMHockey.Api/
├── Controllers/          # API endpoints
├── Services/            # Business logic
├── Models/
│   ├── Entities/        # Database models
│   └── DTOs/            # Data transfer objects
├── Data/                # DbContext and migrations
├── Dockerfile           # Container configuration
└── Program.cs           # Application startup
```

## Local Development Setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- [Git](https://git-scm.com/)

### Step 1: Clone and Configure

```bash
# Clone the repository
git clone https://github.com/yourusername/bhm-hockey-backend.git
cd bhm-hockey-backend/backend

# Copy environment template
cp ../.env.example ../.env

# Edit .env with your local settings
nano ../.env
```

### Step 2: Database Setup

```bash
# Create local PostgreSQL database
psql -U postgres
CREATE DATABASE bhmhockey;
CREATE USER bhmhockey WITH PASSWORD 'dev';
GRANT ALL PRIVILEGES ON DATABASE bhmhockey TO bhmhockey;
\q
```

### Step 3: Run Migrations

```bash
cd BHMHockey.Api

# Install EF Core tools (if not already installed)
dotnet tool install --global dotnet-ef

# Create initial migration
dotnet ef migrations add InitialCreate

# Apply migrations to database
dotnet ef database update
```

### Step 4: Run the API

```bash
# Restore dependencies
dotnet restore

# Run the application
dotnet run

# API will be available at:
# http://localhost:5000
# https://localhost:5001
```

### Step 5: Test the API

```bash
# Check health endpoint
curl http://localhost:5000/health

# Check API info
curl http://localhost:5000/

# View Swagger documentation
open http://localhost:5000/swagger
```

## Digital Ocean App Platform Deployment

### Initial Setup

1. **Push to GitHub**
   ```bash
   git add .
   git commit -m "Initial commit"
   git push origin main
   ```

2. **Create App in Digital Ocean**
   - Log in to [Digital Ocean](https://cloud.digitalocean.com/)
   - Navigate to **App Platform** → **Create App**
   - Choose **GitHub** as source
   - Select your repository and branch
   - Enable **Autodeploy**

3. **Configure App Platform**
   - App Platform will auto-detect the Dockerfile
   - Alternatively, upload `.do/app.yaml` for full configuration
   - Update `cluster_name` in app.yaml with your DB cluster name

4. **Set Environment Variables**

   In the App Platform console, add these **encrypted** environment variables:

   ```bash
   # Generate a secure JWT secret
   openssl rand -base64 32
   ```

   Then add in DO console:
   - `Jwt__Secret` → (paste generated secret)
   - `Expo__AccessToken` → (get from expo.dev later)

5. **Deploy**
   - Click **"Deploy"**
   - First build takes 5-10 minutes
   - Watch build logs for errors
   - Migrations run automatically on startup

6. **Verify**
   ```bash
   # Replace with your actual app URL
   curl https://your-app-name.ondigitalocean.app/health
   ```

### Continuous Deployment

After initial setup, deployments are automatic:

1. Make code changes
2. Commit and push to GitHub
3. App Platform automatically builds and deploys
4. Zero-downtime rolling deployment

### Database Migrations

Migrations run automatically on app startup (see `Program.cs`). To create new migrations:

```bash
# Local development
cd BHMHockey.Api
dotnet ef migrations add YourMigrationName

# Commit and push - migrations will apply on next deployment
git add .
git commit -m "Add migration: YourMigrationName"
git push
```

## API Endpoints (Phase 1)

### Authentication
```
POST   /api/auth/register      - Register new user
POST   /api/auth/login         - Login
POST   /api/auth/refresh       - Refresh JWT token
```

### Users
```
GET    /api/users/me           - Get current user
PUT    /api/users/me           - Update profile
PUT    /api/users/me/push-token - Update push notification token
```

### Organizations
```
POST   /api/organizations      - Create organization
GET    /api/organizations      - List all organizations
GET    /api/organizations/{id} - Get organization details
PUT    /api/organizations/{id} - Update organization
```

### Subscriptions
```
POST   /api/organizations/{id}/subscribe    - Subscribe
DELETE /api/organizations/{id}/subscribe    - Unsubscribe
GET    /api/users/me/subscriptions         - My subscriptions
```

### Events
```
POST   /api/events             - Create event
GET    /api/events             - List all events
GET    /api/events/{id}        - Get event details
PUT    /api/events/{id}        - Update event
DELETE /api/events/{id}        - Cancel event
```

### Event Registration
```
POST   /api/events/{id}/register       - Register for event
DELETE /api/events/{id}/register       - Cancel registration
GET    /api/events/{id}/registrations  - List registrations
GET    /api/users/me/registrations     - My registrations
```

## Development Commands

```bash
# Run the app
dotnet run

# Run with watch (auto-reload)
dotnet watch run

# Run tests (when implemented)
dotnet test

# Create new migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update

# Rollback migration
dotnet ef database update PreviousMigrationName

# Build for production
dotnet publish -c Release

# Format code
dotnet format
```

## Environment Variables Reference

See `.env.example` for all available configuration options.

### Required for Production
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string (auto-injected by App Platform)
- `Jwt__Secret` - JWT signing key (generate with `openssl rand -base64 32`)

### Optional
- `Expo__AccessToken` - For push notifications (add in Phase 1 Week 8)
- `Cors__AllowedOrigins` - Frontend URLs (comma-separated)

## Troubleshooting

### Database Connection Issues

```bash
# Check PostgreSQL is running
sudo systemctl status postgresql

# Test connection
psql -h localhost -U bhmhockey -d bhmhockey
```

### Migration Issues

```bash
# Drop database and recreate (DEV ONLY!)
dotnet ef database drop
dotnet ef database update

# List migrations
dotnet ef migrations list
```

### App Platform Build Failures

- Check build logs in DO console
- Verify Dockerfile is in repository root
- Ensure all NuGet packages are restored
- Check environment variables are set correctly

## Security Notes

- Never commit `.env` file to Git
- Always use strong JWT secrets in production
- Database credentials should use managed database environment variables
- Enable SSL in production (automatic with App Platform)

## Support

For issues or questions:
- Check the [Phase 1 Implementation Plan](../Phase1.md)
- Review the [Product Requirements](../BHM.md)

## License

Private project - All rights reserved
