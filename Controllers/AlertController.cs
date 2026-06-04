using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/alerts")]
public class AlertController : ControllerBase
{
    private readonly IAlertService _alertService;

    public AlertController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLatestAlert(CancellationToken cancellationToken)
    {
        var alert = await _alertService.GetLatestAlertAsync(cancellationToken);
        return Ok(alert);
    }
}
