using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class UserIngredientActivity
{
    [Key]
    public int Id { get; set; }

    public string? UserId { get; set; }

    public ApplicationUser? User { get; set; }

    [Required, MaxLength(300)]
    public string IngredientName { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ActivityType { get; set; } = "selected";

    public int? RecipeId { get; set; }

    public Recipe? Recipe { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
