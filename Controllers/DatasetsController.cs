using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/datasets")]
[Produces("application/json")]
public class DatasetsController : ControllerBase
{
    private readonly IDatasetIngestionService _ingestionService;

    private readonly IRecommendationService _recommendations;

    public DatasetsController(
        IDatasetIngestionService ingestionService,
        IRecommendationService recommendations)
    {
        _ingestionService = ingestionService;

        _recommendations = recommendations;
    }

    // ================= SEED RECIPES =================

    [HttpPost("seed")]
    [ProducesResponseType(
        typeof(ApiResponse<DatasetIngestionResult>),
        200)]
    public async Task<IActionResult> SeedRecipes()
    {
        var result =
            await _ingestionService.SeedRecipesAsync(
                HttpContext.RequestAborted);

        return Ok(
            ApiResponse<DatasetIngestionResult>
                .Success(
                    "Dataset seed complete.",
                    result));
    }

    // ================= SYNC IMAGES =================

    [HttpPost("sync-images")]
    [ProducesResponseType(
        typeof(ApiResponse<DatasetIngestionResult>),
        200)]
    public async Task<IActionResult> SyncImages()
    {
        var result =
            await _ingestionService.SyncImagesAsync(
                HttpContext.RequestAborted);

        return Ok(
            ApiResponse<DatasetIngestionResult>
                .Success(
                    "Image sync complete.",
                    result));
    }

    // ================= IMPORT RECOMMENDATIONS =================

    [HttpPost("import-recommendations")]
    [ProducesResponseType(
        typeof(ApiResponse<RecommendationImportResult>),
        200)]
    public async Task<IActionResult>
        ImportRecommendations()
    {
        var result =
            await _recommendations.ImportAsync(
                HttpContext.RequestAborted);

        return Ok(
            ApiResponse<RecommendationImportResult>
                .Success(
                    "Recommendation import complete.",
                    result));
    }
}