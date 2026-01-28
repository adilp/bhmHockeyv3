# BHM Hockey API

## Quick Start

```bash
yarn api                              # From monorepo root - starts on port 5001
curl http://localhost:5001/health     # Health check
open http://localhost:5001/swagger    # API docs
```

## Tech Stack

- .NET 8 Web API with Entity Framework Core
- PostgreSQL (port 5433 local, DigitalOcean in prod)
- JWT authentication with BCrypt password hashing
- Expo Push API for notifications

## Project Layout

- `Controllers/` - HTTP endpoints (Auth, Users, Organizations, Events, Notifications)
- `Services/` - Business logic, one interface + implementation per domain
- `Services/Background/` - Background jobs (waitlist, notification cleanup)
- `Models/Entities/` - Database models (User, Organization, Event, etc.)
- `Models/DTOs/` - Request/response objects grouped by domain
- `Data/AppDbContext.cs` - EF Core context with all configurations
- `Program.cs` - Startup, DI registration, middleware

## Commands

```bash
dotnet test                                    # Run 213+ unit tests
dotnet ef migrations add MigrationName         # Create new migration
dotnet run --project BHMHockey.Api             # Run API directly
```

## Critical Patterns

### Authorization
- Always use `OrganizationAdminService.IsUserAdminAsync()` for admin checks
- Never check `Organization.CreatorId` directly - use the admin service
- Event management: creator owns standalone events, org admins own org events

### Role-Based Access Control
- Roles are read from the JWT claim (`ClaimTypes.Role`) set at login time
- Valid roles: `"Player"`, `"Organizer"`, `"Admin"`
- Only Organizer/Admin can create events and organizations
- Only Admin can access admin panel endpoints (`/auth/admin/*`)
- If a role is changed, the user must re-login for it to take effect

### UserDto Updates
When adding fields to `User` entity, update ALL `UserDto` creation sites:
- `AuthService.MapToUserDto()`
- `EventService.MapToDto()` (multiple locations)
- `NotificationPersistenceService`

### Database
- Migrations auto-apply on startup (Program.cs)
- Always additive migrations - never drop columns
- JSON columns use JSONB in PostgreSQL, custom converters in tests
- All dates stored as UTC, converted to Central Time for display
- **EF Core uses PascalCase** for table/column names (e.g., `"UserBadges"`, `"FirstName"`) - use quotes in raw SQL

### Background Services
- Singletons that create scopes per execution
- Don't inject scoped services directly - create scope first
- WaitlistBackgroundService runs every 15 minutes
- NotificationCleanupBackgroundService runs daily (30-day retention)

## Validation Rules

| Field | Valid Values |
|-------|-------------|
| User Roles | `"Player"`, `"Organizer"`, `"Admin"` |
| Skill Levels | `"Gold"`, `"Silver"`, `"Bronze"`, `"D-League"` |
| Event Visibility | `"Public"`, `"OrganizationMembers"`, `"InviteOnly"` |
| Event Status | `"Draft"`, `"Published"`, `"Full"`, `"Completed"`, `"Cancelled"` |
| Payment Status | `null`, `"Pending"`, `"MarkedPaid"`, `"Verified"` |
| Position Keys | `"goalie"`, `"skater"` |

## Error Handling

- Services throw `InvalidOperationException` for business rule violations
- Services throw `UnauthorizedAccessException` for auth failures
- Controllers catch and map to `BadRequest`/`Unauthorized`
- Return `null` from services to hide resource existence (404 vs 403)

## Environment Config

Required in `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5433;Database=bhmhockey;..."
  },
  "Jwt": {
    "Secret": "min-32-character-secret-key",
    "Issuer": "BHMHockey",
    "Audience": "BHMHockeyApp"
  }
}
```

Production uses `DATABASE_URL` env var (auto-converted from postgres:// format).

## Naming Conventions

- Classes/Methods: PascalCase (`UserService`, `GetByIdAsync`)
- Parameters/Variables: camelCase (`userId`, `eventId`)
- Interfaces: `I` prefix (`IAuthService`)
- DTOs: Suffix with `Dto`, `Request`, `Response`

## Common Gotchas

- PostgreSQL runs on port **5433** (not 5432) in OrbStack
- First startup is slow due to auto-migrations
- Tests use InMemory DB with different JSON handling than PostgreSQL
- JWT tokens expire in 60 minutes (configurable via `Jwt:ExpiryMinutes`)
- Push notifications require valid `ExponentPushToken[]` format
- Cannot remove last admin from organization

## Adding Features

### New Entity
1. Create in `Models/Entities/`
2. Add `DbSet` to `AppDbContext`
3. Configure in `OnModelCreating()`
4. Run `dotnet ef migrations add Name`
5. Create DTOs, service + interface, controller
6. Register service in `Program.cs`
7. Write tests

### New Endpoint
1. Add controller method
2. Add service logic
3. Add auth checks
4. Write tests
5. Test via Swagger
