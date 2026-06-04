using SmartKitchen.API.DTOs;

namespace SmartKitchen.API.Services;

public interface IDetectionService
{
    DetectionResult Classify(
        SensorDataRequest current,
        SensorDataRequest? previous,
        bool gasEnabled);

    string GetGasStatus(int gasValue);

    // الجديد
    string GetLightStatus(bool light);

    // الجديد
    string GetBuzzerStatus(bool buzzer);
}