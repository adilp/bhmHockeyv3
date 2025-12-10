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

// Background Services
builder.Services.AddHostedService<WaitlistBackgroundService>();

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
