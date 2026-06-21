using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.DTOs.Entities;

public class ReorderEntityItem
{
    [Required]
    public Guid Id { get; set; }
    public int DisplayOrder { get; set; }
}

public class ReorderEntitiesRequest
{
    [Required]
    public List<ReorderEntityItem> Items { get; set; } = new();
}

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
