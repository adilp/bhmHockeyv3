using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;

    // Valid position keys and skill levels
    private static readonly HashSet<string> ValidPositionKeys = new() { "goalie", "skater" };
    private static readonly HashSet<string> ValidSkillLevels = new() { "Gold", "Silver", "Bronze", "D-League" };

    public UserService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;

        return new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Positions,
            user.VenmoHandle,
            user.Role,
            user.CreatedAt
        );
    }

    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateUserProfileRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        // Update fields if provided
        if (request.FirstName != null)
            user.FirstName = request.FirstName;

        if (request.LastName != null)
            user.LastName = request.LastName;

        if (request.PhoneNumber != null)
            user.PhoneNumber = request.PhoneNumber;

        // Validate and update positions
        if (request.Positions != null)
        {
            ValidatePositions(request.Positions);
            user.Positions = NormalizePositions(request.Positions);
        }

        if (request.VenmoHandle != null)
            user.VenmoHandle = request.VenmoHandle;

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Positions,
            user.VenmoHandle,
            user.Role,
            user.CreatedAt
        );
    }

    /// <summary>
    /// Validates position dictionary: keys must be "goalie" or "skater", values must be valid skill levels.
    /// </summary>
    private void ValidatePositions(Dictionary<string, string> positions)
    {
        if (positions.Count == 0)
        {
            throw new InvalidOperationException("At least one position is required");
        }

        foreach (var (key, value) in positions)
        {
            var normalizedKey = key.ToLowerInvariant();
            if (!ValidPositionKeys.Contains(normalizedKey))
            {
                throw new InvalidOperationException($"Invalid position key: '{key}'. Must be 'goalie' or 'skater'");
            }

            if (!ValidSkillLevels.Contains(value))
            {
                throw new InvalidOperationException($"Invalid skill level: '{value}'. Must be Gold, Silver, Bronze, or D-League");
            }
        }
    }

    /// <summary>
    /// Normalizes position keys to lowercase.
    /// </summary>
    private Dictionary<string, string> NormalizePositions(Dictionary<string, string> positions)
    {
        return positions.ToDictionary(
            kvp => kvp.Key.ToLowerInvariant(),
            kvp => kvp.Value
        );
    }

    public async Task UpdatePushTokenAsync(Guid userId, string pushToken)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        user.PushToken = pushToken;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}
