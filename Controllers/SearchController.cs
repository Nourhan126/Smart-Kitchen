using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/search")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<RecipeListDto>>), 200)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 7)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(userId))
            await _searchService.SaveSearchAsync(userId, q);

        var result = await _searchService.SearchAsync(q, page, pageSize, userId);
        return Ok(ApiResponse<PagedResult<RecipeListDto>>.Success("Search results retrieved.", result));
    }

    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), 200)]
    public async Task<IActionResult> Autocomplete([FromQuery] string prefix, [FromQuery] int limit = 10)
    {
        var suggestions = await _searchService.GetAutocompleteSuggestionsAsync(prefix, limit);
        return Ok(ApiResponse<List<string>>.Success("Suggestions retrieved.", suggestions));
    }

    [Authorize]
    [HttpGet("recent")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetRecentSearches([FromQuery] int limit = 10)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var searches = await _searchService.GetRecentSearchesAsync(userId, limit);
        return Ok(ApiResponse<List<string>>.Success("Recent searches retrieved.", searches));
    }

    [Authorize]
    [HttpDelete("recent")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ClearRecentSearches()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _searchService.ClearRecentSearchesAsync(userId);
        return Ok(ApiResponse<object>.Success("Recent searches cleared."));
    }
}
