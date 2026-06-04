
using SmartKitchen.API.DTOs;

namespace SmartKitchen.API.Services;

public interface IRecipeService
{
    Task<PagedResult<RecipeListDto>> GetRecipesAsync(
        RecipeFilterRequest filter,
        string? userId);

    Task<RecipeDetailDto?> GetRecipeByIdAsync(
        int id,
        string? userId,
        List<string>? selectedIngredients = null);

    Task<List<RecipeListDto>> GetTrendingAsync(
        string? userId,
        int count = 7);

    Task<List<RecipeListDto>> GetRecommendedAsync(
        string? userId,
        int count = 7);

    Task<List<RecipeListDto>> GetSuggestedForUserAsync(
        string userId,
        int count = 7);

    Task<List<RecipeListDto>> GetSeasonalAsync(
        string? userId,
        int count = 7);

    Task<List<RecipeMatchDto>> GetByIngredientsAsync(
        IngredientSearchRequest request,
        string? userId);

    Task<IngredientMetadataDto> GetIngredientsMetadataAsync(
        string? userId,
        int recentLimit = 2,
        int popularLimit = 11);

    Task SaveIngredientActivityAsync(
        string? userId,
        IngredientActivityRequest request);

    Task<List<TagCategoryDto>>
        GetFilterCategoriesAsync();

    Task<PagedResult<RecipeListDto>>
        GetByCategoryAsync(
            string tagName,
            int page,
            int pageSize,
            string? userId);

    Task<PagedResult<RecipeListDto>>
        GetRecipesByCategoryValueAsync(
            string category,
            string value,
            int page,
            int pageSize,
            string? userId);

    Task<List<CuisineDto>>
        GetCuisinesAsync();

    Task<PagedResult<RecipeListDto>>
        GetByCuisineAsync(
            string cuisine,
            int page,
            int pageSize,
            string? userId);

    Task<PagedResult<RecipeListDto>>
        GetByCuisineSubcategoryAsync(
            string cuisine,
            string subcategory,
            int page,
            int pageSize,
            string? userId);

    string GetCookingTimeRange(
        int minutes);

    Task<List<RecipeListDto>> GetRecipesByDietAsync(string diet, string? userId);
    Task<List<RecipeListDto>> GetRecipesByAllergenAsync(string allergen, string? userId);
    Task<List<RecipeListDto>> FilterRecipesAsync(
    string? diet,
    string? allergen,
    string? userId);
    Task<List<RecipeListDto>> GetSafeRecipesForUserAsync(string userId);
    Task<FilterOptionsDto> GetFilterOptionsAsync();
    Task<List<string>> SearchIngredientsAsync(string query);
    Task<List<string>> GetRecentIngredientsAsync(string? userId, int limit = 10);
    Task<List<IngredientListItemDto>> GetPopularIngredientsAsync(int limit = 100);
    Task<List<IngredientListItemDto>> SearchIngredientsDetailedAsync(string query, int limit = 20);
}
