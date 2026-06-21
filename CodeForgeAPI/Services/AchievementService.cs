using Microsoft.EntityFrameworkCore;
using CodeForgeAPI.Data;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.Services;

/// <summary>Описание одного достижения</summary>
public record AchievementDefinition(
    string Id,
    string Icon,
    string Title,
    string Description,
    string Color
);

public interface IAchievementService
{
    /// <summary>Проверяет все достижения пользователя и возвращает список только что разблокированных</summary>
    Task<List<AchievementDefinition>> CheckAndUnlockAsync(Guid userId);
}

public class AchievementService : IAchievementService
{
    private readonly ApplicationDbContext _context;

    // Все возможные достижения системы
    public static readonly List<AchievementDefinition> All = new()
    {
        new("first_project",   "🚀", "Первый старт",      "Создай свой первый проект",              "#10b981"),
        new("first_gen",       "📦", "Первая генерация",   "Сгенерируй проект хотя бы раз",          "#6366f1"),
        new("ten_gens",        "⚡", "Генератор",          "10 успешных генераций",                  "#f59e0b"),
        new("thirty_gens",     "🔥", "Кузница кода",       "30 успешных генераций",                  "#ef4444"),
        new("five_projects",   "📁", "Архивариус",         "Создай 5 проектов",                      "#3b82f6"),
        new("ten_projects",    "🗂",  "Коллекционер",       "Создай 10 проектов",                     "#8b5cf6"),
        new("entity_builder",  "🧩", "Архитектор",         "Добавь 20 сущностей суммарно",           "#8b5cf6"),
        new("field_master",    "🔧", "Мастер схем",        "100 полей суммарно",                     "#ec4899"),
        new("avatar",          "🖼",  "С лицом",            "Загрузи аватар",                         "#14b8a6"),
        new("security",        "🔐", "Параноик",           "Включи двухфакторную аутентификацию",    "#ef4444"),
        new("microservices",   "🔀", "Микросервисник",     "Сгенерируй микросервисный проект",       "#6366f1"),
        new("all_stacks",      "🌐", "Full-stack мастер",  "Используй оба стека (C# и Node.js)",     "#10b981"),
    };

    public AchievementService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AchievementDefinition>> CheckAndUnlockAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.Projects)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return new();

        // Уже разблокированные
        var existing = await _context.UserAchievements
            .Where(a => a.UserId == userId)
            .Select(a => a.AchievementId)
            .ToHashSetAsync();

        // Считаем статистику
        var projectCount = await _context.Projects.CountAsync(p => p.UserId == userId);
        var entityCount = await _context.Entities
            .CountAsync(e => e.Project.UserId == userId);
        var fieldCount = await _context.Fields
            .CountAsync(f => f.Entity.Project.UserId == userId);
        var genCount = await _context.GenerationHistories.CountAsync(g => g.UserId == userId);
        var hasAvatar = !string.IsNullOrEmpty(user.AvatarUrl);
        var has2FA = user.TwoFactorEnabled;
        var hasMicroservicesGen = await _context.GenerationHistories
            .AnyAsync(g => g.UserId == userId && g.ArchitectureType == "Microservices");
        var stacksUsed = await _context.GenerationHistories
            .Where(g => g.UserId == userId)
            .Select(g => g.TargetStack)
            .Distinct()
            .CountAsync();

        // Условия для каждого достижения
        var conditions = new Dictionary<string, bool>
        {
            ["first_project"]   = projectCount >= 1,
            ["first_gen"]       = genCount >= 1,
            ["ten_gens"]        = genCount >= 10,
            ["thirty_gens"]     = genCount >= 30,
            ["five_projects"]   = projectCount >= 5,
            ["ten_projects"]    = projectCount >= 10,
            ["entity_builder"]  = entityCount >= 20,
            ["field_master"]    = fieldCount >= 100,
            ["avatar"]          = hasAvatar,
            ["security"]        = has2FA,
            ["microservices"]   = hasMicroservicesGen,
            ["all_stacks"]      = stacksUsed >= 2,
        };

        var newlyUnlocked = new List<AchievementDefinition>();

        foreach (var (achievementId, conditionMet) in conditions)
        {
            if (!conditionMet || existing.Contains(achievementId)) continue;

            _context.UserAchievements.Add(new UserAchievement
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AchievementId = achievementId,
                UnlockedAt = DateTime.UtcNow,
            });

            var def = All.FirstOrDefault(a => a.Id == achievementId);
            if (def != null) newlyUnlocked.Add(def);
        }

        if (newlyUnlocked.Count > 0)
            await _context.SaveChangesAsync();

        return newlyUnlocked;
    }
}
