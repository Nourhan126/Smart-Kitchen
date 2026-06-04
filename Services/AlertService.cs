using Microsoft.EntityFrameworkCore;
using SmartKitchen.API.Data;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Models;

namespace SmartKitchen.API.Services;

public class AlertService : IAlertService
{
    private readonly ApplicationDbContext _db;

    public AlertService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task HandleDetectionAsync(
        DetectionResult detectionResult,
        bool wasAlertActive,
        bool isGasEnabled,
        CancellationToken cancellationToken = default)
    {
        var nowForStorage = CairoTime.UtcNowForStorage();

        if (detectionResult.IsAlertActive)
        {
            var message =
                string.IsNullOrWhiteSpace(
                    detectionResult.AlertMessage)
                    ? "Safety Warning"
                    : detectionResult.AlertMessage;

            _db.SafetyAlerts.Add(
                new SafetyAlert
                {
                    IsActive = true,
                    Message = message,
                    State = detectionResult.State,
                    CreatedAt = nowForStorage
                });

            string title;
            string description;

            if (message.Contains("Fire starting detected") &&
                message.Contains("High temperature"))
            {
                title = "Critical Fire Warning";
                description =
                    "Flame detected together with dangerously high temperature.";
            }
            else if (message.Contains("Fire starting detected"))
            {
                title = "Fire Risk Detected";
                description =
                    "Flame sensor detected possible fire activity.";
            }
            else if (message.Contains("Gas leak risk"))
            {
                title = "Gas Leak Risk Detected";
                description =
                    "Abnormally high gas concentration detected in the kitchen.";
            }
            else if (message.Contains("High temperature"))
            {
                title = "High Temperature Detected";
                description =
                    "Kitchen temperature exceeded the safe threshold.";
            }
            else
            {
                title = "System Safe";
                description =
                    "All sensor readings are within normal ranges.";
            }

            _db.ActivityLogEntries.Add(
                new ActivityLogEntry
                {
                    Time = nowForStorage,
                    Title = title,
                    Description = description,
                    SeverityIconType = "warning"
                });

            if (isGasEnabled)
            {
                await SetGasControlStateAsync(
                    false, // Gas OFF
                    true,  // Fan ON
                    "sensor",
                    cancellationToken);
            }

            await _db.SaveChangesAsync(
                cancellationToken);

            return;
        }

        if (wasAlertActive)
        {
            _db.SafetyAlerts.Add(
                new SafetyAlert
                {
                    IsActive = false,
                    Message = "Safe",
                    State = detectionResult.State,
                    CreatedAt = nowForStorage
                });

            _db.ActivityLogEntries.AddRange(
                new ActivityLogEntry
                {
                    Time = nowForStorage,
                    Title = "Addressed and resolved issue",
                    Description =
                        "Safety issue has been addressed and resolved.",
                    SeverityIconType = "success"
                },
                new ActivityLogEntry
                {
                    Time = nowForStorage,
                    Title = "Reading Back to Normal",
                    Description =
                        "Sensor readings returned to normal ranges.",
                    SeverityIconType = "success"
                });

            await _db.SaveChangesAsync(
                cancellationToken);
        }
    }

    public async Task<AlertUiDto> GetLatestAlertAsync(
        CancellationToken cancellationToken = default)
    {
        var alert =
            await _db.SafetyAlerts
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        if (alert is null)
        {
            return new AlertUiDto
            {
                IsActive = false,
                Message = "Safe"
            };
        }

        return new AlertUiDto
        {
            IsActive = alert.IsActive,
            Message = alert.Message
        };
    }

    public async Task<List<ActivityLogItemDto>> GetActivityLogsAsync(
        string filter,
        CancellationToken cancellationToken = default)
    {
        var normalizedFilter =
            (filter ?? "All").Trim();

        var query =
            _db.ActivityLogEntries
                .AsNoTracking()
                .AsQueryable();

        var cairoNow =
            DateTime.UtcNow.AddHours(3);

        switch (normalizedFilter.ToLowerInvariant())
        {
            case "today":

                query = query.Where(
                    a => a.Time.Date == cairoNow.Date);

                break;

            case "yesterday":

                var yesterday =
                    cairoNow.Date.AddDays(-1);

                query = query.Where(
                    a => a.Time.Date == yesterday);

                break;

            case "last 7 days":

                var from =
                    cairoNow.Date.AddDays(-6);

                query = query.Where(
                    a => a.Time.Date >= from &&
                         a.Time.Date <= cairoNow.Date);

                break;

            case "all":
                break;

            default:

                throw new ArgumentException(
                    "Filter must be one of: Today, Yesterday, Last 7 days, All");
        }

        return await query
            .OrderByDescending(a => a.Time)
            .Select(a => new ActivityLogItemDto
            {
                Time = CairoTime.FormatActivityTime(a.Time),
                Title = a.Title,
                Description = a.Description,
                SeverityIconType = a.SeverityIconType
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GasControlState> GetGasControlStateAsync(
        CancellationToken cancellationToken = default)
    {
        var state =
            await _db.GasControlStates
                .FirstOrDefaultAsync(cancellationToken);

        if (state is not null)
        {
            return state;
        }

        var nowForStorage = CairoTime.UtcNowForStorage();

        state = new GasControlState
        {
            IsEnabled = false,
            UpdatedAt = nowForStorage
        };

        _db.GasControlStates.Add(state);

        await _db.SaveChangesAsync(cancellationToken);

        return state;
    }

    public async Task<GasControlState> SetGasControlStateAsync(
    bool enableGas,
    bool enableFan,
    string source,
    CancellationToken cancellationToken = default)
    {
        var state =
            await GetGasControlStateAsync(
                cancellationToken);

        if (state.IsEnabled == enableGas &&
    state.IsFanEnabled == enableFan)
        {
            return state;
        }

        var nowForStorage = CairoTime.UtcNowForStorage();

        state.IsEnabled = enableGas;
        state.IsFanEnabled = enableFan;
        state.UpdatedAt = nowForStorage;

        if (enableGas)
        {
            _db.ActivityLogEntries.Add(
    new ActivityLogEntry
    {
        Time = nowForStorage,
        Title = "Gas Supply Restored",
        Description =
            source == "sensor"
                ? "Gas supply was automatically restored after sensor readings returned to safe levels."
                : "Gas supply was manually reopened.",
        SeverityIconType = "success"
    });
        }
        else
        {
            _db.ActivityLogEntries.Add(
                new ActivityLogEntry
                {
                    Time = nowForStorage,
                    Title = "Shut down gas via sensor",
                    Description =
                        source == "sensor"
                            ? "Gas was automatically shut down by safety sensors."
                            : "Gas supply was manually shut down.",
                    SeverityIconType = "critical"
                });
        }

        await _db.SaveChangesAsync(
            cancellationToken);

        return state;
    }
}
