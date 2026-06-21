using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using CodeForgeAPI.Data;
using CodeForgeAPI.DTOs.Auth;
using CodeForgeAPI.Models;
using OtpNet;
using QRCoder;

namespace CodeForgeAPI.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly JwtSettings _jwtSettings;
    private readonly IEmailService _emailService;
    
    public AuthService(ApplicationDbContext context, IOptions<JwtSettings> jwtSettings, IEmailService emailService)
    {
        _context = context;
        _jwtSettings = jwtSettings.Value;
        _emailService = emailService;
    }
    
    public async Task<bool> SendVerificationCodeAsync(string email)
    {
        // Check if user already exists
        if (await _context.Users.AnyAsync(u => u.Email == email))
        {
            Console.WriteLine($"[SendVerificationCode] User already exists: {email}");
            return false; // User already exists
        }

        // Generate code
        var code = new Random().Next(100000, 999999).ToString();

        // Save or update token
        var existingToken = await _context.VerificationTokens.FirstOrDefaultAsync(t => t.Email == email);
        if (existingToken != null)
        {
            existingToken.Code = code;
            existingToken.ExpiresAt = DateTime.UtcNow.AddMinutes(15);
        }
        else
        {
            _context.VerificationTokens.Add(new VerificationToken
            {
                Id = Guid.NewGuid(),
                Email = email,
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            });
        }
        
        await _context.SaveChangesAsync();

        // Send email
        try 
        {
            Console.WriteLine($"[SendVerificationCode] Sending email to {email}...");
            await _emailService.SendEmailAsync(
                email,
                EmailTemplates.VerificationSubject,
                EmailTemplates.VerificationCode(code)
            );
            Console.WriteLine($"[SendVerificationCode] Email sent successfully.");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SendVerificationCode] Email sending failed: {ex.Message}");
            Console.WriteLine(ex.ToString());
            return false;
        }
    }

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest request)
    {
        // Verify code
        var token = await _context.VerificationTokens
            .FirstOrDefaultAsync(t => t.Email == request.Email && t.Code == request.Code);

        if (token == null || token.ExpiresAt < DateTime.UtcNow)
        {
            return null; // Invalid or expired code
        }

        // Check if user already exists (double check)
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return null;
        }
        
        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        
        // Create user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _context.Users.Add(user);
        
        // Remove used token
        _context.VerificationTokens.Remove(token);
        
        await _context.SaveChangesAsync();

        try
        {
            await _emailService.SendEmailAsync(
                user.Email,
                EmailTemplates.WelcomeSubject,
                EmailTemplates.Welcome(user.FirstName ?? "пользователь")
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Register] Welcome email failed: {ex.Message}");
        }

        // Generate token
        var jwtToken = GenerateJwtToken(user);
        
        return new AuthResponse
        {
            Id = user.Id,
            Token = jwtToken,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            IsDarkMode = user.IsDarkMode,
            TwoFactorEnabled = user.TwoFactorEnabled
        };
    }

    // VerifyEmailAsync removed as it is now part of RegisterAsync flow with token code

    
    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        // Find user
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            return null;
        }
        
        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return null;
        }

        // If 2FA is enabled, return a special response indicating TOTP is required
        if (user.TwoFactorEnabled)
        {
            return new AuthResponse
            {
                Id = user.Id,
                Token = string.Empty, // No token yet - requires TOTP verification
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role,
                IsDarkMode = user.IsDarkMode,
                TwoFactorEnabled = true
            };
        }
        
        // Generate token
        var jwtToken = GenerateJwtToken(user);
        
        return new AuthResponse
        {
            Id = user.Id,
            Token = jwtToken,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            IsDarkMode = user.IsDarkMode,
            TwoFactorEnabled = user.TwoFactorEnabled
        };
    }

    public async Task<AuthResponse?> LoginWithTotpAsync(LoginWith2FARequest request)
    {
        // Find user
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null) return null;

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return null;

        // Check 2FA is enabled and secret exists
        if (!user.TwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret))
            return null;

        // Verify TOTP code (trim whitespace just in case)
        var trimmedCode = request.TotpCode?.Trim() ?? string.Empty;
        var secretBytes = Base32Encoding.ToBytes(user.TwoFactorSecret);
        var totp = new Totp(secretBytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
        // Window of 5 allows ±150 seconds clock skew
        bool isValid = totp.VerifyTotp(trimmedCode, out long matchedStep, new VerificationWindow(5, 5));
        Console.WriteLine($"[LoginWithTotp] Code={trimmedCode}, Valid={isValid}, MatchedStep={matchedStep}");
        if (!isValid) return null;

        // Generate JWT token
        var jwtToken = GenerateJwtToken(user);

        return new AuthResponse
        {
            Id = user.Id,
            Token = jwtToken,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            IsDarkMode = user.IsDarkMode,
            TwoFactorEnabled = user.TwoFactorEnabled
        };
    }
    
    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users.FindAsync(userId);
    }
    
    public async Task<bool> SendPasswordResetCodeAsync(string email)
    {
        // Check if user exists
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return false; // User not found
        }

        // Generate code
        var code = new Random().Next(100000, 999999).ToString();

        // Save or update token
        var existingToken = await _context.VerificationTokens.FirstOrDefaultAsync(t => t.Email == email);
        if (existingToken != null)
        {
            existingToken.Code = code;
            existingToken.ExpiresAt = DateTime.UtcNow.AddMinutes(15);
        }
        else
        {
            _context.VerificationTokens.Add(new VerificationToken
            {
                Id = Guid.NewGuid(),
                Email = email,
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15)
            });
        }
        
        await _context.SaveChangesAsync();

        // Send email
        try 
        {
            await _emailService.SendEmailAsync(
                email,
                EmailTemplates.PasswordResetSubject,
                EmailTemplates.PasswordResetCode(code)
            );
            return true;
        }
        catch 
        {
            return false;
        }
    }

    public async Task<bool> ResetPasswordAsync(string email, string code, string newPassword)
    {
        // Verify code
        var token = await _context.VerificationTokens
            .FirstOrDefaultAsync(t => t.Email == email && t.Code == code);

        if (token == null || token.ExpiresAt < DateTime.UtcNow)
        {
            return false; // Invalid or expired code
        }

        // Get user
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return false;
        }

        // Hash new password
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        
        // Remove used token
        _context.VerificationTokens.Remove(token);
        
        await _context.SaveChangesAsync();
        
        return true;
    }

    public async Task<Enable2FAResponse?> GenerateTwoFactorSetupAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;

        // Generate a new Base32 secret key (20 bytes = 160 bits)
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretBytes);

        // Store secret temporarily (will be confirmed when user verifies the code)
        user.TwoFactorSecret = base32Secret;
        await _context.SaveChangesAsync();

        // Build label for Google Authenticator: "CodeForge: {name or email}"
        var displayName = !string.IsNullOrEmpty(user.FirstName)
            ? $"{user.FirstName} {user.LastName}".Trim()
            : user.Email;
        var label = Uri.EscapeDataString($"CodeForge: {displayName}");
        var issuer = Uri.EscapeDataString("CodeForge");

        // otpauth URI format for Google Authenticator
        var otpauthUri = $"otpauth://totp/{label}?secret={base32Secret}&issuer={issuer}&digits=6&period=30";

        // Generate QR code as Base64 PNG
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(otpauthUri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        var qrCodeBytes = qrCode.GetGraphic(10);
        var qrCodeBase64 = $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";

        return new Enable2FAResponse
        {
            QrCodeBase64 = qrCodeBase64,
            ManualEntryKey = base32Secret
        };
    }

    public async Task<bool> EnableTwoFactorAsync(Guid userId, string code)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || string.IsNullOrEmpty(user.TwoFactorSecret)) return false;

        // Verify TOTP code (trim whitespace just in case)
        var trimmedVerifyCode = code?.Trim() ?? string.Empty;
        var secretBytesVerify = Base32Encoding.ToBytes(user.TwoFactorSecret);
        var totpVerify = new Totp(secretBytesVerify, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
        bool isValidVerify = totpVerify.VerifyTotp(trimmedVerifyCode, out long verifyMatchedStep, new VerificationWindow(5, 5));
        Console.WriteLine($"[EnableTwoFactor] Code={trimmedVerifyCode}, Valid={isValidVerify}");

        if (!isValidVerify) return false;

        // Enable 2FA - secret is already stored
        user.TwoFactorEnabled = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DisableTwoFactorAsync(Guid userId, string code)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.TwoFactorEnabled || string.IsNullOrEmpty(user.TwoFactorSecret)) return false;

        // Verify TOTP code
        var trimmedDisableCode = code?.Trim() ?? string.Empty;
        var secretBytesDisable = Base32Encoding.ToBytes(user.TwoFactorSecret);
        var totpDisable = new Totp(secretBytesDisable, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
        bool isValidDisable = totpDisable.VerifyTotp(trimmedDisableCode, out _, new VerificationWindow(5, 5));

        if (!isValidDisable) return false;

        // Disable 2FA and remove secret
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        await _context.SaveChangesAsync();
        return true;
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            signingCredentials: creds
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return false;

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }
}
