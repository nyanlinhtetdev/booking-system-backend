using System.ComponentModel.DataAnnotations;

namespace ResourceBookingSystem.WebApp.UserDomain.DTOs;

public sealed class CreateAdminRequestDto
{
    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = null!;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = null!;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = null!;

    [StringLength(30)]
    public string? ContactNumber { get; set; }
}