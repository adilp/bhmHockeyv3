using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Web;
using BHMHockey.Api.Data;
using BHMHockey.Api.Services;
using BHMHockey.Api.Services.Background;

var builder = WebApplication.CreateBuilder(args);

// Configure for network access (React Native development)
// Production uses ASPNETCORE_URLS from app.yaml (port 8080)
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://0.0.0.0:5001");
}

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database Configuration
// DigitalOcean provides DATABASE_URL in postgres:// format, convert to .NET connection string
var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
string connectionString;

if (!string.IsNullOrEmpty(databaseUrl))
{
    // Convert DigitalOcean DATABASE_URL format to .NET connection string
    // Input:  postgresql://user:pass@host:port/database?sslmode=require
    // Output: Host=host;Port=port;Database=database;Username=user;Password=pass;SSL Mode=Require;Trust Server Certificate=true
    connectionString = ConvertDatabaseUrl(databaseUrl);
    Console.WriteLine("📦 Using DATABASE_URL from environment");
    // Log connection details (mask password for security)
    var uri = new Uri(databaseUrl.Replace("postgres://", "postgresql://"));
    Console.WriteLine($"📦 Database Host: {uri.Host}");
    Console.WriteLine($"📦 Database Port: {uri.Port}");
    Console.WriteLine($"📦 Database Name: {uri.AbsolutePath.TrimStart('/')}");
    Console.WriteLine($"📦 Database User: {uri.UserInfo.Split(':')[0]}");
}
else
{
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
    Console.WriteLine("📦 Using connection string from appsettings");
}

var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.EnableDynamicJson();  // Required for Dictionary<string, string> JSON serialization in Npgsql 8.0+
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource));

// CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowExpoApp", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Allow all origins in development for React Native
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Production: Use configured origins
            var allowedOrigins = builder.Configuration["Cors:AllowedOrigins"]?.Split(',')
                ?? new[] { "https://yourdomain.com" };
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// JWT Authentication (allow empty secret in development for initial setup)
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
{
    if (builder.Environment.IsDevelopment())
    {
        jwtSecret = "dev-secret-key-for-local-development-only-min-32-chars";
        Console.WriteLine("⚠️  WARNING: Using development JWT secret. Set Jwt:Secret in .env for production!");
    }
    else
    {
        throw new InvalidOperationException("JWT Secret not configured in production");
    }
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

// HTTP Client for Expo Push Notifications
builder.Services.AddHttpClient("ExpoPush", client =>
{
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
});

// Register application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IOrganizationAdminService, OrganizationAdminService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IWaitlistService, WaitlistService>();
builder.Services.AddScoped<IEventReminderService, EventReminderService>();
builder.Services.AddScoped<INotificationPersistenceService, NotificationPersistenceService>();
builder.Services.AddScoped<IBadgeService, BadgeService>();
builder.Services.AddScoped<ITournamentService, TournamentService>();
builder.Services.AddScoped<ITournamentLifecycleService, TournamentLifecycleService>();
builder.Services.AddScoped<ITournamentTeamService, TournamentTeamService>();
builder.Services.AddScoped<ITournamentMatchService, TournamentMatchService>();
builder.Services.AddScoped<IBracketGenerationService, BracketGenerationService>();
builder.Services.AddScoped<ITournamentRegistrationService, TournamentRegistrationService>();
builder.Services.AddScoped<ITournamentTeamAssignmentService, TournamentTeamAssignmentService>();
builder.Services.AddScoped<ITournamentTeamMemberService, TournamentTeamMemberService>();
builder.Services.AddScoped<ITournamentAuthorizationService, TournamentAuthorizationService>();
builder.Services.AddScoped<ITournamentAdminService, TournamentAdminService>();
builder.Services.AddScoped<ITournamentAuditService, TournamentAuditService>();
builder.Services.AddScoped<ITournamentAnnouncementService, TournamentAnnouncementService>();
builder.Services.AddScoped<IStandingsService, StandingsService>();
builder.Services.AddScoped<IRosterPublishService, RosterPublishService>();

// Background Services
builder.Services.AddHostedService<WaitlistBackgroundService>();
builder.Services.AddHostedService<EventReminderBackgroundService>();
builder.Services.AddHostedService<NotificationCleanupBackgroundService>();
builder.Services.AddHostedService<RosterPublishBackgroundService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Apply migrations automatically on startup (for App Platform)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("🔄 Starting database migration...");
        Console.WriteLine("🔄 Starting database migration...");

        // Log ALL migrations known to EF Core
        var allMigrations = db.Database.GetMigrations().ToList();
        Console.WriteLine($"📋 All migrations in assembly: {allMigrations.Count}");
        foreach (var m in allMigrations)
        {
            Console.WriteLine($"   - {m}");
        }

        // Log applied migrations from database
        var appliedMigrations = db.Database.GetAppliedMigrations().ToList();
        Console.WriteLine($"📋 Applied migrations in DB: {appliedMigrations.Count}");
        foreach (var m in appliedMigrations)
        {
            Console.WriteLine($"   - {m}");
        }

        // Log pending migrations
        var pendingMigrations = db.Database.GetPendingMigrations().ToList();
        Console.WriteLine($"📋 Pending migrations: {pendingMigrations.Count}");
        foreach (var m in pendingMigrations)
        {
            Console.WriteLine($"   - {m}");
        }

        if (!pendingMigrations.Any())
        {
            Console.WriteLine("✅ No pending migrations to apply");
        }

        db.Database.Migrate();

        logger.LogInformation("✅ Database migration completed successfully");
        Console.WriteLine("✅ Database migration completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ An error occurred while migrating the database: {Message}", ex.Message);
        Console.WriteLine($"❌ Migration error: {ex.Message}");
        Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
        // Re-throw in production to prevent app from starting with broken DB
        if (!app.Environment.IsDevelopment())
        {
            throw;
        }
    }
}

