using SmartKitchen.API.DTOs;

namespace SmartKitchen.API.Services;

public class DetectionService : IDetectionService
{
    private static string GetGasRiskLevel(
    int gasValue)
    {
        if (gasValue >= 450)
            return "Critical";

        if (gasValue >= 350)
            return "High";

        if (gasValue >= 250)
            return "Warning";

        return "Normal";
    }

    private static string GetTemperatureRiskLevel(
        double temperature)
    {
        if (temperature >= 65)
            return "Critical";

        if (temperature >= 55)
            return "High";

        if (temperature >= 45)
            return "Warning";

        return "Normal";
    }
    private readonly OnnxPredictionService _onnx;

    public DetectionService(
        OnnxPredictionService onnx)
    {
        _onnx = onnx;
    }

    public DetectionResult Classify(
        SensorDataRequest current,
        SensorDataRequest? previous,
        bool gasEnabled)
    {
        if (!gasEnabled || IsZeroState(current))
        {
            return new DetectionResult
            {
                State = "Normal",
                IsAlertActive = false,
                AlertMessage = "Safe"
            };
        }

        var prediction = _onnx.Predict(
            current.GasValue,
            current.FlameDetected ? 1 : 0,
            (float)current.Temperature,
            (float)current.Humidity
        );

        Console.WriteLine(
            $"ONNX Prediction = {prediction}");

        var warnings = new List<string>();
        var gasRisk =
    GetGasRiskLevel(
        current.GasValue);

        var tempRisk =
            GetTemperatureRiskLevel(
                current.Temperature);

        if (current.FlameDetected)
        {
            warnings.Add("Fire starting detected");

            if (current.Temperature >= 60)
            {
                warnings.Add("High temperature");
            }
        }
        else if (current.GasValue >= 400)
        {
            warnings.Add("Gas leak risk");
        }
        else if (current.Temperature >= 60)
        {
            warnings.Add("High temperature");
        }
        else
        {
            warnings.Add("Safe");
        }
        var state = prediction switch
        {
            0 => "Cooking",
            1 => "Fire",
            2 => "GasLeak",
            3 => "Normal",
            _ => "Normal"
        };

        var riskLevel = "Normal";

        if (current.GasValue >= 450 ||
            current.Temperature >= 65)
        {
            riskLevel = "Critical";
        }
        else if (current.GasValue >= 350 ||
                 current.Temperature >= 55)
        {
            riskLevel = "High";
        }
        else if (current.GasValue >= 250 ||
                 current.Temperature >= 45)
        {
            riskLevel = "Warning";
        }

        var critical =
            riskLevel == "Critical";
        Console.WriteLine(
    $"RiskLevel={riskLevel}");

        Console.WriteLine(
            $"Critical={critical}");

        Console.WriteLine(
            $"GasShutdownRequired={critical}");


        var previousRisk = "Normal";

        if (previous != null)
        {
            previousRisk =
                GetGasRiskLevel(previous.GasValue);
        }
        string? trend = null;

        if (previous != null)
        {
            var gasDelta =
                current.GasValue
                - previous.GasValue;

            if (gasDelta > 50)
            {
                trend = "Increasing";
            }
            else if (gasDelta < -50)
            {
                trend = "Decreasing";
            }
        }
        string? transition = null;

        if (previousRisk != riskLevel)
        {
            transition =
                $"{previousRisk}->{riskLevel}";
        }
        return new DetectionResult
        {
            State = state,

            RiskLevel = riskLevel,

            FanRequired = critical,

            GasShutdownRequired = critical,
            Trend = trend,

            RiskTransition = transition,

            IsAlertActive =
        !warnings.Contains("Safe"),

            AlertMessage =
        string.Join(", ", warnings)
        };

        
    }

    // =====================================================
    // GAS STATUS
    // =====================================================

    public string GetGasStatus(int gasValue)
    {
        if (gasValue <= 300)
        {
            return "Normal";
        }

        if (gasValue <= 400)
        {
            return "High";
        }

        return "Critical";
    }

    // =====================================================
    // LIGHT STATUS
    // =====================================================

    public string GetLightStatus(bool light)
    {
        return light ? "On" : "Off";
    }

    // =====================================================
    // BUZZER STATUS
    // =====================================================

    public string GetBuzzerStatus(bool buzzer)
    {
        return buzzer ? "On" : "Off";
    }

  

    private static bool IsZeroState(
        SensorDataRequest request)
    {
        return request.GasValue == 0
               && !request.Light
               && !request.Buzzer
               && request.Temperature == 0
               && request.Humidity == 0
               && request.Distance == 0
               && !request.MotionDetected
               && !request.FlameDetected;
    }
}