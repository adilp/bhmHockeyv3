using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface IPasswordResetService
{
    /// <summary>
    /// Email a reset link and code to the account with this address, if there is
    /// one. Returns the same way whether or not an account exists, is rate
    /// limited, or the email fails - the caller always answers identically.
    /// </summary>
    Task RequestResetAsync(string email);

    /// <summary>
    /// Set a new password using the link token, or the email plus code.
    /// </summary>
    /// <exception cref="InvalidOperationException">The token or code is invalid, used, expired, or the password is too short</exception>
    Task ConfirmResetAsync(ConfirmPasswordResetRequest request);
}
