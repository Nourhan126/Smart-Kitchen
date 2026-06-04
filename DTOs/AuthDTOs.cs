using System.ComponentModel.DataAnnotations;

namespace SmartKitchen.API.DTOs;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MinLength(8)]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z]).{8,}$",
        ErrorMessage = "Password must be at least 8 characters and contain at least one uppercase and one lowercase letter.")]
    public string Password { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class SocialLoginRequest
{
    [Required]
    public string Provider { get; set; } = string.Empty;

    [Required]
    public string ProviderToken { get; set; } = string.Empty;
}

public class SendOtpRequest
{
    [Required]
    public string ContactMethod { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;
}

public class VerifyOtpRequest
{
    [Required]
    public string EmailOrPhone { get; set; } = string.Empty;

    [Required, StringLength(4, MinimumLength = 4)]
    public string OtpCode { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required]
    public string EmailOrPhone { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

public class ContactRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Message { get; set; } = string.Empty;
}

public class ApiResponse<T>
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public IEnumerable<string>? Errors { get; set; }

    public static ApiResponse<T> Success(string message, T? data = default) =>
        new() { Status = "success", Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, IEnumerable<string>? errors = null) =>
        new() { Status = "error", Message = message, Errors = errors };
}

public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class ProfileDto
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? Location { get; set; }
}

public class UpdateProfileRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? ProfileImageUrl { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }
}
