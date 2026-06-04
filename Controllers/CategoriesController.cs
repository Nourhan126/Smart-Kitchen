using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IRecipeService _recipeService;

    public CategoriesController(IRecipeService recipeService)
    {
        _recipeService = recipeService;
    }


    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<TagCategoryDto>>), 200)]
    public async Task<IActionResult> GetCategories()
    {
        var categories =
            await _recipeService.GetFilterCategoriesAsync();

       
        categories = categories
            .Where(x =>
                x.Values != null &&
                x.Values.Any())
            .ToList();

        return Ok(
            ApiResponse<List<TagCategoryDto>>.Success(
                "Categories retrieved.",
                categories
            )
        );
    }

    [HttpGet("{category}/{value}/recipes")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RecipeListDto>>), 200)]
    public async Task<IActionResult> GetRecipesByCategoryValue(
        string category,
        string value,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 7)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        var result =
            await _recipeService
                .GetRecipesByCategoryValueAsync(
                    category,
                    value,
                    page,
                    pageSize,
                    userId
                );

        return Ok(
            ApiResponse<PagedResult<RecipeListDto>>
                .Success(
                    $"Recipes for '{category} / {value}' retrieved.",
                    result
                )
        );
    }
}
