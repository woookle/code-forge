using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CodeForgeAPI.Data;
using CodeForgeAPI.DTOs.Auth;
using CodeForgeAPI.Services;

namespace CodeForgeAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    
    public AuthController(IAuthService authService, ApplicationDbContext context, IWebHostEnvironment env)
    {
        _authService = authService;
        _context = context;
        _env = env;
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
            isDarkMode = result.IsDarkMode
        });
    }
    
    [HttpPost("login")]
    public async Task<ActionResult<object>> Login([FromBody] LoginRequest request)
    {
        // ModelState.IsValid check is implicitly handled by [ApiController] for simple cases.
        
        var result = await _authService.LoginAsync(request);
        
        if (result == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
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
            isDarkMode = result.IsDarkMode
        });
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
        await _context.SaveChangesAsync();

        return Ok(new { avatarUrl = user.AvatarUrl });
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
            isDarkMode = user.IsDarkMode
        });
    }
}

public class SendCodeRequest
{
    public string Email { get; set; } = string.Empty;
}
