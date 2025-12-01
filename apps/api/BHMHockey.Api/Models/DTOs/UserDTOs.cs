namespace BHMHockey.Api.Models.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    Dictionary<string, string>? Positions,  // {"goalie": "Gold", "skater": "Silver"}
    string? VenmoHandle,
    string Role,
    DateTime CreatedAt
);

public record UpdateUserProfileRequest(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    Dictionary<string, string>? Positions,  // {"goalie": "Gold", "skater": "Silver"}
    string? VenmoHandle
);

public record UpdatePushTokenRequest(
    string PushToken
);
