using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/activity-log")]
public class ActivityLogController : ControllerBase
{
    private readonly IAlertService _alertService;

    public ActivityLogController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string filter = "All", CancellationToken cancellationToken = default)
    {
        try
        {
            var logs = await _alertService.GetActivityLogsAsync(filter, cancellationToken);
            return Ok(logs);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
