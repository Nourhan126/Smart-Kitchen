using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class UserDietPreference
{
    [Key]
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    [Required, MaxLength(100)]
    public string DietName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
