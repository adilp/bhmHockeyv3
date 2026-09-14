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

/// <summary>
/// Start a password reset. The response never says whether the email has an
/// account, so this can't be used to check who is registered.
/// </summary>
public record PasswordResetRequest(string Email);

/// <summary>
/// Finish a password reset with EITHER the token from the emailed link, OR the
/// email address plus the 6-digit code from the same email.
/// </summary>
public record ConfirmPasswordResetRequest(
    string NewPassword,
    string? Token = null,
    string? Email = null,
    string? Code = null
);

public record PasswordResetMessageResponse(string Message);
