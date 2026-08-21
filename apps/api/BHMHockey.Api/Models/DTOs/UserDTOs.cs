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
    int TotalBadgeCount = 0,                 // Total badges user has earned
    bool IsGhostPlayer = false,              // True if this is a placeholder account
    string? DLeagueTeam = null               // Only set for D-League players
);

public record UpdateUserProfileRequest(
    string? FirstName,
    string? LastName,
    string? PhoneNumber,
    Dictionary<string, string>? Positions,  // {"goalie": "Gold", "skater": "Silver"}
    string? VenmoHandle,
    string? DLeagueTeam = null              // Ignored unless a position is D-League
);

public record UpdatePushTokenRequest(
    string PushToken
);
