using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class Tag
{
    [Key] // 🔥 الحل
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public ICollection<RecipeTag> RecipeTags { get; set; } = new List<RecipeTag>();
}