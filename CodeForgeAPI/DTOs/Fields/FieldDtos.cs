using System.ComponentModel.DataAnnotations;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.DTOs.Fields;

public class CreateFieldRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string DataType { get; set; } = "String";
    
    public bool IsRequired { get; set; } = false;
    public bool IsUnique { get; set; } = false;
    public bool IsPrimaryKey { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
    
    public Guid? RelatedEntityId { get; set; }
    public string? RelationshipType { get; set; }
}

public class UpdateFieldRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string DataType { get; set; } = "String";
    
    public bool IsRequired { get; set; } = false;
    public bool IsUnique { get; set; } = false;
    public bool IsPrimaryKey { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
    
    public Guid? RelatedEntityId { get; set; }
    public string? RelationshipType { get; set; }
}
