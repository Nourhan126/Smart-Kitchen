namespace SmartKitchen.API.Services;

public interface IOtpService
{
    Task SendOtpByEmailAsync(string email, string otpCode);
    Task SendOtpBySmsAsync(string phoneNumber, string otpCode);
    string GenerateOtp();
}
