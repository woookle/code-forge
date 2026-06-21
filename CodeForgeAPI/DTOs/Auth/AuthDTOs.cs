using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.DTOs.Auth;

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Code { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "User";
    public bool IsDarkMode { get; set; }
    public bool TwoFactorEnabled { get; set; }
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class Enable2FAResponse
{
    public string QrCodeBase64 { get; set; } = string.Empty;
    public string ManualEntryKey { get; set; } = string.Empty;
}

public class Verify2FARequest
{
    public string Code { get; set; } = string.Empty;
}

public class LoginWith2FARequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TotpCode { get; set; } = string.Empty;
}

public class LoginCheckResponse
{
    public bool RequiresTwoFactor { get; set; }
}
