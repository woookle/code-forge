using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using CodeForgeAPI.Data;
using CodeForgeAPI.DTOs.Auth;
using CodeForgeAPI.Models;

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
                "Подтверждение регистрации", 
                $"<h1>Ваш код подтверждения: {code}</h1><p>Код действителен 15 минут.</p>"
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
            IsDarkMode = user.IsDarkMode
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
        
        // Generate token
        var token = GenerateJwtToken(user);
        
        return new AuthResponse
        {
            Id = user.Id,
            Token = token,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            IsDarkMode = user.IsDarkMode
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
                "Сброс пароля", 
                $"<h1>Код для сброса пароля: {code}</h1><p>Код действителен 15 минут.</p>"
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
}
