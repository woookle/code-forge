using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.DTOs.Entities;

public class CreateEntityRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public int DisplayOrder { get; set; } = 0;

    /// <summary>Microservice group name. If null, defaults to the entity name.</summary>
    [MaxLength(100)]
    public string? ServiceName { get; set; }
}

public class UpdateEntityRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public int DisplayOrder { get; set; } = 0;

    /// <summary>Microservice group name. If null, defaults to the entity name.</summary>
    [MaxLength(100)]
    public string? ServiceName { get; set; }
}
