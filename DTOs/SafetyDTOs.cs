using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.DTOs;

public class SensorDataRequest
{
    public int GasValue { get; set; }

    // بدل SmokeLevel
    public bool Light { get; set; }
    public bool Buzzer { get; set; }

    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public bool MotionDetected { get; set; }
    public bool FlameDetected { get; set; }
    public double Distance { get; set; }
    public DateTime Timestamp { get; set; }
}

public class GasControlRequest
{
    [Required]
    public bool EnableGas { get; set; }
    public bool EnableFan { get; set; }
}

public class LatestSensorResponse
{
    public string State { get; set; } = "Normal";
    public GasUiDto Gas { get; set; } = new();

    // بدل Smoke
    public LightUiDto Light { get; set; } = new();
    public BuzzerUiDto Buzzer { get; set; } = new();

    public AlertUiDto Alert { get; set; } = new();
}

public class GasUiDto
{
    public int Value { get; set; }
    public string Status { get; set; } = "Normal";
}

// DTO جديد للـ Light
public class LightUiDto
{
    public bool IsOn { get; set; }
    public string Status { get; set; } = "Off";
}

// DTO جديد للـ Buzzer
public class BuzzerUiDto
{
    public bool IsOn { get; set; }
    public string Status { get; set; } = "Off";
}

public class AlertUiDto
{
    public bool IsActive { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ActivityLogItemDto
{
    public string Time { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SeverityIconType { get; set; } = string.Empty;
}

public class DetectionResult
{
    public string State { get; set; } = "Normal";

    public string RiskLevel { get; set; } = "Normal";

    public bool IsAlertActive { get; set; }

    public string AlertMessage { get; set; } = string.Empty;

    public bool FanRequired { get; set; }

    public bool GasShutdownRequired { get; set; }

    public bool IsRecovering { get; set; }

    public string? Trend { get; set; }

    public string? RiskTransition { get; set; }
}