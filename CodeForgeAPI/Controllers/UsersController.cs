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
                CreatedAt = u.CreatedAt,
                Projects = u.Projects.Select(p => new Project 
                { 
                    Id = p.Id, 
                    Name = p.Name, 
                    Description = p.Description, 
                    TargetStack = p.TargetStack,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    Entities = p.Entities.Select(e => new Entity 
                    { 
                        Id = e.Id, 
                        Name = e.Name, 
                        ProjectId = p.Id,
                        DisplayOrder = e.DisplayOrder,
                        CreatedAt = e.CreatedAt
                    }).ToList() 
                }).ToList()
            })
            .ToListAsync();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Prevent deleting the last admin or yourself if needed, but for now simple delete
        if (user.Email == "admin@admin.com")
        {
             return BadRequest("Cannot delete the main admin user.");
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return NoContent();
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
