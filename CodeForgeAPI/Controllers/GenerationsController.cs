using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CodeForgeAPI.Data;
using CodeForgeAPI.Models;
using CodeForgeAPI.Services;

namespace CodeForgeAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class GenerationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAchievementService _achievements;

    public GenerationsController(ApplicationDbContext context, IAchievementService achievements)
    {
        _context = context;
        _achievements = achievements;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    /// <summary>Получить историю генераций текущего пользователя</summary>
    [HttpGet]
    public async Task<IActionResult> GetHistory([FromQuery] Guid? projectId, [FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        var query = _context.GenerationHistories
            .Where(g => g.UserId == userId);

        if (projectId.HasValue)
            query = query.Where(g => g.ProjectId == projectId.Value);

        var records = await query
            .OrderByDescending(g => g.CreatedAt)
            .Take(Math.Min(limit, 200))
            .Select(g => new
            {
                g.Id,
                g.ProjectId,
                g.ProjectName,
                g.TargetStack,
                g.ArchitectureType,
                g.EntityCount,
                g.CreatedAt,
            })
            .ToListAsync();

        return Ok(records);
    }

    /// <summary>Сохранить запись о генерации и проверить достижения</summary>
    [HttpPost]
    public async Task<IActionResult> RecordGeneration([FromBody] RecordGenerationRequest request)
    {
        var userId = GetUserId();

        var record = new GenerationHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProjectId = request.ProjectId,
            ProjectName = request.ProjectName,
            TargetStack = request.TargetStack,
            ArchitectureType = request.ArchitectureType,
            EntityCount = request.EntityCount,
            CreatedAt = DateTime.UtcNow,
        };

        _context.GenerationHistories.Add(record);

        // Лог активности
        _context.AccountActivities.Add(new AccountActivity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = "generation",
            Description = $"Сгенерирован проект «{request.ProjectName}»",
            Meta = $"{request.TargetStack}/{request.ArchitectureType}",
            CreatedAt = DateTime.UtcNow,
        });

        await _context.SaveChangesAsync();

        // Проверяем новые достижения
        var newAchievements = await _achievements.CheckAndUnlockAsync(userId);

        return Ok(new
        {
            id = record.Id,
            newAchievements = newAchievements.Select(a => new
            {
                a.Id,
                a.Icon,
                a.Title,
                a.Description,
                a.Color,
            }),
        });
    }

    /// <summary>Очистить всю историю генераций текущего пользователя</summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteHistory()
    {
        var userId = GetUserId();

        var records = await _context.GenerationHistories
            .Where(g => g.UserId == userId)
            .ToListAsync();

        if (records.Count == 0)
            return NoContent();

        _context.GenerationHistories.RemoveRange(records);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    public class RecordGenerationRequest
    {
        public Guid? ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string TargetStack { get; set; } = string.Empty;
        public string ArchitectureType { get; set; } = string.Empty;
        public int EntityCount { get; set; }
    }
}
