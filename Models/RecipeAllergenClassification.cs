using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class RecipeAllergenClassification
{
    [Key]
    public int Id { get; set; }

    public int RecipeId { get; set; }

    public Recipe Recipe { get; set; } = null!;

    [Required, MaxLength(100)]
    public string AllergenName { get; set; } = string.Empty;
}
