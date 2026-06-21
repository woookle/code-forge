using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CodeForgeAPI.Data;
using CodeForgeAPI.Services;

namespace CodeForgeAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AchievementsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IAchievementService _achievements;

    public AchievementsController(ApplicationDbContext context, IAchievementService achievements)
    {
        _context = context;
        _achievements = achievements;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    /// <summary>Получить все достижения пользователя (определения + статус разблокировки)</summary>
    [HttpGet]
    public async Task<IActionResult> GetAchievements()
    {
        var userId = GetUserId();

        var unlockedMap = await _context.UserAchievements
            .Where(a => a.UserId == userId)
            .ToDictionaryAsync(a => a.AchievementId, a => a.UnlockedAt);

        var result = AchievementService.All.Select(def => new
        {
            def.Id,
            def.Icon,
            def.Title,
            def.Description,
            def.Color,
            Unlocked = unlockedMap.ContainsKey(def.Id),
            UnlockedAt = unlockedMap.TryGetValue(def.Id, out var dt) ? (DateTime?)dt : null,
        });

        return Ok(result);
    }

    /// <summary>Принудительно проверить и разблокировать достижения (вызывается при входе)</summary>
    [HttpPost("check")]
    public async Task<IActionResult> CheckAchievements()
    {
        var userId = GetUserId();
        var newOnes = await _achievements.CheckAndUnlockAsync(userId);
        return Ok(newOnes.Select(a => new { a.Id, a.Icon, a.Title, a.Description, a.Color }));
    }
}
