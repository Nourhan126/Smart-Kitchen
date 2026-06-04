using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class RecipeRecommendation
{
    [Key]
    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int RecommendedRecipeId { get; set; }
    public Recipe RecommendedRecipe { get; set; } = null!;

    public double Score { get; set; }
    public int Rank { get; set; }

    [MaxLength(50)]
    public string Source { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Season { get; set; }

    public DateTime CreatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById(
            "Egypt Standard Time"));
}
