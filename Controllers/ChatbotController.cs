using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

public class UploadMediaRequest
{
    public IFormFile File { get; set; } = null!;
    public string? Type { get; set; }
}

[ApiController]
[Route("api/chatbot")]
[Produces("application/json")]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbotService;
    private readonly IMediaStorageService _mediaStorage;

    public ChatbotController(
        IChatbotService chatbotService,
        IMediaStorageService mediaStorage)
    {
        _chatbotService = chatbotService;
        _mediaStorage = mediaStorage;
    }

    [HttpPost("chat")]
    [ProducesResponseType(typeof(ApiResponse<ChatResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(ApiResponse<object>.Fail("Message cannot be empty."));

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var attachments = request.AttachmentIds?.Count > 0
            ? await _mediaStorage.GetAsync(
                request.AttachmentIds,
                HttpContext.RequestAborted)
            : new List<Models.MediaAsset>();

        var reply = await _chatbotService.ChatAsync(
            request.Message,
            userId,
            attachments,
            HttpContext.RequestAborted);

        return Ok(
            ApiResponse<ChatResponse>.Success(
                "Response generated.",
                new ChatResponse
                {
                    Reply = reply
                }));
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(60_000_000)]
    [ProducesResponseType(typeof(ApiResponse<MediaUploadResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Upload(
        [FromForm] UploadMediaRequest request)
    {
        if (request.File == null)
        {
            return BadRequest(
                ApiResponse<object>.Fail("File is required."));
        }

        var requestedType = string.IsNullOrWhiteSpace(request.Type)
            ? InferMediaType(request.File.FileName).ToString()
            : request.Type;

        if (!Enum.TryParse<Models.MediaType>(
            requestedType,
            true,
            out var mediaType))
        {
            return BadRequest(
                ApiResponse<object>.Fail("Invalid media type."));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        try
        {
            var asset = await _mediaStorage.SaveAsync(
                request.File,
                mediaType,
                userId,
                HttpContext.RequestAborted);

            var response = new MediaUploadResponse
            {
                Id = asset.Id,
                Url = asset.Url,
                MediaType = asset.MediaType.ToString(),
                ContentType = asset.ContentType,
                FileName = asset.OriginalFileName,
                SizeBytes = asset.SizeBytes
            };

            return Ok(
                ApiResponse<MediaUploadResponse>.Success(
                    "Upload succeeded.",
                    response));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(
                ApiResponse<object>.Fail(ex.Message));
        }
    }

    private static Models.MediaType InferMediaType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" => Models.MediaType.Image,
            ".mp4" or ".mov" or ".avi" => Models.MediaType.Video,
            _ => Models.MediaType.Document
        };
    }
}
