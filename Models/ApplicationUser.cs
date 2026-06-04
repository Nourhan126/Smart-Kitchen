using Microsoft.AspNetCore.Identity;

namespace SmartKitchen.API.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsOtpVerified { get; set; } = false;
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public string? SocialProvider { get; set; }
    public string? SocialProviderToken { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? Location { get; set; }
}
