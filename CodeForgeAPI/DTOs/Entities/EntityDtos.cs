using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.DTOs.Entities;

public class CreateEntityRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public int DisplayOrder { get; set; } = 0;
}

public class UpdateEntityRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    public int DisplayOrder { get; set; } = 0;
}
