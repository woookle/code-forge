using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.Models;

public class Field
{
    public Guid Id { get; set; }
    
    [Required]
    public Guid EntityId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string DataType { get; set; } = "String";
    
    public bool IsRequired { get; set; }
    public bool IsUnique { get; set; }
    public bool IsPrimaryKey { get; set; }
    
    public int DisplayOrder { get; set; }
    
    // For relationship fields
    public Guid? RelatedEntityId { get; set; }
    
    [MaxLength(20)]
    public string? RelationshipType { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Entity Entity { get; set; } = null!;
    public Entity? RelatedEntity { get; set; }
}
