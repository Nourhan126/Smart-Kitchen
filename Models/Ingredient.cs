using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class Ingredient
{
    [Key] 
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? DisplayName { get; set; }

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    public string? MetadataJson { get; set; }

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; } = new List<RecipeIngredient>();
}
