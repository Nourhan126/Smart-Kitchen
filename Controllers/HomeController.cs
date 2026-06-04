using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/home")]
[Produces("application/json")]
public class HomeController : ControllerBase
{
    private readonly IRecipeService _recipeService;

    public HomeController(IRecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    [HttpGet("trending")]
    [ProducesResponseType(typeof(ApiResponse<List<RecipeListDto>>), 200)]
    public async Task<IActionResult> GetTrending([FromQuery] int count = 4)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var recipes = await _recipeService.GetTrendingAsync(userId, count);
        return Ok(ApiResponse<List<RecipeListDto>>.Success("Trending recipes retrieved.", recipes));
    }

    [HttpGet("recommended")]
    [ProducesResponseType(typeof(ApiResponse<List<RecipeListDto>>), 200)]
    public async Task<IActionResult> GetRecommended([FromQuery] int count = 4)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var recipes = await _recipeService.GetRecommendedAsync(userId, count);
        return Ok(ApiResponse<List<RecipeListDto>>.Success("Recommended recipes retrieved.", recipes));
    }

    [HttpGet("suggested")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<RecipeListDto>>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetSuggested([FromQuery] int count = 4)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var recipes = await _recipeService.GetSuggestedForUserAsync(userId, count);
        return Ok(ApiResponse<List<RecipeListDto>>.Success("Suggested recipes retrieved.", recipes));
    }

    [HttpGet("seasonal")]
    [ProducesResponseType(typeof(ApiResponse<List<RecipeListDto>>), 200)]
    public async Task<IActionResult> GetSeasonal([FromQuery] int count = 4)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var recipes = await _recipeService.GetSeasonalAsync(userId, count);
        return Ok(ApiResponse<List<RecipeListDto>>.Success("Seasonal recipes retrieved.", recipes));
    }
}
