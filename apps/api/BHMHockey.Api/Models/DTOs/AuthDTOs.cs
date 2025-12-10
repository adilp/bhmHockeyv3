namespace BHMHockey.Api.Models.DTOs;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    Dictionary<string, string>? Positions,
    string? VenmoHandle
);

public record LoginRequest(
    string Email,
    string Password
);

public record AuthResponse(
    string Token,
    string RefreshToken,
    UserDto User
);

public record RefreshTokenRequest(
    string RefreshToken
);
