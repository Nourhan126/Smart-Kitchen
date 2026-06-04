using System;
using Microsoft.EntityFrameworkCore;
using SmartKitchen.API.Data;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Models;

namespace SmartKitchen.API.Services;

public class FavoriteService : IFavoriteService
{
    private readonly ApplicationDbContext _db;
    private readonly IRecipeService _recipeService;
    private readonly IImageSearchService _imageService;
    private readonly IImageUrlBuilder _imageUrlBuilder;

    public FavoriteService(
        ApplicationDbContext db,
        IRecipeService recipeService,
        IImageSearchService imageService,
        IImageUrlBuilder imageUrlBuilder)
    {
        _db = db;
        _recipeService = recipeService;
        _imageService = imageService;
        _imageUrlBuilder = imageUrlBuilder;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Add favorite
    // ─────────────────────────────────────────────────────────────────────────

    private string? BuildImageUrl(Recipe recipe) =>
        _imageUrlBuilder.NormalizeImageUrl(recipe.ImageUrl, "recipe");

    public async Task<bool> AddFavoriteAsync(
        string userId,
        int recipeId)
    {
        var exists = await _db.Favorites
            .AnyAsync(f =>
                f.UserId == userId &&
                f.RecipeId == recipeId);

        if (exists)
            return false;

        var recipeExists = await _db.Recipes
            .AnyAsync(r => r.Id == recipeId);

        if (!recipeExists)
            return false;

        _db.Favorites.Add(new Favorite
        {
            UserId = userId,
            RecipeId = recipeId
        });

        await _db.SaveChangesAsync();

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Remove favorite
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<bool> RemoveFavoriteAsync(
        string userId,
        int recipeId)
    {
        var fav = await _db.Favorites
            .FirstOrDefaultAsync(f =>
                f.UserId == userId &&
                f.RecipeId == recipeId);

        if (fav is null)
            return false;

        _db.Favorites.Remove(fav);

        await _db.SaveChangesAsync();

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Get favorites
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<PagedResult<RecipeListDto>> GetFavoritesAsync(
        string userId,
        int page,
        int pageSize)
    {
        var p = Math.Max(1, page);
        var ps = 7;

        var query = _db.Favorites
            .Where(f => f.UserId == userId)
            .Include(f => f.Recipe)
            .OrderByDescending(f => f.CreatedAt);

        var total = await query.CountAsync();

        var recipes = await query
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(f => f.Recipe)
            .ToListAsync();

        // Generate missing images
        foreach (var recipe in recipes)
        {
            if (BuildImageUrl(recipe) is null)
            {
                try
                {
                    var result = await _imageService
                        .SearchImageAsync(recipe.Name, targetType: "recipe");

                    if (result != null &&
                        result.Success &&
                        !string.IsNullOrWhiteSpace(result.ImagePath))
                    {
                        recipe.ImageUrl =
                            _imageUrlBuilder.BuildPublicImageUrl(result);
                    }
                }
                catch
                {
                    // Ignore image failures
                }
            }
        }

        // Save generated image URLs
        await _db.SaveChangesAsync();

        return new PagedResult<RecipeListDto>
        {
            
Items = recipes.Select(r => new RecipeListDto
{
    Id = r.Id,

    Name = r.Name,

    ImageUrl = BuildImageUrl(r),

    Calories = $"{r.Calories:0.#} kcal",
    Minutes = $"{r.Minutes} min",

    Difficulty = GetDifficulty(r),

    Season = GetSeasonFromRecipe(r),

    IsFavorite = true

}).ToList(),


            TotalCount = total,

            Page = p,

            PageSize = ps
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Check favorite
    // ─────────────────────────────────────────────────────────────────────────

    private static string GetDifficulty(Recipe recipe)
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

    private static string GetSeasonFromRecipe(Recipe recipe)
    {
        var text = $"{recipe.Name} {recipe.Description}".ToLowerInvariant();

        if (text.Contains("summer"))
            return "Summer";

        if (text.Contains("winter"))
            return "Winter";

        if (text.Contains("spring"))
            return "Spring";

        if (text.Contains("autumn"))
            return "Autumn";

        return DateTime.Now.Month switch
        {
            12 or 1 or 2 => "Winter",
            3 or 4 or 5 => "Spring",
            6 or 7 or 8 => "Summer",
            _ => "Autumn"
        };
    }

    public async Task<bool> IsFavoriteAsync(
        string userId,
        int recipeId)
    {
        return await _db.Favorites
            .AnyAsync(f =>
                f.UserId == userId &&
                f.RecipeId == recipeId);
    }
}
