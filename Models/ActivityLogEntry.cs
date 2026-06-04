using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class ActivityLogEntry
{
    [Key]
    public int Id { get; set; }

    public DateTime Time { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string SeverityIconType { get; set; } = string.Empty;
}
