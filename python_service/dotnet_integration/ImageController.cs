// ─────────────────────────────────────────────────────────────────────────────
// ImageController.cs
// ASP.NET Core controller integrated with Python image microservice.
// Downloads recipe images and persists them into SQL Server.
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartKitchen.API.Data;
using SmartKitchen.API.Models;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/images")]
[Produces("application/json")]
public class ImageController : ControllerBase
{
    private readonly IImageSearchService _imageService;
    private readonly IImageUrlBuilder _imageUrlBuilder;
    private readonly ApplicationDbContext _context;

    public ImageController(
        IImageSearchService imageService,
        IImageUrlBuilder imageUrlBuilder,
        ApplicationDbContext context)
    {
        _imageService = imageService;
        _imageUrlBuilder = imageUrlBuilder;
        _context = context;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET /api/images/health
    // ─────────────────────────────────────────────────────────────────────────

    [HttpGet("health")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(503)]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        var ok = await _imageService.IsHealthyAsync(ct);

        if (ok)
        {
            return Ok(new
            {
                status = "python image service is reachable"
            });
        }

        return StatusCode(503, new
        {
            status = "python image service is unreachable"
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/images/search
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Search and persist image for a recipe.
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(ImageSearchResult), 200)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 502)]
    public async Task<IActionResult> SearchImage(
        [FromBody] SearchImageRequest request,
        CancellationToken ct)
    {
        if (request.RecipeId <= 0)
        {
            return BadRequest(new
            {
                error = "recipeId is required"
            });
        }

        // Find recipe in database
        var recipe = await _context.Recipes
    .AsNoTracking()
    .FirstOrDefaultAsync(r => r.Id == request.RecipeId, ct);

        if (recipe == null)
        {
            return NotFound(new
            {
                error = "Recipe not found"
            });
        }

        // If image already exists, return it immediately
        var existingRecipeImageUrl =
            _imageUrlBuilder.NormalizeImageUrl(recipe.ImageUrl, "recipe");

        if (existingRecipeImageUrl is not null)
        {
            return Ok(new ImageSearchResult(
                RecipeId: recipe.Id,
                RecipeName: recipe.Name,
                ImageUrl: existingRecipeImageUrl,
                Success: true,
                Error: null
            ));
        }

        // Call Python image service
        var result = await _imageService
            .SearchImageAsync(recipe.Name, targetType: "recipe", ct: ct);

        if (result == null || !result.Success)
        {
            return StatusCode(502, new
            {
                error = result?.Error ?? "Python image service failed"
            });
        }

        var publicImageUrl = _imageUrlBuilder.BuildPublicImageUrl(result);

        // Save image URL into SQL Server
        recipe.ImageUrl = publicImageUrl;

        await _context.SaveChangesAsync(ct);

        // Return response
        return Ok(new ImageSearchResult(
            RecipeId: recipe.Id,
            RecipeName: recipe.Name,
            ImageUrl: publicImageUrl,
            Success: true,
            Error: null
        ));
    }

    [HttpPost("ingredient")]
    [ProducesResponseType(typeof(IngredientImageResult), 200)]
    [ProducesResponseType(typeof(object), 502)]
    public async Task<IActionResult> SearchIngredientImage(
        [FromBody] IngredientImageRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IngredientName))
        {
            return BadRequest(new { error = "ingredientName is required" });
        }

        var ingredient = await _context.Ingredients
            .FirstOrDefaultAsync(i => i.Name == request.IngredientName, ct);

        if (ingredient is null)
        {
            ingredient = new Ingredient
            {
                Name = request.IngredientName.Trim(),
                DisplayName = request.IngredientName.Trim()
            };

            _context.Ingredients.Add(ingredient);
        }

        var existingIngredientImageUrl =
            _imageUrlBuilder.NormalizeImageUrl(ingredient.ImageUrl, "ingredient");

        if (existingIngredientImageUrl is not null)
        {
            return Ok(new IngredientImageResult(
                ingredient.Id,
                ingredient.Name,
                existingIngredientImageUrl,
                true,
                null));
        }

        var result = await _imageService.SearchImageAsync(
            ingredient.Name,
            targetType: "ingredient",
            ct: ct);

        if (result == null || !result.Success)
        {
            return StatusCode(502, new
            {
                error = result?.Error ?? "Python image service failed"
            });
        }

        ingredient.ImageUrl = _imageUrlBuilder.BuildPublicImageUrl(result);
        ingredient.DisplayName ??= ingredient.Name;

        await _context.SaveChangesAsync(ct);

        return Ok(new IngredientImageResult(
            ingredient.Id,
            ingredient.Name,
            ingredient.ImageUrl,
            true,
            null));
    }

    [HttpPost("step")]
    [ProducesResponseType(typeof(StepImageResult), 200)]
    [ProducesResponseType(typeof(object), 404)]
    [ProducesResponseType(typeof(object), 502)]
    public async Task<IActionResult> SearchStepImage(
        [FromBody] StepImageRequest request,
        CancellationToken ct)
    {
        var step = await _context.RecipeSteps
    .AsNoTracking()
    .Include(s => s.Recipe)
    .Include(s => s.Images)
    .FirstOrDefaultAsync(s => s.Id == request.RecipeStepId, ct);

        if (step is null)
        {
            return NotFound(new { error = "Recipe step not found" });
        }

        var result = await _imageService.SearchImageAsync(
            step.Recipe.Name,
            targetType: "step",
            context: request.Context ?? step.StepDescription,
            ct: ct);

        if (result == null || !result.Success)
        {
            return StatusCode(502, new
            {
                error = result?.Error ?? "Python image service failed"
            });
        }

        var imageUrl = _imageUrlBuilder.BuildPublicImageUrl(result);

        if (!step.Images.Any(i => i.ImageUrl == imageUrl))
        {
            step.Images.Add(new RecipeStepImage
            {
                ImageUrl = imageUrl,
                SortOrder = step.Images.Count,
                Source = "python-service"
            });

            await _context.SaveChangesAsync(ct);
        }

        return Ok(new StepImageResult(
            step.Id,
            imageUrl,
            true,
            null));
    }

}

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

public record SearchImageRequest(
    int RecipeId
);

public record IngredientImageRequest(
    string IngredientName
);

public record StepImageRequest(
    int RecipeStepId,
    string? Context
);

public record ImageSearchResult(
    int RecipeId,
    string RecipeName,
    string? ImageUrl,
    bool Success,
    string? Error
);

public record IngredientImageResult(
    int IngredientId,
    string IngredientName,
    string? ImageUrl,
    bool Success,
    string? Error
);

public record StepImageResult(
    int RecipeStepId,
    string? ImageUrl,
    bool Success,
    string? Error
);
