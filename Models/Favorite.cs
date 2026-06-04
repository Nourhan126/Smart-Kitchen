using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class Favorite
{
    [Key] // 🔥 مهم جدًا (Primary Key)
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById(
            "Egypt Standard Time"));
}