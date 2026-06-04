using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Models;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

public class MediaUploadRequest
{
    public IFormFile File { get; set; }
}

[ApiController]
[Route("api/media")]
[Produces("application/json")]
public class MediaController : ControllerBase
{
    private readonly IMediaStorageService _mediaStorage;

    public MediaController(IMediaStorageService mediaStorage)
    {
        _mediaStorage = mediaStorage;
    }

    [HttpPost("upload/{type}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(60_000_000)]
    [ProducesResponseType(typeof(ApiResponse<MediaUploadResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Upload(
        [FromRoute] string type,
        [FromForm] MediaUploadRequest request)
    {
        if (!Enum.TryParse<MediaType>(type, true, out var mediaType))
        {
            return BadRequest(
                ApiResponse<object>.Fail("Invalid media type."));
        }

        if (request.File == null)
        {
            return BadRequest(
                ApiResponse<object>.Fail("File is required."));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            var asset = await _mediaStorage.SaveAsync(
                request.File,
                mediaType,
                userId,
                HttpContext.RequestAborted);

            return Ok(
                ApiResponse<MediaUploadResponse>.Success(
                    "Upload succeeded.",
                    new MediaUploadResponse
                    {
                        Id = asset.Id,
                        Url = asset.Url,
                        MediaType = asset.MediaType.ToString(),
                        ContentType = asset.ContentType,
                        FileName = asset.OriginalFileName,
                        SizeBytes = asset.SizeBytes
                    }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(
                ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MediaAssetDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> Get(Guid id)
    {
        var asset = await _mediaStorage.GetAsync(
            id,
            HttpContext.RequestAborted);

        if (asset == null)
        {
            return NotFound(
                ApiResponse<object>.Fail("Media not found."));
        }

        return Ok(
            ApiResponse<MediaAssetDto>.Success(
                "Media retrieved.",
                new MediaAssetDto
                {
                    Id = asset.Id,
                    Url = asset.Url,
                    MediaType = asset.MediaType.ToString(),
                    ContentType = asset.ContentType,
                    FileName = asset.OriginalFileName,
                    SizeBytes = asset.SizeBytes,
                    CreatedAt = asset.CreatedAt
                }));
    }
}