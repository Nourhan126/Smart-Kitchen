using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartKitchen.API.Data;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Hubs;
using SmartKitchen.API.Models;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/sensors")]
public class SensorController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IDetectionService _detectionService;
    private readonly IAlertService _alertService;
    private readonly IHubContext<SafetyHub> _hubContext;

    public SensorController(
        ApplicationDbContext db,
        IDetectionService detectionService,
        IAlertService alertService,
        IHubContext<SafetyHub> hubContext)
    {
        _db = db;
        _detectionService = detectionService;
        _alertService = alertService;
        _hubContext = hubContext;
    }

    [HttpPost("data")]
    public async Task<IActionResult> PostSensorData(
        [FromBody] SensorDataRequest request,
        CancellationToken cancellationToken)
    {
        var gasState =
            await _alertService.GetGasControlStateAsync(
                cancellationToken);

        var previousAlert =
            await _alertService.GetLatestAlertAsync(
                cancellationToken);

        var previousReading =
            await _db.SensorReadings
                .AsNoTracking()
                .OrderByDescending(s => s.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);

        var previousRequest =
            previousReading is null
                ? null
                : new SensorDataRequest
                {
                    GasValue = previousReading.GasValue,

                    // الجديد
                    Light = previousReading.Light,
                    Buzzer = previousReading.Buzzer,

                    Temperature = previousReading.Temperature,
                    Humidity = previousReading.Humidity,
                    MotionDetected = previousReading.MotionDetected,
                    FlameDetected = previousReading.FlameDetected,
                    Distance = previousReading.Distance,
                    Timestamp = previousReading.Timestamp
                };

        var timestamp =
            CairoTime.NormalizeIncomingTimestamp(
                request.Timestamp);

        var normalizedRequest =
            new SensorDataRequest
            {
                GasValue = request.GasValue,

                // الجديد
                Light = request.Light,
                Buzzer = request.Buzzer,

                Temperature = request.Temperature,
                Humidity = request.Humidity,
                MotionDetected = request.MotionDetected,
                FlameDetected = request.FlameDetected,
                Distance = request.Distance,
                Timestamp = timestamp
            };

        var detection =
            _detectionService.Classify(
                normalizedRequest,
                previousRequest,
                true);
        if (detection.Trend == "Increasing")
        {
            _db.ActivityLogEntries.Add(
                new ActivityLogEntry
                {
                    Time = DateTime.UtcNow,
                    Title = "Gas Level Increasing",
                    Description =
                        "Gas concentration continues to rise.",
                    SeverityIconType = "warning"
                });
        }

        if (detection.Trend == "Decreasing")
        {
            _db.ActivityLogEntries.Add(
                new ActivityLogEntry
                {
                    Time = DateTime.UtcNow,
                    Title = "Gas Leak Improving",
                    Description =
                        "Gas concentration is decreasing.",
                    SeverityIconType = "info"
                });
        }
        if (detection.RiskTransition ==
    "Warning->High")
        {
            _db.ActivityLogEntries.Add(
                new ActivityLogEntry
                {
                    Time = DateTime.UtcNow,
                    Title = "Risk Level Increased",
                    Description =
                        "Gas concentration reached a higher risk category.",
                    SeverityIconType = "warning"
                });
        }

        if (detection.RiskTransition ==
            "High->Critical")
        {
            _db.ActivityLogEntries.Add(
                new ActivityLogEntry
                {
                    Time = DateTime.UtcNow,
                    Title = "Critical Gas Leak Detected",
                    Description =
                        "Emergency threshold exceeded. Safety actions activated.",
                    SeverityIconType = "critical"
                });
        }
        if (detection.RiskTransition ==
    "Critical->High")
        {
            _db.ActivityLogEntries.Add(
                new ActivityLogEntry
                {
                    Time = DateTime.UtcNow,
                    Title = "Risk Level Reduced",
                    Description =
                        "Gas concentration dropped below the critical threshold.",
                    SeverityIconType = "info"
                });
        }

        if (detection.RiskTransition ==
            "Warning->Normal")
        {
            _db.ActivityLogEntries.Add(
                new ActivityLogEntry
                {
                    Time = DateTime.UtcNow,
                    Title = "Environment Restored",
                    Description =
                        "All sensor readings returned to safe levels.",
                    SeverityIconType = "success"
                });
        }
        _db.SensorReadings.Add(
            new SensorReading
            {
                GasValue = normalizedRequest.GasValue,

                // الجديد
                Light = normalizedRequest.Light,
                Buzzer = normalizedRequest.Buzzer,

                Temperature = normalizedRequest.Temperature,
                Humidity = normalizedRequest.Humidity,
                MotionDetected = normalizedRequest.MotionDetected,
                FlameDetected = normalizedRequest.FlameDetected,
                Distance = normalizedRequest.Distance,
                Timestamp = normalizedRequest.Timestamp,
                State = detection.State
            });

        await _db.SaveChangesAsync(
            cancellationToken);

        await _alertService.HandleDetectionAsync(
    detection,
    previousAlert.IsActive,
    true,
    cancellationToken);
        if (detection.GasShutdownRequired)
        {
            Console.WriteLine(
    "AUTO SHUTDOWN TRIGGERED");
            await _alertService.SetGasControlStateAsync(
                false,
                true,
                "sensor",
                cancellationToken);
        }
        if (detection.State == "Normal")
        {
            await _alertService.SetGasControlStateAsync(
                false,
                false,
                "sensor",
                cancellationToken);
        }


        if (detection.State == "Normal")
        {
            var gasControl =
                await _alertService.GetGasControlStateAsync(
                    cancellationToken);

            if (!gasControl.IsEnabled ||
                gasControl.IsFanEnabled)
            {
                await _alertService.SetGasControlStateAsync(
                    true,   // Gas ON
                    false,  // Fan OFF
                    "sensor",
                    cancellationToken);
            }
        }

        var latestAlert =
            await _alertService.GetLatestAlertAsync(
                cancellationToken);

        var response =
            new LatestSensorResponse
            {
                State = detection.State,

                Gas = new GasUiDto
                {
                    Value = normalizedRequest.GasValue,
                    Status = _detectionService
                        .GetGasStatus(
                            normalizedRequest.GasValue)
                },

                // Light
                Light = new LightUiDto
                {
                    IsOn = normalizedRequest.Light,
                    Status = normalizedRequest.Light
                        ? "On"
                        : "Off"
                },

                // Buzzer
                Buzzer = new BuzzerUiDto
                {
                    IsOn = normalizedRequest.Buzzer,
                    Status = normalizedRequest.Buzzer
                        ? "On"
                        : "Off"
                },

                Alert = latestAlert
            };

        await _hubContext.Clients.All.SendAsync(
            "SensorUpdated",
            response,
            cancellationToken);

        await _hubContext.Clients.All.SendAsync(
            "AlertUpdated",
            latestAlert,
            cancellationToken);
        if (!previousAlert.IsActive &&
            latestAlert.IsActive)
        {
            await _hubContext.Clients.All.SendAsync(
                "PushNotification",
                "Emergency Leak! Gas Leak in Kitchen",
                cancellationToken);
        }

        return Ok();
    }
}
