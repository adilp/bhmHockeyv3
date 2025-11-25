using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using BHMHockey.Api.Data;
using BHMHockey.Api.Services;

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
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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

// Register application services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

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
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
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
