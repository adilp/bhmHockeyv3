using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;

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
            user.SkillLevel,
            user.Position,
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

        if (request.SkillLevel != null)
            user.SkillLevel = request.SkillLevel;

        if (request.Position != null)
            user.Position = request.Position;

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
            user.SkillLevel,
            user.Position,
            user.VenmoHandle,
            user.Role,
            user.CreatedAt
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
