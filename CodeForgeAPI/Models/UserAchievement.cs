using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.Models;

public class UserAchievement
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    [Required, MaxLength(100)]
    public string AchievementId { get; set; } = string.Empty;

    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
