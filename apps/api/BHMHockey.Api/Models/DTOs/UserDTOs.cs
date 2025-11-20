namespace BHMHockey.Api.Models.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Role,
    DateTime CreatedAt
);

public record UpdateUserRequest(
    string? FirstName,
    string? LastName,
    string? PhoneNumber
);

public record UpdatePushTokenRequest(
    string PushToken
);
