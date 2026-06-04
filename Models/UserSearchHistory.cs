using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class UserSearchHistory
{
    [Key] // 🔥 مهم جدًا
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Required, MaxLength(500)]
    public string SearchTerm { get; set; } = string.Empty;

    public DateTime SearchedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById(
            "Egypt Standard Time"));
}