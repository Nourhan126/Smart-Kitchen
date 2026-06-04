using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartKitchen.API.Data;
using Microsoft.Extensions.DependencyInjection;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Models;
using System;

namespace SmartKitchen.API.Services;

public class RecipeService : IRecipeService
{
    private const int RecipeListPageSize = 7;

    private readonly ApplicationDbContext _db;
    private readonly IImageSearchService _imageService;
    private readonly IImageUrlBuilder _imageUrlBuilder;
    private readonly IRecommendationService _recommendations;


    private static readonly Dictionary<string, string[]> CuisineKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["North Indian"] = ["north indian", "punjabi", "mughal", "north-indian", "tandoori", "biryani"],
            ["South Indian"] = ["south indian", "tamil", "kerala", "karnataka", "andhra", "dosa", "idli", "sambar", "udupi", "south-indian"],
            ["Italian"] = ["italian", "italy", "pasta", "pizza", "risotto", "tiramisu", "carbonara", "pesto", "marinara"],
            ["Chinese"] = ["chinese", "china", "stir-fry", "stir fry", "dim sum", "wonton", "dumpling", "fried rice", "chow mein"],
            ["Street Food"] = ["street food", "street-food", "chaat", "vada pav", "pani puri", "shawarma"],
            ["Regional"] = ["regional indian", "regional"],
            ["Korean"] = ["korean", "korea", "kimchi", "bibimbap", "bulgogi", "japchae"],
            ["Japanese"] = ["japanese", "japan", "sushi", "ramen", "tempura", "miso", "teriyaki", "udon"],
            ["Mexican"] = ["mexican", "mexico", "taco", "burrito", "enchilada", "quesadilla", "salsa", "guacamole"],
            ["Mediterranean"] = ["mediterranean", "greek", "turkish", "lebanese", "middle eastern", "hummus", "falafel"],
            ["American"] = ["american", "bbq", "barbecue", "burger", "mac and cheese", "southern"],
            ["Burmese"] = ["burmese", "myanmar", "burma"],
        };
 
    private static readonly Dictionary<string, string[]> ItalianSubcategoryKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Pasta"] = ["pasta", "spaghetti", "fettuccine", "penne", "linguine", "rigatoni", "tagliatelle", "lasagna", "bucatini", "farfalle"],
            ["Pizza"] = ["pizza"],
            ["Risotto"] = ["risotto"],
            ["Seafood Dishes"] = ["seafood", "shrimp", "clam", "mussel", "scallop", "calamari", "squid"],
            ["Meat Dishes"] = ["osso buco", "saltimbocca", "scaloppine", "involtini"],
            ["Antipasti"] = ["antipasto", "antipasti", "bruschetta", "crostini", "caprese", "prosciutto"],
            ["Soups and Stews"] = ["minestrone", "ribollita", "zuppa"],
            ["Desserts"] = ["tiramisu", "panna cotta", "cannoli", "gelato", "biscotti", "zabaglione"],
            ["Sauces"] = ["pesto", "marinara", "bolognese", "alfredo", "arrabbiata", "amatriciana"],
        };

    private static readonly List<string> DefaultPopularIngredients =
  [
      "Milk",
    "Eggs",
    "Bread",
    "Chicken",
    "Onion",
    "Butter",
    "Mushroom",
    "Potato",
    "Tomato",
    "Cheese",
    "Curd"
  ];
    public RecipeService(
      ApplicationDbContext db,
      IImageSearchService imageService,
      IImageUrlBuilder imageUrlBuilder,
      IRecommendationService recommendations)
    {
        _db = db;
        _imageService = imageService;
        _imageUrlBuilder = imageUrlBuilder;
        _recommendations = recommendations;
    }


   
   
    private string? BuildImageUrl(Recipe r) =>
        _imageUrlBuilder.NormalizeImageUrl(r.ImageUrl, "recipe");

    private string? BuildIngredientImageUrl(string? imageUrl) =>
        _imageUrlBuilder.NormalizeImageUrl(imageUrl, "ingredient");

    private string? BuildStepImageUrl(string? imageUrl) =>
        _imageUrlBuilder.NormalizeImageUrl(imageUrl, "step");

    private static int NormalizePage(int page) =>
        Math.Max(1, page);

    private static int NormalizeRecipeListPageSize(int pageSize = RecipeListPageSize) =>
        RecipeListPageSize;

    private static int NormalizeRecipeListCount(int count = RecipeListPageSize) =>
        RecipeListPageSize;

    private static string[] GetSeasonKeywords(string season) =>
        season.Trim().ToLowerInvariant() switch
        {
            "summer" => ["summer", "salad", "smoothie", "iced", "cold", "ice cream", "juice", "lemonade"],
            "winter" => ["winter", "soup", "stew", "hot", "warm", "chili", "cocoa", "tea"],
            "spring" => ["spring", "salad", "green", "fresh", "asparagus", "pea", "herb"],
            _ => ["autumn", "fall", "pumpkin", "apple", "cinnamon", "roast", "stew"]
        };

    private async Task<HashSet<int>> GetUserFavoriteIdsAsync(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return new HashSet<int>();

        return (await _db.Favorites
            .Where(f => f.UserId == userId)
            .Select(f => f.RecipeId)
            .ToListAsync())
            .ToHashSet();
    }

    private RecipeListDto MapToListDto(
        Recipe r,
        HashSet<int> favoriteIds)
    {
        return new RecipeListDto
        {
            Id = r.Id,
            Name = r.Name,
            ImageUrl = BuildImageUrl(r),
            Calories = $"{r.Calories:0.#} kcal",
            Minutes = $"{r.Minutes} min",
            Difficulty = GetDifficulty(r),
            Season = GetSeasonFromRecipe(r),
            IsFavorite = favoriteIds.Contains(r.Id)
        };
    }

    private async Task EnsureRecipeImagesAsync(
        IEnumerable<Recipe> recipes,
        CancellationToken ct = default)
    {
        var changed = false;

        foreach (var recipe in recipes)
        {
            if (BuildImageUrl(recipe) is not null)
            {
                continue;
            }

            try
            {
                var result = await _imageService.SearchImageAsync(
                    recipe.Name,
                    targetType: "recipe",
                    ct: ct);

                if (result is { Success: true } &&
                    !string.IsNullOrWhiteSpace(result.ImagePath))
                {
                    recipe.ImageUrl =
                        _imageUrlBuilder.BuildPublicImageUrl(result);
                    changed = true;
                }
            }
            catch
            {
                // Intentionally best-effort to avoid failing API responses.
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureIngredientImagesAsync(
        IEnumerable<RecipeIngredient> recipeIngredients,
        CancellationToken ct = default)
    {
        var changed = false;

        foreach (var recipeIngredient in recipeIngredients)
        {
            var ingredient = recipeIngredient.Ingredient;
            if (ingredient == null || BuildIngredientImageUrl(ingredient.ImageUrl) is not null)
            {
                continue;
            }

            try
            {
                var searchName =
                    ingredient.DisplayName ?? ingredient.Name;

                var result = await _imageService.SearchImageAsync(
                    searchName,
                    targetType: "ingredient",
                    ct: ct);

                if (result is { Success: true } &&
                    !string.IsNullOrWhiteSpace(result.ImagePath))
                {
                    ingredient.ImageUrl =
                        _imageUrlBuilder.BuildPublicImageUrl(result);
                    changed = true;
                }
            }
            catch
            {
                // Intentionally best-effort to avoid failing API responses.
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureStepImagesAsync(
        Recipe recipe,
        CancellationToken ct = default)
    {
        var changed = false;

        foreach (var step in recipe.Steps.OrderBy(s => s.StepNumber))
        {
            if (step.Images.Any(i => BuildStepImageUrl(i.ImageUrl) is not null))
            {
                continue;
            }

            try
            {
                var result = await _imageService.SearchImageAsync(
                    recipe.Name,
                    targetType: "step",
                    context: step.StepDescription,
                    ct: ct);

                if (result is { Success: true } &&
                    !string.IsNullOrWhiteSpace(result.ImagePath))
                {
                    step.Images.Add(new RecipeStepImage
                    {
                        ImageUrl = _imageUrlBuilder.BuildPublicImageUrl(result),
                        SortOrder = 0,
                        Source = "python-service"
                    });
                    changed = true;
                }
            }
            catch
            {
                // Intentionally best-effort to avoid failing API responses.
            }
        }

        if (changed)
        {
            await _db.SaveChangesAsync(ct);
        }
    }
    private static IQueryable<Recipe> ApplySkillLevelFilters(
     IQueryable<Recipe> query,
     List<string>? values)
    {
        if (values == null || values.Count == 0)
            return query;

        var normalized = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().ToLower())
            .ToList();

        return query.Where(r =>

            (normalized.Contains("easy")
                &&
                r.Minutes <= 30
                &&
                r.NIngredients <= 8
                &&
                r.NSteps <= 8)

            ||

            (normalized.Contains("medium")
                &&
                r.Minutes > 30
                &&
                r.Minutes <= 60)

            ||

            (normalized.Contains("advanced")
                &&
                (
                    r.Minutes > 60
                    ||
                    r.NIngredients > 12
                    ||
                    r.NSteps > 12
                ))
        );
    }
    private static IQueryable<Recipe> ApplyFilterRequest(
        IQueryable<Recipe> query,
        RecipeFilterRequest filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();

            query = query.Where(r =>
                r.Name.ToLower().Contains(search));
        }



        query = ApplySkillLevelFilters(
     query,
     filter.SkillLevels);

        query = ApplyDietFilters(
            query,
            filter.Diets);

        query = ApplyMealFilters(
            query,
            filter.Meals);

        var time = filter.Time;

        if (time is { Count: > 0 })
        {
            query = ApplyTimeFilters(
                query,
                time);
        }

        return query;
    }
    private static IQueryable<Recipe> ApplyMealFilters(
    IQueryable<Recipe> query,
    List<string>? values)
    {
        if (values == null || values.Count == 0)
            return query;

        var normalized = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().ToLower())
            .ToList();

        return query.Where(r =>
            r.RecipeTags.Any(rt =>
                normalized.Contains(
                    rt.Tag.Name.ToLower())));
    }
    private static IQueryable<Recipe> ApplyTagOrNameFilters(
        IQueryable<Recipe> query,
        List<string>? values)
    {
        if (values is not { Count: > 0 })
        {
            return query;
        }

        var normalized = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().ToLower())
            .ToList();

        if (normalized.Count == 0)
        {
            return query;
        }

        return query.Where(r =>
            normalized.Any(v =>
                r.Name.ToLower().Contains(v) ||
                (r.Description != null &&
                 r.Description.ToLower().Contains(v)) ||
                r.RecipeTags.Any(t =>
                    t.Tag.Name.ToLower().Contains(v))));
    }

    private static IQueryable<Recipe> ApplyDietFilters(
    IQueryable<Recipe> query,
    List<string>? values)
    {
        if (values is not { Count: > 0 } ||
            values.Any(v => string.Equals(v, "None", StringComparison.OrdinalIgnoreCase)))
        {
            return query;
        }

        var normalized = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().ToLower())
            .ToList();

        normalized = normalized
            .Select(v => v switch
            {
                "veg" => "vegetarian",
                "non-veg" => "non-vegetarian",
                _ => v
            })
            .ToList();

        return query.Where(r =>
            normalized.Any(v =>
                r.DietClassifications.Any(d =>
                    d.DietName.ToLower().Contains(v)) ||
                r.RecipeTags.Any(t =>
                    t.Tag.Name.ToLower().Contains(v)) ||
                r.Name.ToLower().Contains(v) ||
                (r.Description != null &&
                 r.Description.ToLower().Contains(v))));
    }

    private static IQueryable<Recipe> ApplyAllergenExclusions(
        IQueryable<Recipe> query,
        List<string>? values)
    {
        if (values is not { Count: > 0 } ||
            values.Any(v => string.Equals(v, "None", StringComparison.OrdinalIgnoreCase)))
        {
            return query;
        }

        var normalized = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().ToLower())
            .ToList();

        return query.Where(r =>
            !normalized.Any(v =>
                r.AllergenClassifications.Any(a =>
                    a.AllergenName.ToLower().Contains(v)) ||
                r.RecipeIngredients.Any(i =>
                    i.Ingredient.Name.ToLower().Contains(v)) ||
                r.RecipeTags.Any(t =>
                    t.Tag.Name.ToLower().Contains(v))));
    }

    private static IQueryable<Recipe> ApplyTimeFilters(
        IQueryable<Recipe> query,
        List<string> values)
    {
        var normalized = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().ToLower())
            .ToList();

        return query.Where(r =>
            normalized.Any(v =>
                (v == "5-10 min" && r.Minutes >= 5 && r.Minutes < 10) ||
                (v == "10-20 min" && r.Minutes >= 10 && r.Minutes < 20) ||
                (v == "20-30 min" && r.Minutes >= 20 && r.Minutes < 30) ||
                (v == "30-45 min" && r.Minutes >= 30 && r.Minutes < 45) ||
                (v == "45-60 min" && r.Minutes >= 45 && r.Minutes <= 60) ||
                ((v == "> 1 hr" || v == ">1 hr" || v == ">60" || v == ">60 min") &&
                 r.Minutes > 60)));
    }

  

    public async Task<PagedResult<RecipeListDto>> GetRecipesAsync(
        RecipeFilterRequest filter,
        string? userId)
    {
        var query = ApplyFilterRequest(_db.Recipes.AsQueryable(), filter);

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var excludedAllergens =
                await _db.UserAllergenPreferences
                    .Where(p => p.UserId == userId)
                    .Select(p => p.AllergenName)
                    .ToListAsync();

            query = ApplyAllergenExclusions(query, excludedAllergens);
        }


        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var p =
            NormalizePage(filter.Page);

        var ps =
            NormalizeRecipeListPageSize(filter.PageSize);

        var total =
            await query.CountAsync();

        var items = await query
            .OrderBy(r => r.Id)
            .Skip((p - 1) * ps)
            .Take(ps)
            .ToListAsync();

        await EnsureRecipeImagesAsync(items);

        return new PagedResult<RecipeListDto>
        {
            Items = items
        .Select(r => MapToListDto(r, favoriteIds))
        .ToList(),

            TotalCount = total,

            Page = p,

            PageSize = ps
        };
    }



    public async Task<RecipeDetailDto?> GetRecipeByIdAsync(
    int id,
    string? userId,
    List<string>? selectedIngredients = null)
    {
        var recipe = await _db.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)

            .Include(r => r.Steps.OrderBy(s => s.StepNumber))
                .ThenInclude(s => s.Images)

            .Include(r => r.RecipeTags)
                .ThenInclude(rt => rt.Tag)

            .FirstOrDefaultAsync(r => r.Id == id);

        if (recipe == null)
            return null;

        await EnsureRecipeImagesAsync(new[] { recipe });
        await EnsureIngredientImagesAsync(recipe.RecipeIngredients);
        await EnsureStepImagesAsync(recipe);

        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var ingredientNames = recipe.RecipeIngredients
            .Select(i => i.Ingredient.Name)
            .ToList();
        ///////
        Console.WriteLine("========== RECIPE DEBUG ==========");
        Console.WriteLine($"RecipeId = {recipe.Id}");
        Console.WriteLine($"Minutes = {recipe.Minutes}");
        Console.WriteLine($"NIngredients (DB) = {recipe.NIngredients}");
        Console.WriteLine($"NSteps (DB) = {recipe.NSteps}");
        Console.WriteLine($"Actual Ingredients Count = {recipe.RecipeIngredients.Count}");
        Console.WriteLine($"Actual Steps Count = {recipe.Steps.Count}");
        Console.WriteLine("=================================");

        var insights = BuildInsights(
            recipe,
            ingredientNames,
            selectedIngredients);
        var ingredients = ingredientNames
    .Select(x => new IngredientListItemDto
    {
        Name = x,
        ImageUrl = null
    })
    .ToList();

        return new RecipeDetailDto
        {
            Id = recipe.Id,

            Name = recipe.Name,

            ImageUrl = BuildImageUrl(recipe),

            Minutes = recipe.Minutes,

            Nutrition = new NutritionDto
            {
                Calories = $"{RecipeListDto.FormatNumber(recipe.Calories)} kcal",
                Carbs = $"{RecipeListDto.FormatNumber(recipe.Carbs)} g",
                Fat = $"{RecipeListDto.FormatNumber(recipe.Fat)} g",
                Fiber = $"{RecipeListDto.FormatNumber(recipe.Fiber)} g"
            },


            Ingredients = ingredients,


            Insights = insights,

            Steps = recipe.Steps
                .OrderBy(s => s.StepNumber)
                .Select(s => new RecipeStepDto
                {
                    StepNumber = s.StepNumber,
                    Description = s.StepDescription,
                    ImageUrls = s.Images
                        .OrderBy(i => i.SortOrder)
                        .Select(i => BuildStepImageUrl(i.ImageUrl))
                        .Where(url => url is not null)
                        .Select(url => url!)
                        .ToList()
                })
                .ToList(),

            Difficulty = insights.Difficulty,

            

            

            

            MissingIngredientNames = insights.MissingIngredientNames,

            Substitutes = insights.Substitutes,

            IsFavorite =
    favoriteIds.Contains(recipe.Id)
        };
    }

    public async Task<List<RecipeListDto>> GetTrendingAsync(
        string? userId,
        int count = 7)
    {
        count = NormalizeRecipeListCount(count);

        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var recipes = await _db.Recipes
            .OrderByDescending(r => r.Favorites.Count)
            .ThenBy(r => r.Minutes)
            .Take(count)
            .ToListAsync();

        await EnsureRecipeImagesAsync(recipes);

        return recipes
            .Select(r => MapToListDto(r, favoriteIds))
            .ToList();
    }


    public async Task<List<RecipeListDto>> GetRecommendedAsync(
        string? userId,
        int count = 7)
    {
        count = NormalizeRecipeListCount(count);

        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var ids = await _recommendations
            .GetRecommendedIdsAsync(userId, count);

        if (ids.Count == 0)
        {
            return await GetTrendingAsync(userId, count);
        }

        var order = ids
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        var recipes = await _db.Recipes
            .Where(r => ids.Contains(r.Id))
            .ToListAsync();

        await EnsureRecipeImagesAsync(recipes);

        return recipes
            .OrderBy(r => order.GetValueOrDefault(r.Id, int.MaxValue))
            .Select(r => MapToListDto(r, favoriteIds))
            .ToList();
    }



    public async Task<PagedResult<RecipeListDto>> GetByCategoryAsync(
        string tagName,
        int page,
        int pageSize,
        string? userId)
    {
        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var p =
            NormalizePage(page);

        var ps =
            NormalizeRecipeListPageSize(pageSize);

        IQueryable<Recipe> query =
            tagName switch
            {
                "<10 min" or "<10"
                    => _db.Recipes.Where(r => r.Minutes < 10),

                "10-20 min" or "10-20"
                    => _db.Recipes.Where(r =>
                        r.Minutes >= 10 &&
                        r.Minutes < 20),

                "20-30 min" or "20-30"
                    => _db.Recipes.Where(r =>
                        r.Minutes >= 20 &&
                        r.Minutes < 30),

                "30-60 min" or "30-60"
                    => _db.Recipes.Where(r =>
                        r.Minutes >= 30 &&
                        r.Minutes < 60),

                ">60 min" or ">60"
                    => _db.Recipes.Where(r =>
                        r.Minutes >= 60),

                _ =>
                    _db.Recipes.Where(r =>
                        r.RecipeTags.Any(t =>
                            t.Tag.Name.ToLower() ==
                            tagName.ToLower()))
            };

        var total =
            await query.CountAsync();

        var items = await query
            .OrderBy(r => r.Id)
            .Skip((p - 1) * ps)
            .Take(ps)
            .ToListAsync();
       
        await EnsureRecipeImagesAsync(items);

        return new PagedResult<RecipeListDto>
        {
            Items = items
                .Select(r => MapToListDto(r, favoriteIds))
                .ToList(),

            TotalCount = total,

            Page = p,

            PageSize = ps
        };
    }



    public async Task<List<RecipeMatchDto>>
    GetByIngredientsAsync(
        IngredientSearchRequest request,
        string? userId)
    {
        var allIngredients =
    (request.Ingredients ?? new List<string>())
    .Concat(request.Popular ?? new List<string>())
    .Where(i => !string.IsNullOrWhiteSpace(i))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();

        if (allIngredients.Count == 0)
        {
            return new List<RecipeMatchDto>();
        }
        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var inputIngredients = allIngredients
    .Select(NormalizeIngredient)
    .Where(i => i.Length > 0)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();

        if (inputIngredients.Count == 0)
        {
            return new List<RecipeMatchDto>();
        }

        await SaveIngredientActivityAsync(
    userId,
    new IngredientActivityRequest
    {
        Ingredients = allIngredients,
        ActivityType = "generated"
    });

        var recipes = await _db.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .ToListAsync();

        // await EnsureRecipeImagesAsync(recipes);

        var scored = recipes
            .Select(r =>
            {
                var ingredientNames =
                    r.RecipeIngredients
                        .Select(ri => ri.Ingredient.Name)
                        .ToList();

                var recipeIngNormalized =
                    ingredientNames
                        .Select(NormalizeIngredient)
                        .ToList();

                int matchCount =
                    inputIngredients.Count(input =>
                        recipeIngNormalized.Any(ri =>
                            ri == input ||
                            ri.Contains(input) ||
                            input.Contains(ri)));

                int missing =
                    r.RecipeIngredients.Count > 0
                        ? r.RecipeIngredients.Count - matchCount
                        : 0;

                double confidence =
                    r.RecipeIngredients.Count > 0
                        ? (double)matchCount /
                          Math.Max(
                              inputIngredients.Count,
                              r.RecipeIngredients.Count)
                        : 0;

                return new
                {
                    Recipe = r,

                    MatchCount = matchCount,

                    Missing = missing,

                    Confidence = confidence,

                    MatchedIngredients =
         recipeIngNormalized
             .Where(ri =>
                 inputIngredients.Any(input =>
                     ri == input ||
                     ri.Contains(input) ||
                     input.Contains(ri)))
             .Distinct()
             .ToList(),

                    MissingIngredientNames =
         recipeIngNormalized
             .Where(ri =>
                 !inputIngredients.Any(input =>
                     ri == input ||
                     ri.Contains(input) ||
                     input.Contains(ri)))
             .Distinct()
             .ToList()
                };
            })
            .Where(x => x.MatchCount > 0)
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.MatchCount)
            .ThenBy(x => x.Missing)
            .ThenBy(x => x.Recipe.Minutes)
            .Take(7)
            .ToList();


        return scored
            .Select(x => new RecipeMatchDto
            {
                // ===== CARD DATA =====

                Id = x.Recipe.Id,

                Name = x.Recipe.Name,

                ImageUrl = BuildImageUrl(x.Recipe),

                Calories = $"{RecipeListDto.FormatNumber(x.Recipe.Calories)} kcal",
                Minutes = $"{x.Recipe.Minutes} min",

                IsFavorite =
                    favoriteIds.Contains(
                        x.Recipe.Id),

                // ===== DETAILS SECTION =====

                Difficulty =
                    GetDifficulty(
                        x.Recipe.Minutes,
                        x.Recipe.RecipeIngredients.Count,
                        x.Recipe.NSteps),

               

                // IMPORTANT
                Substitutes =
                    GetSubstituteMap(
                        x.MissingIngredientNames),

                // ===== MATCH INFO =====

                MatchCount =
                    x.MatchCount,

                TotalIngredients =
                    x.Recipe.RecipeIngredients.Count,

                MissingIngredientsCount =
                    x.Missing,

                ConfidenceScore =
                    Math.Round(
                        x.Confidence * 100,
                        1),

                MatchPercentage =
                    Math.Round(
                        x.Confidence * 100,
                        1)
            })
            .ToList();
    }

    private static RecipeInsightsDto BuildInsights(
        Recipe recipe,
        List<string> ingredientNames,
        List<string>? selectedIngredients)
    {
        var recipeIngredients = ingredientNames
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .ToList();

        var normalizedRecipeIngredients = recipeIngredients
            .Select(NormalizeIngredient)
            .ToList();

        var normalizedSelected = selectedIngredients?
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(NormalizeIngredient)
            .Where(i => i.Length > 0)
            .Distinct()
            .ToList() ?? new List<string>();

        List<string> matched;
        List<string> missing;

        if (normalizedSelected.Count == 0)
        {
            matched = recipeIngredients;
            missing = new List<string>();
        }
        else
        {
            matched = recipeIngredients
                .Where((ingredient, index) =>
                    normalizedSelected.Any(input =>
                        normalizedRecipeIngredients[index] == input ||
                        normalizedRecipeIngredients[index].Contains(input) ||
                        input.Contains(normalizedRecipeIngredients[index])))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            missing = recipeIngredients
                .Where((ingredient, index) =>
                    !normalizedSelected.Any(input =>
                        normalizedRecipeIngredients[index] == input ||
                        normalizedRecipeIngredients[index].Contains(input) ||
                        input.Contains(normalizedRecipeIngredients[index])))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var totalIngredients =
            recipe.NIngredients > 0
                ? recipe.NIngredients
                : recipeIngredients.Count;

        var matchCount = matched.Count;

        var denominator =
            normalizedSelected.Count > 0
                ? Math.Max(normalizedSelected.Count, totalIngredients)
                : Math.Max(1, totalIngredients);

        var score =
            totalIngredients > 0
                ? Math.Round((double)matchCount / denominator * 100, 1)
                : 0;

        return new RecipeInsightsDto
        {
            TotalIngredients = totalIngredients,

            Difficulty = GetDifficulty(recipe),


            MissingIngredientNames = missing,

            Substitutes = GetSubstituteMap(
         missing.Count > 0
             ? missing
             : recipeIngredients)
        };
    }

    private static string NormalizeIngredient(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var normalized =
            System.Text.RegularExpressions.Regex
                .Replace(
                    raw.ToLower().Trim(),
                    @"[^a-z\s]",
                    " ");

        return System.Text.RegularExpressions.Regex
            .Replace(normalized, @"\s+", " ")
            .Trim();
    }
    private static string GetDifficulty(
     int minutes,
     int nIngredients,
     int nSteps)
    {
        if (minutes > 90)
            return "Advanced";

        if (minutes > 45)
            return "Medium";

        if (nSteps <= 8 &&
            nIngredients <= 8)
            return "Easy";

        if (nSteps <= 15 &&
            nIngredients <= 12)
            return "Medium";

        return "Advanced";
    }

    private static string GetMainIngredient(
        List<string> ingredients)
    {
        var items = ingredients
            .Select(x => x.ToLower())
            .ToList();

        string[] priority =
        {
        "chicken",
        "beef",
        "fish",
        "salmon",
        "shrimp",
        "lamb",
        "egg",
        "tofu",
        "potato",
        "tomato",
        "onion"
    };

        foreach (var item in priority)
        {
            if (items.Any(x => x.Contains(item)))
                return item;
        }

        return items.FirstOrDefault()
               ?? string.Empty;
    }

    private static string GetSeason()
    {
        return DateTime.Now.Month switch
        {
            12 or 1 or 2 => "Winter",
            3 or 4 or 5 => "Spring",
            6 or 7 or 8 => "Summer",
            _ => "Autumn"
        };
    }

    private static List<string> GetSubstitutes(
        List<string> ingredients)
    {
        var map =
            new Dictionary<string, List<string>>
            {
                ["chicken"] =
                    new()
                    {
                    "turkey",
                    "tofu"
                    },

                ["beef"] =
                    new()
                    {
                    "lamb",
                    "mushroom"
                    },

                ["milk"] =
                    new()
                    {
                    "almond milk",
                    "soy milk"
                    },

                ["egg"] =
                    new()
                    {
                    "banana",
                    "flaxseed"
                    }
            };

        var result =
            new List<string>();

        foreach (var ingredient in ingredients)
        {
            if (map.TryGetValue(
                    ingredient.ToLower(),
                    out var subs))
            {
                result.AddRange(subs);
            }
        }

        return result
            .Distinct()
            .ToList();
    }

    public async Task<IngredientMetadataDto> GetIngredientsMetadataAsync(
        string? userId,
        int recentLimit = 2,
        int popularLimit = 11)
    {
        var defaults = new IngredientMetadataDto
        {
            Recent = new List<string>(),
            Popular = new List<string>
            {
                "Milk",
                "Eggs",
                "Bread",
                "Chicken",
                "Onion",
                "Butter",
                "Mushroom",
                "Potato",
                "Tomato",
                "Cheese",
                "Curd"
            }
        };

        var recent = new List<string>();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            recent = await _db.UserIngredientActivities
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => a.IngredientName)
                .ToListAsync();

            if (recent.Count < recentLimit)
            {
                var searches = await _db.UserSearchHistories
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.SearchedAt)
                    .Select(s => s.SearchTerm)
                    .ToListAsync();

                recent.AddRange(searches);
            }
        }

        recent = recent
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => ToDisplayName(i))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(recentLimit)
            .ToList();

        var popular = await _db.UserIngredientActivities
            .GroupBy(i => i.IngredientName)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(popularLimit)
            .ToListAsync();

        if (popular.Count == 0)
        {
            popular = await _db.RecipeIngredients
                .GroupBy(i => i.Ingredient.Name)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(popularLimit)
                .ToListAsync();
        }

        popular = popular
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => ToDisplayName(i))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(popularLimit)
            .ToList();

        return new IngredientMetadataDto
        {
            Recent = recent,
            Popular = popular.Count > 0 ? popular : defaults.Popular
        };
    }

    public async Task SaveIngredientActivityAsync(
        string? userId,
        IngredientActivityRequest request)
    {
        var normalizedIngredients = request.Ingredients?
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (normalizedIngredients.Count == 0)
        {
            return;
        }

        foreach (var ingredient in normalizedIngredients)
        {
            _db.UserIngredientActivities.Add(
                new UserIngredientActivity
                {
                    UserId = userId,
                    IngredientName = ingredient,
                    ActivityType = string.IsNullOrWhiteSpace(request.ActivityType)
                        ? "selected"
                        : request.ActivityType.Trim(),
                    RecipeId = request.RecipeId,
                    CreatedAt = DateTime.UtcNow
                });
        }

        await _db.SaveChangesAsync();
    }

    private static string ToDisplayName(string value)
    {
        var words = value.Trim()
            .Replace("_", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(
            " ",
            words.Select(w =>
                char.ToUpperInvariant(w[0]) +
                (w.Length > 1 ? w[1..].ToLowerInvariant() : "")));
    }

    public async Task<List<CuisineDto>> GetCuisinesAsync()
    {
        var cuisines = CuisineKeywords
            .Select(c => new CuisineDto
            {
                Name = c.Key,

                Slug = c.Key
                    .ToLower()
                    .Replace(" ", "-"),

                Subcategories =
                    c.Key == "Italian"
                        ? ItalianSubcategoryKeywords.Keys.ToList()
                        : new List<string>()
            })
            .ToList();

        return await Task.FromResult(cuisines);
    }

    public async Task<PagedResult<RecipeListDto>>
        GetByCuisineAsync(
            string cuisine,
            int page,
            int pageSize,
            string? userId)
    {
        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var keywords =
            CuisineKeywords.TryGetValue(
                cuisine,
                out var kws)
                    ? kws
                    : new[] { cuisine };

        var query = _db.Recipes.Where(r =>

            keywords.Any(k =>

                r.Name.ToLower().Contains(k)

                ||

                (r.Description != null &&
                 r.Description.ToLower().Contains(k))

                ||

                r.RecipeTags.Any(t =>
                    t.Tag.Name.ToLower().Contains(k))
            ));

        var total =
            await query.CountAsync();

        var p = NormalizePage(page);
        var ps = NormalizeRecipeListPageSize(pageSize);

        var items = await query
            .OrderBy(r => r.Id)
            .Skip((p - 1) * ps)
            .Take(ps)
            .ToListAsync();

        await EnsureRecipeImagesAsync(items);

        return new PagedResult<RecipeListDto>
        {
            Items = items
                .Select(r =>
                    MapToListDto(r, favoriteIds))
                .ToList(),

            TotalCount = total,

            Page = p,

            PageSize = ps
        };
    }

  

    public async Task<PagedResult<RecipeListDto>>
        GetByCuisineSubcategoryAsync(
            string cuisine,
            string subcategory,
            int page,
            int pageSize,
            string? userId)
    {
        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var keywords =
            ItalianSubcategoryKeywords.TryGetValue(
                subcategory,
                out var kws)
                    ? kws
                    : new[] { subcategory };

        var query = _db.Recipes.Where(r =>

            keywords.Any(k =>

                r.Name.ToLower().Contains(k)

                ||

                (r.Description != null &&
                 r.Description.ToLower().Contains(k))

                ||

                r.RecipeTags.Any(t =>
                    t.Tag.Name.ToLower().Contains(k))
            ));

        var total =
            await query.CountAsync();

        var p = NormalizePage(page);
        var ps = NormalizeRecipeListPageSize(pageSize);

        var items = await query
            .OrderBy(r => r.Id)
            .Skip((p - 1) * ps)
            .Take(ps)
            .ToListAsync();

        await EnsureRecipeImagesAsync(items);

        return new PagedResult<RecipeListDto>
        {
            Items = items
                .Select(r =>
                    MapToListDto(r, favoriteIds))
                .ToList(),

            TotalCount = total,

            Page = p,

            PageSize = ps
        };
    }


    public async Task<List<TagCategoryDto>>
        GetFilterCategoriesAsync()
    {
        return new List<TagCategoryDto>
    {
        // ================= SKILL LEVEL =================

        new()
        {
            Category = "Skill Level",

            Description =
                "Easy, Medium, Advanced",

            Values = new List<string>
            {
                "Easy",
                "Medium",
                "Advanced"
            }
        },

        // ================= RECIPE TIME =================

        new()
        {
            Category = "Recipe Time",

            Description =
                "Under 30 min, 1 Hour and more",

            Values = new List<string>
            {
                "5-10 min",
                "10-20 min",
                "20-30 min",
                "30-45 min",
                "45-60 min",
                "> 1 hr"
            }
        },

        // ================= DIET =================

        new()
        {
            Category = "Diet",

            Description =
                "Vegetarian, Non-Vegetarian, Vegan, Gluten-free and more",

            Values = new List<string>
            {
                "Vegetarian",
                "Non-Vegetarian",
                "Vegan",
                "Gluten-Free",
                "Dairy-Free",
                "Keto",
                "Low-Carb",
                "Paleo",
                "None"
            }
        },

        // ================= CUISINE =================

        new()
        {
            Category = "Cuisine",

            Description =
                "Indian, Chinese, Italian and More",

            Values = new List<string>
            {
                "North Indian",
                "South Indian",
                "Italian",
                "Chinese",
                "Street Food",
                "Regional",
                "Korean",
                "Japanese",
                "Mexican",
                "Mediterranean",
                "American",
                "Burmese"
            }
        },

        // ================= COURSES =================

        new()
        {
            Category = "Courses",

            Description =
                "Appetizers, Main Course, Sides, Salads, Desserts and more",

            Values = new List<string>
            {
                "Appetiser",
                "Main Course",
                "Sides",
                "Salads",
                "Desserts",
                "Breakfast",
                "Lunch",
                "Dinner",
                "Snack",
                "Brunch"
            }
        },

        // ================= HEALTHY EATING =================

        new()
        {
            Category = "Healthy Eating",

            Description =
                "Recipes that are low in Calories, Fat & Sugar, and High in nutrients",

            Values = new List<string>
            {
                "Low Calories",
                "Low Fat",
                "Low Sugar",
                "High Protein",
                "Healthy",
                "Fitness Meals"
            }
        },

        // ================= SMART APPLIANCES =================

        new()
        {
            Category = "Smart Appliances",

            Description =
                "Recipes for your Smart Cooker, Smart Thermometer and more",

            Values = new List<string>
            {
                "Air Fryer",
                "Smart Cooker",
                "Smart Oven",
                "Pressure Cooker",
                "Thermometer",
                "Blender"
            }
        },

        // ================= BEVERAGES =================

        new()
        {
            Category = "Beverages",

            Description =
                "Juices, Shakes, Mocktails, Cocktails and more",

            Values = new List<string>
            {
                "Juices",
                "Shakes",
                "Mocktails",
                "Cocktails",
                "Coffee",
                "Tea",
                "Smoothies"
            }
        },

        // ================= SEASONAL =================

        new()
        {
            Category = "Seasonal",

            Description =
                "Recipes with season’s special ingredients",

            Values = new List<string>
            {
                "Summer",
                "Winter",
                "Spring",
                "Autumn",
                "Ramadan",
                "Christmas"
            }
        },

        // ================= SOUPS & STEWS =================

        new()
        {
            Category = "Soups & Stews",

            Description =
                "Chili, Noodle Soup and more",

            Values = new List<string>
            {
                "Chicken Soup",
                "Noodle Soup",
                "Tomato Soup",
                "Vegetable Soup",
                "Chili",
                "Stew"
            }
        }
    };
    }


    public async Task<List<RecipeListDto>>
        GetSeasonalAsync(
            string? userId,
            int count)
    {
        count = NormalizeRecipeListCount(count);

        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var ids = await _recommendations
            .GetSeasonalIdsAsync(userId, count);

        if (ids.Count == 0)
        {
            var season = GetSeason();
            var keywords = GetSeasonKeywords(season);

            var seasonalRecipes = await _db.Recipes
                .Where(r => keywords.Any(k =>
                    r.Name.ToLower().Contains(k) ||
                    (r.Description != null &&
                     r.Description.ToLower().Contains(k)) ||
                    r.RecipeTags.Any(t =>
                        t.Tag.Name.ToLower().Contains(k))))
                .OrderBy(r => r.Minutes)
                .ThenBy(r => r.Name)
                .Take(count)
                .ToListAsync();

            if (seasonalRecipes.Count == 0)
            {
                seasonalRecipes = await _db.Recipes
                    .OrderBy(r => r.Name)
                    .Take(count)
                    .ToListAsync();
            }

            await EnsureRecipeImagesAsync(seasonalRecipes);

            return seasonalRecipes
                .Select(r => MapToListDto(r, favoriteIds))
                .ToList();
        }

        var order = ids
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        var recipes = await _db.Recipes
            .Where(r => ids.Contains(r.Id))
            .ToListAsync();

        await EnsureRecipeImagesAsync(recipes);

        return recipes
            .OrderBy(r => order.GetValueOrDefault(r.Id, int.MaxValue))
            .Select(r => MapToListDto(r, favoriteIds))
            .ToList();
    }


    public async Task<List<RecipeListDto>>
        GetSuggestedForUserAsync(
            string userId,
            int count)
    {
        count = NormalizeRecipeListCount(count);

        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var dietPrefs = await _db.UserDietPreferences
            .Where(p => p.UserId == userId)
            .Select(p => p.DietName.ToLower())
            .ToListAsync();

        var allergens = await _db.UserAllergenPreferences
            .Where(p => p.UserId == userId)
            .Select(p => p.AllergenName.ToLower())
            .ToListAsync();

        var ingredientActivity = await _db.UserIngredientActivities
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.IngredientName.ToLower())
            .Take(20)
            .ToListAsync();

        var searchTerms = await _db.UserSearchHistories
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SearchedAt)
            .Select(s => s.SearchTerm.ToLower())
            .Take(20)
            .ToListAsync();

        var favoriteIngredients = await _db.Favorites
            .Where(f => f.UserId == userId)
            .SelectMany(f => f.Recipe.RecipeIngredients)
            .Select(ri => ri.Ingredient.Name.ToLower())
            .Take(30)
            .ToListAsync();

        var query = _db.Recipes
            .Include(r => r.RecipeIngredients)
                .ThenInclude(ri => ri.Ingredient)
            .Include(r => r.RecipeTags)
                .ThenInclude(rt => rt.Tag)
            .Include(r => r.DietClassifications)
            .Include(r => r.AllergenClassifications)
            .AsQueryable();

        query = ApplyAllergenExclusions(query, allergens);

        var candidates = await query
            .Take(300)
            .ToListAsync();

        var scored = candidates
            .Select(r =>
            {
                var text = $"{r.Name} {r.Description}".ToLowerInvariant();
                var ingredients = r.RecipeIngredients
                    .Select(i => i.Ingredient.Name.ToLowerInvariant())
                    .ToList();

                var tags = r.RecipeTags
                    .Select(t => t.Tag.Name.ToLowerInvariant())
                    .ToList();

                var score = 0;
                score += ingredients.Count(i =>
                    ingredientActivity.Any(a => i.Contains(a) || a.Contains(i))) * 5;
                score += ingredients.Count(i =>
                    favoriteIngredients.Any(f => i.Contains(f) || f.Contains(i))) * 4;
                score += searchTerms.Count(s => text.Contains(s)) * 3;
                score += dietPrefs.Count(d =>
                    r.DietClassifications.Any(c => c.DietName.ToLower().Contains(d)) ||
                    tags.Any(t => t.Contains(d))) * 2;

                return new { Recipe = r, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Recipe.Minutes)
            .Take(count)
            .Select(x => x.Recipe)
            .ToList();

        if (scored.Count < count)
        {
            var existingIds = scored.Select(r => r.Id).ToHashSet();
            var extra = await _db.Recipes
                .Where(r => !existingIds.Contains(r.Id))
                .OrderBy(r => r.Minutes)
                .ThenBy(r => r.Name)
                .Take(count - scored.Count)
                .ToListAsync();

            scored.AddRange(extra);
        }

        await EnsureRecipeImagesAsync(scored);

        return scored
            .Select(r => MapToListDto(r, favoriteIds))
            .ToList();
    }
    public async Task<PagedResult<RecipeListDto>>
    GetRecipesByCategoryValueAsync(
        string category,
        string value,
        int page,
        int pageSize,
        string? userId)
    {
        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var p =
            NormalizePage(page);

        var ps =
            NormalizeRecipeListPageSize(pageSize);

        IQueryable<Recipe> query =
            _db.Recipes.AsQueryable();

        switch (category.ToLower())
        {
            // ================= SKILL LEVEL =================

            case "skill level":

                query = _db.Recipes.Where(r =>
                    r.RecipeTags.Any(t =>
                        t.Tag.Name.ToLower()
                            .Contains(value.ToLower()))

                    ||

                    r.Name.ToLower()
                        .Contains(value.ToLower()));

                break;


            // ================= RECIPE TIME =================

            case "recipe time":

                query = value switch
                {
                    "5-10 min" =>
                        _db.Recipes.Where(r =>
                            r.Minutes >= 5 &&
                            r.Minutes < 10),

                    "<10" =>
                        _db.Recipes.Where(r =>
                            r.Minutes < 10),

                    "10-20 min" or "10-20" =>
                        _db.Recipes.Where(r =>
                            r.Minutes >= 10 &&
                            r.Minutes < 20),

                    "20-30 min" or "20-30" =>
                        _db.Recipes.Where(r =>
                            r.Minutes >= 20 &&
                            r.Minutes < 30),

                    "30-45 min" =>
                        _db.Recipes.Where(r =>
                            r.Minutes >= 30 &&
                            r.Minutes < 45),

                    "45-60 min" =>
                        _db.Recipes.Where(r =>
                            r.Minutes >= 45 &&
                            r.Minutes <= 60),

                    "30-60" =>
                        _db.Recipes.Where(r =>
                            r.Minutes >= 30 &&
                            r.Minutes < 60),

                    "> 1 hr" or ">1 hr" or ">60" =>
                        _db.Recipes.Where(r =>
                            r.Minutes > 60),

                    _ =>
                        _db.Recipes.Where(r => false)
                };

                break;


            // ================= DIET =================

            case "diet":

                query = _db.Recipes.Where(r =>

                    r.RecipeTags.Any(t =>
                        t.Tag.Name.ToLower()
                            .Contains(value.ToLower()))

                    ||

                    r.Name.ToLower()
                        .Contains(value.ToLower())

                    ||

                    (r.Description != null &&
                     r.Description.ToLower()
                        .Contains(value.ToLower()))
                );

                break;


            // ================= CUISINE =================

            case "cuisine":

                query = _db.Recipes.Where(r =>

                    r.RecipeTags.Any(t =>
                        t.Tag.Name.ToLower()
                            .Contains(value.ToLower()))

                    ||

                    r.Name.ToLower()
                        .Contains(value.ToLower())

                    ||

                    (r.Description != null &&
                     r.Description.ToLower()
                        .Contains(value.ToLower()))
                );

                break;


            // ================= COURSES =================

            case "courses":

                query = _db.Recipes.Where(r =>

                    r.RecipeTags.Any(t =>
                        t.Tag.Name.ToLower()
                            .Contains(value.ToLower()))

                    ||

                    r.Name.ToLower()
                        .Contains(value.ToLower()));

                break;


            // ================= HEALTHY EATING =================

            case "healthy eating":

                query = _db.Recipes.Where(r =>

                    r.RecipeTags.Any(t =>
                        t.Tag.Name.ToLower()
                            .Contains(value.ToLower()))

                    ||

                    (r.Description != null &&
                     r.Description.ToLower()
                        .Contains(value.ToLower()))
                );

                break;


            // ================= SMART APPLIANCES =================

            case "smart appliances":

                query = _db.Recipes.Where(r =>

                    r.Name.ToLower()
                        .Contains(value.ToLower())

                    ||

                    r.RecipeTags.Any(t =>
                        t.Tag.Name.ToLower()
                            .Contains(value.ToLower()))
                );

                break;


            // ================= BEVERAGES =================

            case "beverages":

                query = _db.Recipes.Where(r =>

                    r.Name.ToLower()
                        .Contains(value.ToLower())

                    ||

                    r.RecipeTags.Any(t =>
                        t.Tag.Name.ToLower()
                            .Contains(value.ToLower()))
                );

                break;


            // ================= SEASONAL =================

            case "seasonal":

                query = _db.Recipes.Where(r =>

                    r.Name.ToLower()
                        .Contains(value.ToLower())

                    ||

                    (r.Description != null &&
                     r.Description.ToLower()
                        .Contains(value.ToLower()))

                    ||

                    r.RecipeTags.Any(t =>
                        t.Tag.Name.ToLower()
                            .Contains(value.ToLower()))
                );

                break;


            // ================= SOUPS & STEWS =================

            case "soups & stews":

                query = _db.Recipes.Where(r =>

                    r.Name.ToLower()
                        .Contains(value.ToLower())

                    ||

                    r.RecipeTags.Any(t =>
                        t.Tag.Name.ToLower()
                            .Contains(value.ToLower()))
                );

                break;

            default:

                query = _db.Recipes.Where(r => false);

                break;
        }

        var total =
            await query.CountAsync();

        var items = await query
            .OrderBy(r => r.Id)
            .Skip((p - 1) * ps)
            .Take(ps)
            .ToListAsync();

        await EnsureRecipeImagesAsync(items);

        return new PagedResult<RecipeListDto>
        {
            Items = items
                .Select(r => MapToListDto(r, favoriteIds))
                .ToList(),

            TotalCount = total,

            Page = p,

            PageSize = ps
        };
    }
    private static string GetDifficulty(
    Recipe recipe)
    {
        if (recipe.Minutes > 90)
            return "Advanced";

        if (recipe.Minutes > 45)
            return "Medium";

        if (recipe.NSteps <= 8 &&
            recipe.NIngredients <= 8)
            return "Easy";

        if (recipe.NSteps <= 15 &&
            recipe.NIngredients <= 12)
            return "Medium";

        return "Advanced";
    }

    private static string GetMainIngredient(
        Recipe recipe)
    {
        var ingredients =
            recipe.RecipeIngredients
                .Select(i =>
                    i.Ingredient.Name.ToLower())
                .ToList();

        string[] priority =
        {
        "chicken",
        "beef",
        "fish",
        "salmon",
        "shrimp",
        "lamb",
        "egg",
        "tofu",
        "potato",
        "tomato",
        "onion"
    };

        foreach (var item in priority)
        {
            if (ingredients.Any(x =>
                    x.Contains(item)))
            {
                return item;
            }
        }

        return ingredients.FirstOrDefault()
               ?? string.Empty;
    }

    private static List<string> GetSubstitutes(
        Recipe recipe)
    {
        var substitutes =
            new Dictionary<string, List<string>>
            {
                ["chicken"] =
                    new()
                    {
                    "turkey",
                    "tofu"
                    },

                ["beef"] =
                    new()
                    {
                    "lamb",
                    "mushroom"
                    },

                ["milk"] =
                    new()
                    {
                    "almond milk",
                    "soy milk"
                    },

                ["egg"] =
                    new()
                    {
                    "banana",
                    "flaxseed"
                    }
            };

        var ingredients =
            recipe.RecipeIngredients
                .Select(i =>
                    i.Ingredient.Name.ToLower())
                .ToList();

        var result =
            new List<string>();

        foreach (var ingredient in ingredients)
        {
            if (substitutes.TryGetValue(
                    ingredient,
                    out var values))
            {
                result.AddRange(values);
            }
        }

        return result
            .Distinct()
            .ToList();
    }
    private static string GetSeasonFromRecipe(Recipe recipe)
    {
        var text =
            $"{recipe.Name} {recipe.Description}"
            .ToLower();
        if (text.Contains("summer"))
            return "Summer";

        if (text.Contains("winter"))
            return "Winter";

        if (text.Contains("spring"))
            return "Spring";

        if (text.Contains("autumn"))
            return "Autumn";

        return GetSeason();
    }
    public string GetCookingTimeRange(int minutes)
    {
        if (minutes < 15)
            return "Quick";

        if (minutes < 30)
            return "Medium";

        if (minutes < 60)
            return "Long";

        return "Very Long";
    }
  
private static Dictionary<string, List<string>>
GetSubstituteMap(
    List<string> ingredients)
    {
        var substitutes =
            new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase)
            {
                ["chicken"] = new() { "Turkey", "Tofu" },
                ["beef"] = new() { "Lamb", "Mushroom" },
                ["fish"] = new() { "Tofu", "Jackfruit" },
                ["seafood"] = new() { "Mushroom", "Tofu" },
                ["milk"] = new() { "Almond Milk", "Soy Milk" },
                ["egg"] = new() { "Banana", "Flaxseed" },
                ["butter"] = new() { "Olive Oil", "Ghee" },
                ["rice"] = new() { "Quinoa", "Bulgur" },
                ["onion"] = new() { "Shallot", "Leek" },
                ["garlic"] = new() { "Garlic Powder", "Asafoetida" },
                ["tomato"] = new() { "Tomato Paste", "Red Pepper" },
                ["flour"] = new() { "Almond Flour", "Oat Flour" },
                ["cheese"] = new() { "Nutritional Yeast", "Vegan Cheese" },
                ["cream"] = new() { "Coconut Cream", "Cashew Cream" },
                ["sugar"] = new() { "Honey", "Maple Syrup" }
            };

        var result =
            new Dictionary<string, List<string>>();

        foreach (var ingredient in ingredients)
        {
            var normalized =
                NormalizeIngredient(ingredient);

            foreach (var item in substitutes)
            {
                if (normalized.Contains(item.Key))
                {
                    result[ingredient] = item.Value;
                    break;
                }
            }
        }
        return result;
    }

    public async Task<List<RecipeListDto>> GetRecipesByDietAsync(
        string diet,
        string? userId)
    {
        var filter = new RecipeFilterRequest
        {
            Diets = new List<string> { NormalizeDietName(diet) },
            Page = 1,
            PageSize = RecipeListPageSize
        };

        var result = await GetRecipesAsync(filter, userId);
        return result.Items;
    }

    public async Task<List<RecipeListDto>> GetRecipesByAllergenAsync(
        string allergen,
        string? userId)
    {
        var favoriteIds = await GetUserFavoriteIdsAsync(userId);
        var normalized = NormalizeIngredient(allergen);

        var query = _db.Recipes
            .Where(r =>
                r.AllergenClassifications.Any(a =>
                    a.AllergenName.ToLower().Contains(normalized)) ||
                r.RecipeIngredients.Any(i =>
                    i.Ingredient.Name.ToLower().Contains(normalized)) ||
                r.RecipeTags.Any(t =>
                    t.Tag.Name.ToLower().Contains(normalized)));

        var recipes = await query
            .OrderBy(r => r.Id)
            .Take(RecipeListPageSize)
            .ToListAsync();

        await EnsureRecipeImagesAsync(recipes);

        return recipes
            .Select(r => MapToListDto(r, favoriteIds))
            .ToList();
    }
    public async Task<List<RecipeListDto>> FilterRecipesAsync(
        string? diet,
        string? allergen,
        string? userId)
    {
        var favoriteIds =
            await GetUserFavoriteIdsAsync(userId);

        var query =
            _db.Recipes
                .Include(r => r.RecipeTags)
                    .ThenInclude(rt => rt.Tag)
                .Include(r => r.RecipeIngredients)
                    .ThenInclude(ri => ri.Ingredient)
                .AsQueryable();

        if (!string.IsNullOrWhiteSpace(diet))
        {
            var normalizedDiet =
                diet.Trim().ToLower();

            query = query.Where(r =>
                r.RecipeTags.Any(t =>
                    t.Tag.Name.ToLower()
                        .Contains(normalizedDiet)));
        }

        if (!string.IsNullOrWhiteSpace(allergen))
        {
            var normalizedAllergen =
                NormalizeIngredient(allergen);

            query = query.Where(r =>
                r.RecipeIngredients.Any(i =>
                    i.Ingredient.Name.ToLower()
                        .Contains(normalizedAllergen))
                ||
                r.RecipeTags.Any(t =>
                    t.Tag.Name.ToLower()
                        .Contains(normalizedAllergen)));
        }

        var recipes = await query
            .OrderBy(r => r.Id)
            .Take(RecipeListPageSize)
            .ToListAsync();

        await EnsureRecipeImagesAsync(recipes);

        return recipes
            .Select(r => MapToListDto(r, favoriteIds))
            .ToList();
    }
    public async Task<List<RecipeListDto>> GetSafeRecipesForUserAsync(
        string userId)
    {
        var allergens = await _db.UserAllergenPreferences
            .Where(p => p.UserId == userId)
            .Select(p => p.AllergenName)
            .ToListAsync();

        var favoriteIds = await GetUserFavoriteIdsAsync(userId);
        var query = ApplyAllergenExclusions(_db.Recipes.AsQueryable(), allergens);

        var recipes = await query
            .OrderBy(r => r.Minutes)
            .Take(RecipeListPageSize)
            .ToListAsync();

        await EnsureRecipeImagesAsync(recipes);

        return recipes
            .Select(r => MapToListDto(r, favoriteIds))
            .ToList();
    }

    public Task<FilterOptionsDto> GetFilterOptionsAsync()
    {
        return Task.FromResult(new FilterOptionsDto
        {
            SkillLevels = new List<string> { "Easy", "Medium", "Advanced" },
            Diets = new List<string>
            {
                "Veg",
                "Non-Veg",
                "Vegan",
                "Keto",
                "Dairy-Free",
                "Gluten-Free",
                "Allergen"
            },
            Meals = new List<string>
            {
                "Appetiser",
                "Breakfast",
                "Lunch",
                "Dinner",
                "Snack",
                "Brunch",
                "Main Course",
                "Dessert"
            },
            Time = new List<string>
            {
                "5-10 min",
                "10-20 min",
                "20-30 min",
                "30-45 min",
                "45-60 min",
                "> 1 hr"
            }
        });
    }

    public async Task<List<string>> SearchIngredientsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<string>();
        }

        var normalized = query.Trim().ToLower();

        return await _db.Ingredients
            .Where(i =>
                i.Name.ToLower().Contains(normalized) ||
                (i.DisplayName != null &&
                 i.DisplayName.ToLower().Contains(normalized)))
            .OrderBy(i => i.Name.ToLower().StartsWith(normalized) ? 0 : 1)
            .ThenBy(i => i.Name)
            .Select(i => i.DisplayName ?? i.Name)
            .Distinct()
            .Take(20)
            .ToListAsync();
    }

    public async Task<List<string>> GetRecentIngredientsAsync(
        string? userId,
        int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return new List<string>();
        }

        var recentRaw = await _db.UserIngredientActivities
            .Where(a =>
                a.UserId == userId &&
                !string.IsNullOrWhiteSpace(a.IngredientName))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.IngredientName)
            .ToListAsync();

        return recentRaw
            .Select(ToDisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    public async Task<List<IngredientListItemDto>> GetPopularIngredientsAsync(int limit = 100)
    {
        var requestedNames = DefaultPopularIngredients
            .Take(Math.Max(1, limit))
            .ToList();

        var requestedNameKeys = requestedNames
            .Select(n => n.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        var ingredients = await _db.Ingredients
            .Where(i =>
                requestedNameKeys.Contains(i.Name.ToLower()) ||
                (i.DisplayName != null &&
                 requestedNameKeys.Contains(i.DisplayName.ToLower())))
            .ToListAsync();

        await EnsureIngredientImagesAsync(
            ingredients.Select(i => new RecipeIngredient { Ingredient = i }));

        return requestedNames
            .Select(name =>
            {
                var match = ingredients.FirstOrDefault(i =>
                    string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(i.DisplayName, name, StringComparison.OrdinalIgnoreCase) ||
                    NormalizeIngredient(i.Name) == NormalizeIngredient(name) ||
                    NormalizeIngredient(i.DisplayName ?? string.Empty) == NormalizeIngredient(name));

                return new IngredientListItemDto
                {
                    Name = name,
                    ImageUrl = BuildIngredientImageUrl(match?.ImageUrl)
                };
            })
            .ToList();
    }

    public async Task<List<IngredientListItemDto>> SearchIngredientsDetailedAsync(
        string query,
        int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<IngredientListItemDto>();
        }

        var normalized = query.Trim().ToLowerInvariant();

        var ingredients = await _db.Ingredients
            .Where(i =>
                i.Name.ToLower().Contains(normalized) ||
                (i.DisplayName != null &&
                 i.DisplayName.ToLower().Contains(normalized)))
            .OrderBy(i => i.Name.ToLower().StartsWith(normalized) ? 0 : 1)
            .ThenBy(i => i.Name)
            .Take(limit)
            .ToListAsync();

        await EnsureIngredientImagesAsync(
            ingredients.Select(i => new RecipeIngredient { Ingredient = i }));

        return ingredients
            .Select(i => new IngredientListItemDto
            {
                Name = i.DisplayName ?? i.Name,
                ImageUrl = BuildIngredientImageUrl(i.ImageUrl)
            })
            .ToList();
    }

    private static string NormalizeDietName(string diet)
    {
        return diet.Trim().ToLowerInvariant() switch
        {
            "veg" => "Vegetarian",
            "non-veg" => "Non-Vegetarian",
            "low-carb" => "Low-Carb",
            _ => diet.Trim()
        };
    }


}
