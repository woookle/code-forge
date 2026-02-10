using CodeForgeAPI.DTOs.Auth;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.Services;

public interface IAuthService
{
    Task<AuthResponse?> RegisterAsync(RegisterRequest request);
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<bool> SendVerificationCodeAsync(string email);
    Task<bool> SendPasswordResetCodeAsync(string email);
    Task<bool> ResetPasswordAsync(string email, string code, string newPassword);
}
