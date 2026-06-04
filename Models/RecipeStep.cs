using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartKitchen.API.Models;

public class RecipeStep
{
    [Key]
    public int Id { get; set; }

    public int RecipeId { get; set; }

    public Recipe Recipe { get; set; } = null!;

    public int StepNumber { get; set; }

    [Column("Description")]
    public string? StepDescription { get; set; }

    public ICollection<RecipeStepImage> Images { get; set; } =
        new List<RecipeStepImage>();
}
