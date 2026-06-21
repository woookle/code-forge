using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeForgeAPI.Data;
using CodeForgeAPI.Models;
using CodeForgeAPI.DTOs.Users;
using System.Security.Claims;

namespace CodeForgeAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    private readonly IWebHostEnvironment _env;

    public UsersController(ApplicationDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpPost("{id}/avatar")]
    public async Task<IActionResult> UploadUserAvatar(Guid id, IFormFile file)
    {
        if (!IsAuthorizedToModifyUser(id))
        {
            return Forbid();
        }

        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded");

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(_env.WebRootPath))
        {
            _env.WebRootPath = Path.Combine(_env.ContentRootPath, "wwwroot");
        }
        
        var uploadsFolder = Path.Combine(_env.WebRootPath, "avatars");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var fileExtension = Path.GetExtension(file.FileName);
        var fileName = $"{user.Id}_{Guid.NewGuid()}{fileExtension}";
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
        user.UpdatedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();

        return Ok(new { avatarUrl = user.AvatarUrl });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        if (!IsAuthorizedToModifyUser(id))
        {
            return Forbid();
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        user.FirstName = request.FirstName ?? user.FirstName;
        user.LastName = request.LastName ?? user.LastName;
        user.AvatarUrl = request.AvatarUrl ?? user.AvatarUrl;
        if (request.IsDarkMode.HasValue)
        {
            user.IsDarkMode = request.IsDarkMode.Value;
        }
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        return await _context.Users
            .Select(u => new User
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Role = u.Role,
                AvatarUrl = u.AvatarUrl,
                IsDarkMode = u.IsDarkMode,
                TwoFactorEnabled = u.TwoFactorEnabled,
                CreatedAt = u.CreatedAt,
                Projects = u.Projects.Select(p => new Project
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    TargetStack = p.TargetStack,
                    ArchitectureType = p.ArchitectureType,
                    AuthConfig = p.AuthConfig,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    Entities = p.Entities.Select(e => new Entity
                    {
                        Id = e.Id,
                        Name = e.Name,
                        ProjectId = p.Id,
                        DisplayOrder = e.DisplayOrder,
                        ServiceName = e.ServiceName,
                        CreatedAt = e.CreatedAt,
                        Fields = e.Fields.Select(f => new Field
                        {
                            Id = f.Id,
                            Name = f.Name,
                            DataType = f.DataType,
                            IsRequired = f.IsRequired,
                            IsUnique = f.IsUnique,
                            IsPrimaryKey = f.IsPrimaryKey,
                            DisplayOrder = f.DisplayOrder,
                            EntityId = e.Id
                        }).ToList(),
                        SourceRelationships = e.SourceRelationships.Select(r => new Relationship
                        {
                            Id = r.Id,
                            SourceEntityId = r.SourceEntityId,
                            TargetEntityId = r.TargetEntityId,
                            RelationshipType = r.RelationshipType,
                            SourceFieldName = r.SourceFieldName,
                            TargetFieldName = r.TargetFieldName
                        }).ToList(),
                        TargetRelationships = e.TargetRelationships.Select(r => new Relationship
                        {
                            Id = r.Id,
                            SourceEntityId = r.SourceEntityId,
                            TargetEntityId = r.TargetEntityId,
                            RelationshipType = r.RelationshipType,
                            SourceFieldName = r.SourceFieldName,
                            TargetFieldName = r.TargetFieldName
                        }).ToList()
                    }).ToList()
                }).ToList()
            })
            .ToListAsync();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId == id)
            return BadRequest(new { message = "Нельзя удалить собственный аккаунт" });

        var user = await _context.Users
            .Include(u => u.Projects)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound(new { message = "Пользователь не найден" });

        if (IsProtectedAdmin(user.Email))
            return BadRequest(new { message = "Нельзя удалить главного администратора" });

        var projectCount = user.Projects.Count;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Токены верификации привязаны к email, а не к UserId
            var tokens = await _context.VerificationTokens
                .Where(t => t.Email == user.Email)
                .ToListAsync();
            if (tokens.Count > 0)
                _context.VerificationTokens.RemoveRange(tokens);

            DeleteAvatarFile(user.AvatarUrl);

            // Каскад через EF/PostgreSQL: Projects → Entities → Fields/Relationships,
            // а также GenerationHistories, UserAchievements, AccountActivities
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Пользователь и все связанные данные удалены",
                deletedProjects = projectCount,
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"[DeleteUser] Error: {ex.Message}");
            return StatusCode(500, new { message = "Ошибка при удалении пользователя" });
        }
    }

    private static bool IsProtectedAdmin(string email) =>
        email.Equals("admin@codeforge.ru", StringComparison.OrdinalIgnoreCase) ||
        email.Equals("admin@admin.com", StringComparison.OrdinalIgnoreCase);

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private void DeleteAvatarFile(string? avatarUrl)
    {
        if (string.IsNullOrEmpty(avatarUrl)) return;

        var webRoot = string.IsNullOrEmpty(_env.WebRootPath)
            ? Path.Combine(_env.ContentRootPath, "wwwroot")
            : _env.WebRootPath;

        var avatarPath = Path.Combine(webRoot, avatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(avatarPath))
        {
            try { System.IO.File.Delete(avatarPath); } catch { }
        }
    }

    private bool IsAuthorizedToModifyUser(Guid userId)
    {
        // Admin can modify anyone
        if (User.IsInRole("Admin")) 
            return true;

        // User can modify themselves
        var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (currentUserIdClaim != null && Guid.TryParse(currentUserIdClaim.Value, out var currentUserId))
        {
            return currentUserId == userId;
        }

        return false;
    }
}
