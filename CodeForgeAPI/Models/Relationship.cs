using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.Models;

public class Relationship
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid SourceEntityId { get; set; }
    
    [Required]
    public Guid TargetEntityId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string RelationshipType { get; set; } = "OneToMany";
    
    [Required]
    [MaxLength(100)]
    public string SourceFieldName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string TargetFieldName { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Entity SourceEntity { get; set; } = null!;
    public Entity TargetEntity { get; set; } = null!;
}
