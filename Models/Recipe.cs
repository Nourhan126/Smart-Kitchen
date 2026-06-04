
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmartKitchen.API.Models;

public class Recipe
{
    [JsonIgnore]
    public int Id { get; set; }

    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Minutes { get; set; }

    public string? ImageUrl { get; set; }

    public string Category { get; set; } = "General";

    // ================= Nutrition =================

    public double Calories { get; set; }

    public double Carbs { get; set; }

    public double Fat { get; set; }

    public double Fiber { get; set; }

    // ================= Recipe Metadata =================

    public int NSteps { get; set; }

    public int NIngredients { get; set; }

    // ================= Navigation =================

    public ICollection<RecipeIngredient> RecipeIngredients { get; set; }
        = new List<RecipeIngredient>();

    public ICollection<RecipeStep> Steps { get; set; }
        = new List<RecipeStep>();

    public ICollection<RecipeTag> RecipeTags { get; set; }
        = new List<RecipeTag>();
    public ICollection<Favorite> Favorites { get; set; }
        = new List<Favorite>();

    public ICollection<RecipeDietClassification> DietClassifications { get; set; }
        = new List<RecipeDietClassification>();

    public ICollection<RecipeAllergenClassification> AllergenClassifications { get; set; }
        = new List<RecipeAllergenClassification>();
}
