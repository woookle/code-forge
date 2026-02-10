using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.Models;

public class User
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? FirstName { get; set; }
    
    [MaxLength(100)]
    public string? LastName { get; set; }
    
    [MaxLength(500)]
    public string? AvatarUrl { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    [MaxLength(50)]
    public string Role { get; set; } = "User";

    // Navigation properties
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
