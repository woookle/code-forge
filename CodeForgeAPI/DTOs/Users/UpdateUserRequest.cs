using System.ComponentModel.DataAnnotations;

namespace CodeForgeAPI.DTOs.Users;

public class UpdateUserRequest
{
    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public bool? IsDarkMode { get; set; }
}
