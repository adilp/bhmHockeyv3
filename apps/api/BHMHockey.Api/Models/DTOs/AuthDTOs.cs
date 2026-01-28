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

public record AdminPasswordResetResponse(
    Guid UserId,
    string Email,
    string TemporaryPassword,
    string Message
);

public record AdminUserSearchResult(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive
);

public record AdminUpdateRoleRequest(
    string Role
);

public record AdminUpdateRoleResponse(
    Guid UserId,
    string Email,
    string PreviousRole,
    string NewRole,
    string Message
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record ForgotPasswordRequest(
    string Email
);

public record ForgotPasswordResponse(
    string Message
);

public record AdminStatsResponse(
    int TotalUsers,
    int ActiveUsers
);
