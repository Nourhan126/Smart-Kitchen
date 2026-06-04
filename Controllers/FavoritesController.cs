using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
[Produces("application/json")]
public class FavoritesController : ControllerBase
{
    private readonly IFavoriteService _favoriteService;

    public FavoritesController(IFavoriteService favoriteService)
    {
        _favoriteService = favoriteService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RecipeListDto>>), 200)]
    public async Task<IActionResult> GetFavorites([FromQuery] int page = 1, [FromQuery] int pageSize = 7)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _favoriteService.GetFavoritesAsync(userId, page, pageSize);
        return Ok(ApiResponse<PagedResult<RecipeListDto>>.Success("Favorites retrieved.", result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> AddFavorite([FromBody] FavoriteRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var added = await _favoriteService.AddFavoriteAsync(userId, request.RecipeId);
        if (!added)
            return BadRequest(ApiResponse<object>.Fail("Recipe already in favorites or not found."));
        return Ok(ApiResponse<object>.Success("Recipe added to favorites."));
    }

    [HttpDelete("{recipeId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> RemoveFavorite(int recipeId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var removed = await _favoriteService.RemoveFavoriteAsync(userId, recipeId);
        if (!removed)
            return NotFound(ApiResponse<object>.Fail("Favorite not found."));
        return Ok(ApiResponse<object>.Success("Recipe removed from favorites."));
    }

    [HttpGet("{recipeId:int}/is-favorite")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    public async Task<IActionResult> IsFavorite(int recipeId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isFav = await _favoriteService.IsFavoriteAsync(userId, recipeId);
        return Ok(ApiResponse<bool>.Success("Favorite status retrieved.", isFav));
    }
}
