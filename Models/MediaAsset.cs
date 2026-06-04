using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.Models;

public class MediaAsset
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(300)]
    public string OriginalFileName { get; set; } = string.Empty;

    [Required, MaxLength(260)]
    public string StoredFileName { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string ContentType { get; set; } = string.Empty;

    public MediaType MediaType { get; set; }

    public long SizeBytes { get; set; }

    [Required, MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string RelativePath { get; set; } = string.Empty;

    public string? ExtractedText { get; set; }

    public DateTime? ExtractedAt { get; set; }

    public DateTime CreatedAt { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(
        DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById(
            "Egypt Standard Time"));
}
