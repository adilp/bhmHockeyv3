namespace BHMHockey.Api.Models.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string? SkillLevel,
    string? Position,
    string? VenmoHandle,
    string Role,
    DateTime CreatedAt
);

public record UpdateUserProfileRequest(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    string? SkillLevel,
    string? Position,
    string? VenmoHandle
);

public record UpdatePushTokenRequest(
    string PushToken
);
