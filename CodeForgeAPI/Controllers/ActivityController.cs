using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using CodeForgeAPI.Data;

namespace CodeForgeAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ActivityController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ActivityController(ApplicationDbContext context)
    {
        _context = context;
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    /// <summary>Получить историю активности аккаунта</summary>
    [HttpGet]
    public async Task<IActionResult> GetActivity([FromQuery] int limit = 30)
    {
        var userId = GetUserId();

        var activities = await _context.AccountActivities
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Min(limit, 100))
            .Select(a => new
            {
                a.Id,
                a.EventType,
                a.Description,
                a.Meta,
                a.CreatedAt,
            })
            .ToListAsync();

        return Ok(activities);
    }
}
