using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Security.Cryptography;
using System.Text;

namespace SmartKitchen.API.Services;

public class OtpService : IOtpService
{
    private readonly IConfiguration _config;
    private readonly ILogger<OtpService> _logger;

    private static readonly Dictionary<string, OtpEntry> _otpStore = new();

    public OtpService(
        IConfiguration config,
        ILogger<OtpService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private class OtpEntry
    {
        public string Code { get; set; } = "";

        public DateTime Expiry { get; set; }
    }

    // ✅ مطابق للـ Interface
    public string GenerateOtp()
    {
        int value =
            RandomNumberGenerator.GetInt32(
                0,
                10000);

        return value.ToString("D4");
    }

    private void StoreOtp(
        string key,
        string otp)
    {
        key =
            key.Trim().ToLower();

        TimeZoneInfo egyptTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                "Egypt Standard Time");

        var egyptNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                egyptTimeZone);

        _otpStore[key] =
            new OtpEntry
            {
                Code = otp,
                Expiry = egyptNow.AddMinutes(60)
            };

        _logger.LogInformation(
            "OTP Stored for {Key}: {Otp}",
            key,
            otp);
    }

    public bool VerifyOtp(
        string key,
        string otp)
    {
        key =
            key.Trim().ToLower();

        if (!_otpStore.TryGetValue(
            key,
            out var entry))
        {
            return false;
        }

        TimeZoneInfo egyptTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                "Egypt Standard Time");

        var egyptNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                egyptTimeZone);

        if (entry.Expiry < egyptNow)
        {
            _otpStore.Remove(key);

            return false;
        }

        if (entry.Code != otp)
        {
            return false;
        }

        _otpStore.Remove(key);

        return true;
    }

    public async Task SendOtpByEmailAsync(
        string email,
        string otpCode)
    {
        email =
            email.Trim().ToLower();

        var section =
            _config.GetSection(
                "OtpSettings:Email");

        var host =
            section["Host"]
            ?? throw new InvalidOperationException(
                "Email Host not configured.");

        var username =
            section["Username"]
            ?? throw new InvalidOperationException(
                "Email Username not configured.");

        var password =
            section["Password"]
            ?? throw new InvalidOperationException(
                "Email Password not configured.");

        StoreOtp(email, otpCode);

        var message =
            new MimeMessage();

        message.From.Add(
            new MailboxAddress(
                section["SenderName"]
                    ?? "Smart Kitchen",

                section["SenderEmail"]
                    ?? "noreply@smartkitchen.app"));

        message.To.Add(
            MailboxAddress.Parse(email));

        message.Subject =
            "Your Smart Kitchen OTP Code";

        message.Body =
            new TextPart("html")
            {
                Text = $@"
                    <h2>Smart Kitchen – Verification Code</h2>
                    <p>Your one-time verification code is:</p>
                    <h1 style='letter-spacing:8px'>{otpCode}</h1>
                    <p>This code expires in <strong>60 minutes</strong>.</p>"
            };

        using var client =
            new SmtpClient();

        await client.ConnectAsync(
            host,
            int.Parse(
                section["Port"] ?? "587"),
            SecureSocketOptions.StartTls);

        await client.AuthenticateAsync(
            username,
            password);

        await client.SendAsync(message);

        await client.DisconnectAsync(true);
    }

    public async Task SendOtpBySmsAsync(
    string phoneNumber,
    string otpCode)
    {
        phoneNumber = phoneNumber.Trim();

        var section =
            _config.GetSection("OtpSettings:Vonage");

        var apiKey = section["ApiKey"];
        var apiSecret = section["ApiSecret"];

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(apiSecret))
        {
            throw new InvalidOperationException(
                "Vonage credentials not configured.");
        }

        StoreOtp(phoneNumber, otpCode);

        using var client = new HttpClient();

        var content =
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["api_key"] = apiKey,
                    ["api_secret"] = apiSecret,
                    ["to"] = phoneNumber.Replace("+", ""),
                    ["from"] = "SmartKitchen",
                    ["text"] = $"Your Smart Kitchen OTP is {otpCode}"
                });

        var response =
            await client.PostAsync(
                "https://rest.nexmo.com/sms/json",
                content);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Failed to send SMS: {Response}",
                responseBody);

            throw new Exception(responseBody);
        }

        _logger.LogInformation(
            "SMS sent successfully: {Response}",
            responseBody);
    }
}
