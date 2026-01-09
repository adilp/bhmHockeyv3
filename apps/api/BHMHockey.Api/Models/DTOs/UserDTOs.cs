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
    DateTime CreatedAt,
    // Badge fields (for roster display)
    List<UserBadgeDto>? Badges = null,      // Top 3 badges by displayOrder
    int TotalBadgeCount = 0                  // Total badges user has earned
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
