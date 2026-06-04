using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/cuisines")]
[Produces("application/json")]
public class CuisinesController : ControllerBase
{
    private readonly IRecipeService _recipeService;

    public CuisinesController(IRecipeService recipeService)
    {
        _recipeService = recipeService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CuisineDto>>), 200)]
    public async Task<IActionResult> GetCuisines()
    {
        var result = await _recipeService.GetCuisinesAsync();
        return Ok(ApiResponse<List<CuisineDto>>.Success("Cuisines retrieved.", result));
    }

    [HttpGet("{cuisine}/recipes")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RecipeListDto>>), 200)]
    public async Task<IActionResult> GetCuisineRecipes(
        string cuisine,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 7)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _recipeService.GetByCuisineAsync(cuisine, page, pageSize, userId);
        return Ok(ApiResponse<PagedResult<RecipeListDto>>.Success("Cuisine recipes retrieved.", result));
    }

    [HttpGet("{cuisine}/{subcategory}/recipes")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RecipeListDto>>), 200)]
    public async Task<IActionResult> GetCuisineSubcategoryRecipes(
        string cuisine,
        string subcategory,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 7)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _recipeService.GetByCuisineSubcategoryAsync(cuisine, subcategory, page, pageSize, userId);
        return Ok(ApiResponse<PagedResult<RecipeListDto>>.Success("Cuisine subcategory recipes retrieved.", result));
    }
}
