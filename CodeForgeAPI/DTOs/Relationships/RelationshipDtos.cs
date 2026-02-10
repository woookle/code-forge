using System.ComponentModel.DataAnnotations;
using CodeForgeAPI.Models;

namespace CodeForgeAPI.DTOs.Relationships;

public class CreateRelationshipRequest
{
    [Required]
    public Guid SourceEntityId { get; set; }
    
    [Required]
    public Guid TargetEntityId { get; set; }
    
    [Required]
    [MaxLength(20)]
    public string RelationshipType { get; set; } = "OneToMany"; // OneToOne, OneToMany, ManyToMany
    
    [MaxLength(100)]
    public string? SourceFieldName { get; set; } // Optional: override default name
    
    [MaxLength(100)]
    public string? TargetFieldName { get; set; } // Optional: override default name
}

public class UpdateRelationshipRequest
{
    [Required]
    [MaxLength(20)]
    public string RelationshipType { get; set; } = "OneToMany";
    
    [MaxLength(100)]
    public string? SourceFieldName { get; set; }
    
    [MaxLength(100)]
    public string? TargetFieldName { get; set; }
}
