using SmartKitchen.API.DTOs;
using SmartKitchen.API.Models;

namespace SmartKitchen.API.Services;

public interface IAlertService
{
    Task HandleDetectionAsync(
        DetectionResult detectionResult,
        bool wasAlertActive,
        bool isGasEnabled,
        CancellationToken cancellationToken = default);

    Task<AlertUiDto> GetLatestAlertAsync(
        CancellationToken cancellationToken = default);

    Task<List<ActivityLogItemDto>> GetActivityLogsAsync(
        string filter,
        CancellationToken cancellationToken = default);

    Task<GasControlState> GetGasControlStateAsync(
        CancellationToken cancellationToken = default);

    Task<GasControlState> SetGasControlStateAsync(
        bool enableGas,
        bool enableFan,
        string source,
        CancellationToken cancellationToken = default);
}