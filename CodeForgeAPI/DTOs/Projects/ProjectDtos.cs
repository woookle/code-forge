using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.DTOs.Projects;

public class CreateProjectRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string TargetStack { get; set; } = "CSharp_PostgreSQL";
}

public class UpdateProjectRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string TargetStack { get; set; } = "CSharp_PostgreSQL";
}
