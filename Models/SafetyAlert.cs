using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class SafetyAlert
{
    [Key]
    public int Id { get; set; }

    public bool IsActive { get; set; }

    [Required, MaxLength(200)]
    public string Message { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string State { get; set; } = "Normal";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
