using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface IUserService
{
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateUserProfileRequest request);
    Task UpdatePushTokenAsync(Guid userId, string pushToken);
    Task DeleteAccountAsync(Guid userId);
}