app.UseHttpsRedirection();
app.UseCors("AllowExpoApp");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Simple root endpoint
app.MapGet("/", () => new
{
    service = "BHM Hockey API",
    version = "1.0.0",
    status = "running"
});

// Privacy Policy page (required for App Store submission)
app.MapGet("/privacy", () => Results.Content(@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Privacy Policy - BHM Hockey</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            max-width: 800px;
            margin: 0 auto;
            padding: 40px 20px;
            line-height: 1.6;
            color: #333;
            background: #f9f9f9;
        }
        h1 { color: #0D1117; }
        h2 { color: #00D9C0; margin-top: 30px; }
        .updated { color: #666; font-size: 14px; }
    </style>
</head>
<body>
    <h1>Privacy Policy</h1>
    <p class='updated'>Last updated: December 2025</p>

    <h2>Information We Collect</h2>
    <p>BHM Hockey collects the following information to provide our services:</p>
    <ul>
        <li><strong>Account Information:</strong> Email address, name, and password for this app (encrypted)</li>
        <li><strong>Profile Information:</strong> Skill level, playing position, and Venmo handle (optional)</li>
        <li><strong>Device Information:</strong> Push notification tokens to send event notifications</li>
    </ul>

    <h2>How We Use Your Information</h2>
    <p>Your information is used solely to:</p>
    <ul>
        <li>Authenticate your account and provide app functionality</li>
        <li>Display your name to other users in event registrations</li>
        <li>Send push notifications about events you're registered for or organizations you follow</li>
        <li>Facilitate payments between event organizers and participants via Venmo</li>
    </ul>

    <h2>Data Sharing</h2>
    <p>We do not sell, trade, or share your personal information with third parties, except:</p>
    <ul>
        <li>Your name and Venmo handle may be visible to event organizers for payment coordination</li>
        <li>We use Expo's push notification service to deliver notifications to your device</li>
    </ul>

    <h2>Data Security</h2>
    <p>Your data is stored securely on encrypted servers. Passwords are hashed using industry-standard encryption and are never stored in plain text.</p>

    <h2>Data Retention</h2>
    <p>Your data is retained as long as your account is active. You may request deletion of your account and associated data at any time.</p>

    <h2>Your Rights</h2>
    <p>You have the right to:</p>
    <ul>
        <li>Access your personal data through the app</li>
        <li>Update or correct your information</li>
        <li>Request deletion of your account</li>
        <li>Opt out of push notifications through your device settings</li>
    </ul>

    <h2>Contact Us</h2>
    <p>For questions about this privacy policy or to request data deletion, please contact us at: <strong>adilpatel420@gmail.com</strong></p>

    <h2>Changes to This Policy</h2>
    <p>We may update this privacy policy from time to time. We will notify users of any material changes through the app.</p>
</body>
</html>
", "text/html"));

app.Run();

// Helper function to convert DATABASE_URL to .NET connection string
static string ConvertDatabaseUrl(string databaseUrl)
{
    // DigitalOcean provides: postgresql://user:pass@host:port/database?sslmode=require
    // We need: Host=host;Port=port;Database=database;Username=user;Password=pass;SSL Mode=Require;Trust Server Certificate=true

    if (string.IsNullOrEmpty(databaseUrl)) return databaseUrl;

    // Handle both postgresql:// and postgres:// schemes
    if (!databaseUrl.StartsWith("postgresql://") && !databaseUrl.StartsWith("postgres://"))
        return databaseUrl;

    var uri = new Uri(databaseUrl.Replace("postgres://", "postgresql://"));
    var userInfo = uri.UserInfo.Split(':');
    var query = HttpUtility.ParseQueryString(uri.Query);

    var connectionStringBuilder = new StringBuilder();
    connectionStringBuilder.Append($"Host={uri.Host};");
    connectionStringBuilder.Append($"Port={uri.Port};");
    connectionStringBuilder.Append($"Database={uri.AbsolutePath.TrimStart('/')};");
    connectionStringBuilder.Append($"Username={userInfo[0]};");
    connectionStringBuilder.Append($"Password={Uri.UnescapeDataString(userInfo[1])};");

    // Handle SSL mode
    var sslMode = query["sslmode"];
    if (sslMode == "require")
    {
        connectionStringBuilder.Append("SSL Mode=Require;Trust Server Certificate=true;");
    }

    return connectionStringBuilder.ToString();
}

