using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace BHMHockey.Api.Services;

public class AuthService : IAuthService
{
    private const string ADMIN_EMAIL = "a@a.com";

    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly INotificationService _notificationService;

    public AuthService(AppDbContext context, IConfiguration configuration, INotificationService notificationService)
    {
        _context = context;
        _configuration = configuration;
        _notificationService = notificationService;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Check if user already exists (including deactivated accounts)
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (existingUser != null)
        {
            // Same error message whether active or deactivated to not leak account status
            throw new InvalidOperationException("User with this email already exists");
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Create user
        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Positions = request.Positions,
            VenmoHandle = request.VenmoHandle,
            Role = "Player"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Generate tokens
        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        var userDto = MapToUserDto(user);
        return new AuthResponse(token, refreshToken, userDto);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("This account has been deleted");
        }

        var token = GenerateJwtToken(user);
        var refreshToken = GenerateRefreshToken();

        var userDto = MapToUserDto(user);
        return new AuthResponse(token, refreshToken, userDto);
    }

    public Task<AuthResponse> RefreshTokenAsync(string refreshToken)
    {
        // Simplified refresh token logic for Phase 1
        // In production, store refresh tokens in database
        return Task.FromException<AuthResponse>(new NotImplementedException("Refresh token functionality will be implemented in next iteration"));
    }

    public async Task<bool> LogoutAsync(Guid userId)
    {
        // Simplified logout for Phase 1
        // In production, invalidate refresh tokens
        return await Task.FromResult(true);
    }

    public async Task<AdminPasswordResetResponse> AdminResetPasswordAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException("Cannot reset password for deactivated account");
        }

        // Generate a random temporary password
        var temporaryPassword = GenerateTemporaryPassword();

        // Hash and save
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new AdminPasswordResetResponse(
            user.Id,
            user.Email,
            temporaryPassword,
            "Password reset successful. Please share this temporary password with the user securely."
        );
    }

    public async Task<List<AdminUserSearchResult>> SearchUsersAsync(string query)
    {
        var queryLower = query.ToLower();
        var users = await _context.Users
            .Where(u =>
                u.Email.ToLower().Contains(queryLower) ||
                u.FirstName.ToLower().Contains(queryLower) ||
                u.LastName.ToLower().Contains(queryLower))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Take(20)
            .Select(u => new AdminUserSearchResult(
                u.Id,
                u.Email,
                u.FirstName,
                u.LastName,
                u.Role,
                u.IsActive
            ))
            .ToListAsync();

        return users;
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Verify current password
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new InvalidOperationException("Current password is incorrect");
        }

        // Validate new password
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            throw new InvalidOperationException("New password must be at least 6 characters");
        }

        // Hash and save new password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        // Find the user requesting password reset
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower() && u.IsActive);

        // Always return success message to prevent email enumeration
        var successMessage = "If an account exists with this email, Adil Patel will reach out to help you reset your password.";

        if (user == null)
        {
            // Don't reveal if email exists or not
            return new ForgotPasswordResponse(successMessage);
        }

        // Find admin user to notify
        var admin = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == ADMIN_EMAIL.ToLower() && u.IsActive);

        if (admin != null && !string.IsNullOrEmpty(admin.PushToken))
        {
            // Send push notification to admin
            await _notificationService.SendPushNotificationAsync(
                admin.PushToken,
                "Password Reset Request",
                $"{user.FirstName} {user.LastName} ({user.Email}) needs a password reset",
                new { type = "password_reset_request", userEmail = user.Email, userId = user.Id.ToString() },
                admin.Id,
                "password_reset_request"
            );
        }

        return new ForgotPasswordResponse(successMessage);
    }

    public async Task<AdminStatsResponse> GetAdminStatsAsync()
    {
        var totalUsers = await _context.Users.CountAsync();
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive);

        return new AdminStatsResponse(totalUsers, activeUsers);
    }

    public async Task<AdminUpdateRoleResponse> UpdateUserRoleAsync(Guid userId, string newRole)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException("Cannot update role for deactivated account");
        }

        var previousRole = user.Role;

        if (previousRole == newRole)
        {
            throw new InvalidOperationException($"User is already a {newRole}");
        }

        user.Role = newRole;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new AdminUpdateRoleResponse(
            user.Id,
            user.Email,
            previousRole,
            newRole,
            $"Successfully updated {user.FirstName} {user.LastName} from {previousRole} to {newRole}"
        );
    }

    public async Task<string> GetUserRoleAsync(Guid userId)
    {
        var user = await _context.Users
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => u.Role)
            .FirstOrDefaultAsync();

        return user ?? "Player";
    }

    private static string GenerateTemporaryPassword()
    {
        // Generate a readable temporary password: 3 words + 2 digits
        var words = new[] { "hockey", "skate", "puck", "stick", "goal", "ice", "rink", "team", "pass", "shot" };
        var random = new Random();
        var word1 = words[random.Next(words.Length)];
        var word2 = words[random.Next(words.Length)];
        var digits = random.Next(10, 99);

        return $"{char.ToUpper(word1[0])}{word1[1..]}{char.ToUpper(word2[0])}{word2[1..]}{digits}";
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSecret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT Secret not configured");
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var expiryMinutes = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        // Simplified for Phase 1
        return Guid.NewGuid().ToString();
    }

    private UserDto MapToUserDto(User user)
    {
        return new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Positions,
            user.VenmoHandle,
            user.Role,
            user.CreatedAt,
            null, // Badges
            0,    // TotalBadgeCount
            user.IsGhostPlayer
        );
    }
}
