using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class RecipeStepImage
{
    [Key]
    public int Id { get; set; }

    public int RecipeStepId { get; set; }

    public RecipeStep RecipeStep { get; set; } = null!;

    [Required, MaxLength(1000)]
    public string ImageUrl { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }
}
