using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/recipes")]
[Produces("application/json")]
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _recipeService;

    public RecipesController(
        IRecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResult<RecipeListDto>>),
        200)]
    public async Task<IActionResult> GetRecipes(
        [FromQuery] RecipeFilterRequest filter)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var result =
            await _recipeService.GetRecipesAsync(
                filter,
                userId);

        return Ok(
            ApiResponse<PagedResult<RecipeListDto>>
            .Success(
                "Recipes retrieved.",
                result));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(
        typeof(ApiResponse<RecipeDetailDto>),
        200)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        404)]
    public async Task<IActionResult> GetRecipe(
        int id,
        [FromQuery] List<string>? ingredients = null)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var recipe =
            await _recipeService.GetRecipeByIdAsync(
                id,
                userId,
                ingredients);

        if (recipe is null)
        {
            return NotFound(
                ApiResponse<object>.Fail(
                    "Recipe not found."));
        }

        return Ok(
            ApiResponse<RecipeDetailDto>
            .Success(
                "Recipe retrieved.",
                recipe));
    }

    [HttpPost("filter")]
    [ProducesResponseType(
        typeof(ApiResponse<PagedResult<RecipeListDto>>),
        200)]
    public async Task<IActionResult> FilterRecipes(
        [FromBody] RecipeFilterRequest filter)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var result =
            await _recipeService.GetRecipesAsync(
                filter,
                userId);

        return Ok(
            ApiResponse<PagedResult<RecipeListDto>>
                .Success(
                    "Filtered recipes retrieved.",
                    result));
    }

    [HttpGet("{id:int}/steps")]
    [ProducesResponseType(
        typeof(ApiResponse<RecipeStepsResponseDto>),
        200)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        404)]
    public async Task<IActionResult> GetSteps(
        int id)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var recipe =
            await _recipeService.GetRecipeByIdAsync(
                id,
                userId);

        if (recipe is null)
        {
            return NotFound(
                ApiResponse<object>.Fail(
                    "Recipe not found."));
        }

        var response =
            new RecipeStepsResponseDto
            {
               
                Steps = recipe.Steps
            };

        return Ok(
            ApiResponse<RecipeStepsResponseDto>
            .Success(
                "Steps retrieved.",
                response));
    }

    [HttpGet("trending")]
    [ProducesResponseType(
        typeof(ApiResponse<List<RecipeListDto>>),
        200)]
    public async Task<IActionResult> GetTrending(
        [FromQuery] int count = 7)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var recipes =
            await _recipeService.GetTrendingAsync(
                userId,
                count);

        return Ok(
            ApiResponse<List<RecipeListDto>>
            .Success(
                "Trending recipes retrieved.",
                recipes));
    }

    [HttpGet("recommended")]
    [ProducesResponseType(
        typeof(ApiResponse<List<RecipeListDto>>),
        200)]
    public async Task<IActionResult> GetRecommended(
        [FromQuery] int count = 7)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var recipes =
            await _recipeService.GetRecommendedAsync(
                userId,
                count);

        return Ok(
            ApiResponse<List<RecipeListDto>>
            .Success(
                "Recommended recipes retrieved.",
                recipes));
    }

    [HttpGet("seasonal")]
    [ProducesResponseType(
        typeof(ApiResponse<List<RecipeListDto>>),
        200)]
    public async Task<IActionResult> GetSeasonal(
        [FromQuery] int count = 7)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var recipes =
            await _recipeService.GetSeasonalAsync(
                userId,
                count);

        return Ok(
            ApiResponse<List<RecipeListDto>>
            .Success(
                "Seasonal recipes retrieved.",
                recipes));
    }

    [HttpPost("by-ingredients")]
    [ProducesResponseType(
        typeof(ApiResponse<List<RecipeMatchDto>>),
        200)]
    public async Task<IActionResult> GetByIngredients(
        [FromBody] IngredientSearchRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var result =
            await _recipeService.GetByIngredientsAsync(
                request,
                userId);

        return Ok(
            ApiResponse<List<RecipeMatchDto>>
            .Success(
                "Recipes by ingredients retrieved.",
                result));
    }

    [HttpGet("filter-categories")]
    [ProducesResponseType(
        typeof(ApiResponse<List<TagCategoryDto>>),
        200)]
    public async Task<IActionResult>
        GetFilterCategories()
    {
        var categories =
            await _recipeService
                .GetFilterCategoriesAsync();

        return Ok(
            ApiResponse<List<TagCategoryDto>>
            .Success(
                "Filter categories retrieved.",
                categories));
    }

    [HttpGet("ingredients-metadata")]
    [ProducesResponseType(
        typeof(ApiResponse<IngredientMetadataDto>),
        200)]
    public async Task<IActionResult> GetIngredientsMetadata()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var metadata =
            await _recipeService
                .GetIngredientsMetadataAsync(userId);

        return Ok(
            ApiResponse<IngredientMetadataDto>
                .Success(
                    "Ingredient metadata retrieved.",
                    metadata));
    }

    [HttpPost("ingredient-activity")]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        200)]
    public async Task<IActionResult> SaveIngredientActivity(
        [FromBody] IngredientActivityRequest request)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        await _recipeService
            .SaveIngredientActivityAsync(
                userId,
                request);

        return Ok(
            ApiResponse<object>
                .Success(
                    "Ingredient activity saved."));
    }

    [HttpGet("diet-allergen-filter")]
    [ProducesResponseType(
    typeof(ApiResponse<List<RecipeListDto>>),
    200)]
    public async Task<IActionResult> FilterByDietAndAllergen(
    [FromQuery] string? diet,
    [FromQuery] string? allergen)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var recipes =
            await _recipeService.FilterRecipesAsync(
                diet,
                allergen,
                userId);

        return Ok(
            ApiResponse<List<RecipeListDto>>
            .Success(
                "Recipes retrieved.",
                recipes));
    }

    [HttpGet("safe-for-user")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ProducesResponseType(
        typeof(ApiResponse<List<RecipeListDto>>),
        200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetSafeForUser()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier)!;

        var recipes =
            await _recipeService.GetSafeRecipesForUserAsync(
                userId);

        return Ok(
            ApiResponse<List<RecipeListDto>>
            .Success(
                "Safe recipes retrieved.",
                recipes));
    }

    [HttpGet("filter-options")]
    [ProducesResponseType(
        typeof(ApiResponse<FilterOptionsDto>),
        200)]
    public async Task<IActionResult> GetFilterOptions()
    {
        var options =
            await _recipeService.GetFilterOptionsAsync();

        return Ok(
            ApiResponse<FilterOptionsDto>
            .Success(
                "Filter options retrieved.",
                options));
    }
}
