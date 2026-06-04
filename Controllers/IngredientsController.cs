using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/ingredients")]
[Produces("application/json")]
public class IngredientsController : ControllerBase
{
    private readonly IRecipeService _recipeService;

    public IngredientsController(
        IRecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    [HttpGet("recent")]
    [ProducesResponseType(
        typeof(ApiResponse<List<string>>),
        200)]
    public async Task<IActionResult> GetRecent()
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var recent =
            await _recipeService
                .GetRecentIngredientsAsync(userId);

        return Ok(
            ApiResponse<List<string>>
                .Success(
                    "Recent ingredients retrieved.",
                    recent));
    }

    [HttpGet("popular")]
    [ProducesResponseType(
        typeof(ApiResponse<List<IngredientListItemDto>>),
        200)]
    public async Task<IActionResult> GetPopular()
    {
        var popular =
            await _recipeService
                .GetPopularIngredientsAsync();

        return Ok(
            ApiResponse<List<IngredientListItemDto>>
                .Success(
                    "Popular ingredients retrieved.",
                    popular));
    }

    [HttpGet("search")]
    [ProducesResponseType(
        typeof(ApiResponse<List<IngredientListItemDto>>),
        200)]
    public async Task<IActionResult> Search(
        [FromQuery] string q)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(q))
        {
            await _recipeService.SaveIngredientActivityAsync(
                userId,
                new IngredientActivityRequest
                {
                    Ingredients = new List<string> { q },
                    ActivityType = "search"
                });
        }

        var results =
            await _recipeService
                .SearchIngredientsDetailedAsync(q);

        return Ok(
            ApiResponse<List<IngredientListItemDto>>
                .Success(
                    "Ingredients found.",
                    results));
    }
}
