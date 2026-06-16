using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.Models;

public class Project
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string TargetStack { get; set; } = "CSharp_PostgreSQL";

    /// <summary>Architecture type: Monolith (default) or Microservices</summary>
    [Required]
    [MaxLength(20)]
    public string ArchitectureType { get; set; } = "Monolith";

    /// <summary>JSON-serialized AuthConfig; null means auth is disabled</summary>
    public string? AuthConfig { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<Entity> Entities { get; set; } = new List<Entity>();
}
