using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class RecipeDietClassification
{
    [Key]
    public int Id { get; set; }

    public int RecipeId { get; set; }

    public Recipe Recipe { get; set; } = null!;

    [Required, MaxLength(100)]
    public string DietName { get; set; } = string.Empty;
}
