using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(string refreshToken);
    Task<bool> LogoutAsync(Guid userId);
    Task<AdminPasswordResetResponse> AdminResetPasswordAsync(Guid userId);
    Task<List<AdminUserSearchResult>> SearchUsersAsync(string query);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<AdminStatsResponse> GetAdminStatsAsync();
}
