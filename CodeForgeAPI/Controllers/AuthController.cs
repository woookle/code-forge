using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CodeForgeAPI.Data;
using CodeForgeAPI.DTOs.Auth;
using CodeForgeAPI.Models;
using CodeForgeAPI.Services;

namespace CodeForgeAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IAchievementService _achievements;

    public AuthController(IAuthService authService, ApplicationDbContext context, IWebHostEnvironment env, IAchievementService achievements)
    {
        _authService = authService;
        _context = context;
        _env = env;
        _achievements = achievements;
    }

    private void LogActivity(Guid userId, string eventType, string description, string? meta = null)
    {
        _context.AccountActivities.Add(new AccountActivity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = eventType,
            Description = description,
            Meta = meta,
            CreatedAt = DateTime.UtcNow,
        });
    }
    
    [HttpPost("send-code")]
    public async Task<IActionResult> SendCode([FromBody] SendCodeRequest request)
    {
        var result = await _authService.SendVerificationCodeAsync(request.Email);
        if (result)
        {
            return Ok(new { message = "Verification code sent" });
        }
        return BadRequest(new { message = "Failed to send code or user already exists" });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var result = await _authService.SendPasswordResetCodeAsync(request.Email);
        if (result)
        {
             return Ok(new { message = "Password reset code sent" });
        }
        // For security, checking if user exists shouldn't always return distinct error, 
        // but for now strictly following logic:
        return BadRequest(new { message = "Failed to send code or user not found" });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var result = await _authService.ResetPasswordAsync(request.Email, request.Code, request.NewPassword);
        if (result)
        {
            return Ok(new { message = "Password reset successfully" });
        }
        return BadRequest(new { message = "Invalid code or failed to reset password" });
    }

    // Temporary endpoint for development cleanup
    [HttpDelete("test-cleanup/{email}")]
    public async Task<IActionResult> CleanupUser(string email)
    {
        var user = _context.Users.FirstOrDefault(u => u.Email == email);
        if (user != null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"User {email} deleted" });
        }
        return NotFound(new { message = "User not found" });
    }

    [HttpPost("register")]
    public async Task<ActionResult<object>> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        
        if (result == null)
        {
            return BadRequest(new { message = "Registration failed. Invalid code or user already exists." });
        }
        
        // Set JWT token in httpOnly cookie
        SetTokenCookie(result.Token);
        
        // Don't send token in response body
        return Ok(new 
        { 
            id = result.Id,
            email = result.Email,
            firstName = result.FirstName,
            lastName = result.LastName,
            avatarUrl = result.AvatarUrl,
            role = result.Role,
            isDarkMode = result.IsDarkMode,
            twoFactorEnabled = result.TwoFactorEnabled
        });
    }
    
    [HttpPost("login")]
    public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        
        if (result == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        // If 2FA is required, return a special flag without setting the cookie
        if (result.TwoFactorEnabled && string.IsNullOrEmpty(result.Token))
        {
            return Ok(new
            {
                requiresTwoFactor = true,
                email = result.Email
            });
        }
        
        // Set JWT token in httpOnly cookie
        SetTokenCookie(result.Token);
        
        // Don't send token in response body
        return Ok(new 
        { 
            id = result.Id,
            email = result.Email,
            firstName = result.FirstName,
            lastName = result.LastName,
            avatarUrl = result.AvatarUrl,
            role = result.Role,
            isDarkMode = result.IsDarkMode,
            twoFactorEnabled = result.TwoFactorEnabled
        });
    }

    [HttpPost("login-2fa")]
    public async Task<ActionResult<object>> LoginWith2FA([FromBody] LoginWith2FARequest request)
    {
        Console.WriteLine($"[LoginWith2FA] Email={request.Email}, TotpCode={request.TotpCode}, PasswordLen={request.Password?.Length}");
        var result = await _authService.LoginWithTotpAsync(request);

        if (result == null)
        {
            Console.WriteLine($"[LoginWith2FA] Failed for {request.Email}");
            return Unauthorized(new { message = "Неверный код. Проверьте код в Google Authenticator и попробуйте снова." });
        }

        // Set JWT token in httpOnly cookie
        SetTokenCookie(result.Token);

        return Ok(new
        {
            id = result.Id,
            email = result.Email,
            firstName = result.FirstName,
            lastName = result.LastName,
            avatarUrl = result.AvatarUrl,
            role = result.Role,
            isDarkMode = result.IsDarkMode,
            twoFactorEnabled = result.TwoFactorEnabled
        });
    }

    [Authorize]
    [HttpPost("2fa/setup")]
    public async Task<IActionResult> SetupTwoFactor()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _authService.GenerateTwoFactorSetupAsync(Guid.Parse(userId));
        if (result == null) return NotFound(new { message = "User not found" });

        return Ok(new
        {
            qrCodeBase64 = result.QrCodeBase64,
            manualEntryKey = result.ManualEntryKey
        });
    }

    [Authorize]
    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor([FromBody] Verify2FARequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var uid = Guid.Parse(userId);
        var result = await _authService.EnableTwoFactorAsync(uid, request.Code);
        if (!result) return BadRequest(new { message = "Invalid TOTP code. Please check the code in your authenticator app." });

        LogActivity(uid, "2fa_enabled", "Двухфакторная аутентификация включена");
        await _context.SaveChangesAsync();

        var newAchievements = await _achievements.CheckAndUnlockAsync(uid);

        return Ok(new
        {
            message = "Two-factor authentication enabled successfully",
            newAchievements = newAchievements.Select(a => new { a.Id, a.Icon, a.Title, a.Description, a.Color }),
        });
    }

    [Authorize]
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] Verify2FARequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _authService.DisableTwoFactorAsync(Guid.Parse(userId), request.Code);
        if (!result) return BadRequest(new { message = "Invalid TOTP code. Please check the code in your authenticator app." });

        return Ok(new { message = "Two-factor authentication disabled successfully" });
    }

    [HttpPost("avatar")]
    [Authorize]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _context.Users.FindAsync(Guid.Parse(userId));
        if (user == null) return NotFound("User not found");

        if (string.IsNullOrEmpty(_env.WebRootPath))
        {
            _env.WebRootPath = Path.Combine(_env.ContentRootPath, "wwwroot");
        }
        var uploadsFolder = Path.Combine(_env.WebRootPath, "avatars");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{userId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Delete old avatar if exists
        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            var oldAvatarPath = Path.Combine(_env.WebRootPath, user.AvatarUrl.TrimStart('/'));
            if (System.IO.File.Exists(oldAvatarPath))
            {
                try { System.IO.File.Delete(oldAvatarPath); } catch {}
            }
        }

        user.AvatarUrl = $"/avatars/{fileName}";
        LogActivity(user.Id, "avatar_changed", "Аватар профиля обновлён");
        await _context.SaveChangesAsync();

        // Проверяем достижение за аватар
        var newAchievements = await _achievements.CheckAndUnlockAsync(user.Id);

        return Ok(new
        {
            avatarUrl = user.AvatarUrl,
            newAchievements = newAchievements.Select(a => new { a.Id, a.Icon, a.Title, a.Description, a.Color }),
        });
    }
    
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var success = await _authService.ChangePasswordAsync(userId, request);
        if (!success)
            return BadRequest(new { message = "Неверный текущий пароль" });

        LogActivity(userId, "password_changed", "Пароль аккаунта изменён");
        await _context.SaveChangesAsync();

        return Ok(new { message = "Пароль успешно изменён" });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        // Clear the cookie
        Response.Cookies.Delete("jwt");
        return Ok(new { message = "Logged out successfully" });
    }
    
    private void SetTokenCookie(string token)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // Only send over HTTPS
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(1)
        };
        
        Response.Cookies.Append("jwt", token, cookieOptions);
    }
    
    [Authorize]
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = _context.Users.Find(Guid.Parse(userId));
        if (user == null) return NotFound("User not found");

        return Ok(new
        {
            id = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            avatarUrl = user.AvatarUrl,
            role = user.Role,
            isDarkMode = user.IsDarkMode,
            twoFactorEnabled = user.TwoFactorEnabled
        });
    }
}

public class SendCodeRequest
{
    public string Email { get; set; } = string.Empty;
}
