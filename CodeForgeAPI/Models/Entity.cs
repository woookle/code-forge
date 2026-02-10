using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.Models;

public class Entity
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid ProjectId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public int DisplayOrder { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Project Project { get; set; } = null!;
    public ICollection<Field> Fields { get; set; } = new List<Field>();
    public ICollection<Relationship> SourceRelationships { get; set; } = new List<Relationship>();
    public ICollection<Relationship> TargetRelationships { get; set; } = new List<Relationship>();
}
