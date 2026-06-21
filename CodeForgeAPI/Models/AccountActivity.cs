using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.Models;

public class AccountActivity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Тип события: project_created, generation, entity_created, login, avatar_changed, 2fa_enabled, password_changed</summary>
    [Required, MaxLength(100)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Meta { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
