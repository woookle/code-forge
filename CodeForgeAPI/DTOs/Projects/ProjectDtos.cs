using System.ComponentModel.DataAnnotations;
using CodeForgeAPI.Models;

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

    [MaxLength(20)]
    public string ArchitectureType { get; set; } = "Monolith";

    /// <summary>Optional auth settings; if null, authentication is disabled in generated project</summary>
    public AuthConfig? AuthConfig { get; set; }
}

public class UpdateProjectRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    [MaxLength(50)]
    public string? TargetStack { get; set; }

    [MaxLength(20)]
    public string? ArchitectureType { get; set; }

    /// <summary>Optional auth settings; if null, authentication is disabled in generated project</summary>
    public AuthConfig? AuthConfig { get; set; }

    /// <summary>Explicit flag: if true, AuthConfig is cleared (set to null) even when AuthConfig property is null</summary>
    public bool ClearAuth { get; set; } = false;
}
