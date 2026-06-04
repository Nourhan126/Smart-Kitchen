using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class SensorReading
{
    [Key]
    public int Id { get; set; }

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

    [Required, MaxLength(30)]
    public string State { get; set; } = "Normal";
}