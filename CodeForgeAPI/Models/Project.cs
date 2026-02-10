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
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public User User { get; set; } = null!;
    public ICollection<Entity> Entities { get; set; } = new List<Entity>();
}
