using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.Data;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Models;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/support")]
[Produces("application/json")]
public class SupportController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SupportController> _logger;
    private readonly EmailService _emailService;

    public SupportController(
        ApplicationDbContext db,
        ILogger<SupportController> logger,
        EmailService emailService)
    {
        _db = db;
        _logger = logger;
        _emailService = emailService;
    }

    [HttpPost("contact")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Contact(
        [FromBody] ContactRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Validation failed.",
                    GetModelErrors()));
        }

        TimeZoneInfo egyptTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                "Egypt Standard Time");

        var egyptNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                egyptTimeZone);

        var message = new ContactMessage
        {
            Name = request.Name,
            Email = request.Email,
            Message = request.Message,
            CreatedAt = egyptNow
        };

        _db.ContactMessages.Add(message);

        await _db.SaveChangesAsync();

        await _emailService.SendAsync(
            request.Name,
            request.Email,
            request.Message);

        _logger.LogInformation(
            "Contact message received from {smartkitchen840@gmail.com}",
            request.Email);

        return Ok(
            ApiResponse<object>.Success(
                "Thank you for reaching out! We will get back to you shortly."));
    }

    private IEnumerable<string> GetModelErrors() =>
        ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage);
}