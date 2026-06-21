using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.Models;

public class GenerationHistory
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Может быть null если проект был удалён</summary>
    public Guid? ProjectId { get; set; }

    [Required, MaxLength(200)]
    public string ProjectName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string TargetStack { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ArchitectureType { get; set; } = string.Empty;

    public int EntityCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
    public Project? Project { get; set; }
}
