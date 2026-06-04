
using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace SmartKitchen.API.DTOs;

public class RecipeListDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public string Calories { get; set; } = string.Empty;

    public string Minutes { get; set; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string Difficulty { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string Season { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public static string FormatNumber(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}

public class RecipeDetailDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public int Minutes { get; set; }

    public NutritionDto Nutrition { get; set; } = new();

    public List<IngredientListItemDto> Ingredients { get; set; } = new();




    public RecipeInsightsDto Insights { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public List<RecipeStepDto> Steps { get; set; } = new();

    public bool IsFavorite { get; set; }


    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string Difficulty { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string MainIngredient { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public string Season { get; set; } = string.Empty;

    

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public List<string> MissingIngredientNames { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public Dictionary<string, List<string>> Substitutes { get; set; }
        = new();
}



public class RecipeInsightsDto
{
    public int TotalIngredients { get; set; }

    public string Difficulty { get; set; } = string.Empty;

   

    public List<string> MissingIngredientNames { get; set; } = new();

    public Dictionary<string, List<string>> Substitutes { get; set; }
        = new();
}

public class NutritionDto
{
    public string Calories { get; set; } = string.Empty;

    public string Carbs { get; set; } = string.Empty;

    public string Fat { get; set; } = string.Empty;

    public string Fiber { get; set; } = string.Empty;
}

public class RecipeStepDto
{
    public int StepNumber { get; set; }

    public string? Description { get; set; }

    public List<string> ImageUrls { get; set; } = new();
}

public class RecipeStepsResponseDto
{


    public List<RecipeStepDto> Steps { get; set; } = new();
}

public class RecipeFilterRequest
{
    public string? Search { get; set; }



    public List<string>? SkillLevels { get; set; }

    public List<string>? Diets { get; set; }

    

    public List<string>? Meals { get; set; }

    

    [JsonPropertyName("time")]
    public List<string>? Time { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 7;


}



public class TagCategoryDto
{
    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> Values { get; set; } = new();
}

public class IngredientSearchRequest
{
    public List<string> Ingredients { get; set; } = new();
    public List<string> Popular { get; set; } = new();
}

public class RecipeMatchDto : RecipeListDto
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public int MatchCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public int TotalIngredients { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public int MissingIngredientsCount { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public double ConfidenceScore { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public double MatchPercentage { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public new string Difficulty { get; set; } = string.Empty;

   

    

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public List<string> MissingIngredientNames { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public Dictionary<string, List<string>> Substitutes { get; set; }
        = new();
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;

    public List<Guid>? AttachmentIds { get; set; }
}

public class ChatResponse
{
    public string Reply { get; set; } = string.Empty;
}

public class MediaUploadResponse
{
    public Guid Id { get; set; }

    public string Url { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }
}

public class MediaAssetDto
{
    public Guid Id { get; set; }

    public string Url { get; set; } = string.Empty;

    public string MediaType { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class FavoriteRequest
{
    public int RecipeId { get; set; }
}

public class CuisineDto
{
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public List<string> Subcategories { get; set; } = new();
}

public class IngredientMetadataDto
{
    public List<string> Recent { get; set; } = new();

    public List<string> Popular { get; set; } = new();
}


public class IngredientListItemDto
{
    public string Name { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }
}

public class IngredientActivityRequest
{
    public List<string> Ingredients { get; set; } = new();

    public string ActivityType { get; set; } = "selected";

    public int? RecipeId { get; set; }
}





public class FilterOptionsDto
{
    public List<string> SkillLevels { get; set; } = new();
    public List<string> Diets { get; set; } = new();
    public List<string> Meals { get; set; } = new();
    public List<string> Time { get; set; } = new();
}
