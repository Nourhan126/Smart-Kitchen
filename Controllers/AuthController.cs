using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Models;
using SmartKitchen.API.Services;
using Google.Apis.Auth;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOtpService _otpService;
    private readonly JwtTokenService _jwtService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        IOtpService otpService,
        JwtTokenService jwtService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _otpService = otpService;
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Validation failed.",
                    GetModelErrors()));
        }

        var existing =
            await _userManager.FindByEmailAsync(
                request.Email);

        if (existing is not null)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Email is already registered."));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.Name,
            IsOtpVerified = true
        };

        var result =
            await _userManager.CreateAsync(
                user,
                request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Registration failed.",
                    result.Errors.Select(
                        e => e.Description)));
        }

        return Ok(
            ApiResponse<object>.Success(
                "Registration successful."));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Validation failed.",
                    GetModelErrors()));
        }

        var user =
            await _userManager.FindByEmailAsync(
                request.Email);

        if (user is null ||
            !await _userManager.CheckPasswordAsync(
                user,
                request.Password))
        {
            return Unauthorized(
                ApiResponse<object>.Fail(
                    "Invalid email or password."));
        }

        var (token, expiresAt) =
            _jwtService.CreateToken(user);

        return Ok(
            ApiResponse<TokenResponse>.Success(
                "Login successful.",
                new TokenResponse
                {
                    Token = token,
                    ExpiresAt = expiresAt
                }));
    }

    [HttpPost("social-login")]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> SocialLogin(
        [FromBody] SocialLoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Validation failed.",
                    GetModelErrors()));
        }

        string? email = null;
        string name;
        string socialId;

        if (request.Provider.Equals(
            "Google",
            StringComparison.OrdinalIgnoreCase))
        {
            GoogleJsonWebSignature.Payload payload;

            try
            {
                payload =
                    await GoogleJsonWebSignature.ValidateAsync(
                        request.ProviderToken);
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Invalid Google token");

                return BadRequest(
                    ApiResponse<object>.Fail(
                        "Invalid Google token."));
            }

            email = payload.Email;
            name = payload.Name ?? email!;
            socialId = payload.Subject;
        }
        else if (request.Provider.Equals(
            "Facebook",
            StringComparison.OrdinalIgnoreCase))
        {
            using var http = new HttpClient();

            var graphUrl =
                $"https://graph.facebook.com/me?fields=id,name,email&access_token={Uri.EscapeDataString(request.ProviderToken)}";

            var response =
                await http.GetAsync(graphUrl);

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(
                    ApiResponse<object>.Fail(
                        "Invalid Facebook token."));
            }

            var json =
                await response.Content
                    .ReadFromJsonAsync<FacebookUserInfo>();

            if (json is null ||
                string.IsNullOrEmpty(json.Id))
            {
                return BadRequest(
                    ApiResponse<object>.Fail(
                        "Invalid Facebook response."));
            }

            email = json.Email;
            name = json.Name ?? "Facebook User";
            socialId = json.Id;
        }
        else
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Unsupported provider. Use 'Google' or 'Facebook'."));
        }

        var user =
            await _userManager.Users
                .FirstOrDefaultAsync(u =>
                    u.SocialProvider == request.Provider &&
                    u.SocialProviderToken == socialId);

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = socialId,
                Email = email,
                FullName = name,
                IsOtpVerified = true,
                SocialProvider = request.Provider,
                SocialProviderToken = socialId
            };

            var createResult =
                await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                return BadRequest(
                    ApiResponse<object>.Fail(
                        "Could not create user account.",
                        createResult.Errors.Select(
                            e => e.Description)));
            }
        }
        else
        {
            user.FullName = name;

            if (!string.IsNullOrEmpty(email))
            {
                user.Email = email;
            }

            await _userManager.UpdateAsync(user);
        }

        var (jwt, expiresAt) =
            _jwtService.CreateToken(user);

        return Ok(
            ApiResponse<TokenResponse>.Success(
                "Social login successful.",
                new TokenResponse
                {
                    Token = jwt,
                    ExpiresAt = expiresAt
                }));
    }

    [HttpPost("send-otp")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> SendOtp(
        [FromBody] SendOtpRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Validation failed.",
                    GetModelErrors()));
        }

        if (string.IsNullOrEmpty(request.Value))
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Value is required."));
        }

        var otp =
            _otpService.GenerateOtp();

        var user =
            await FindUserByEmailOrPhoneAsync(
                request.Value);

        if (user is not null)
        {
            TimeZoneInfo egyptTimeZone =
                TimeZoneInfo.FindSystemTimeZoneById(
                    "\"Africa/Cairo\"");

            var egyptNow =
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    egyptTimeZone);

            user.OtpCode = otp;

            user.OtpExpiresAt =
                egyptNow.AddMinutes(60);

            await _userManager.UpdateAsync(user);
        }

        try
        {
            if (request.ContactMethod.Equals(
                "phone",
                StringComparison.OrdinalIgnoreCase))
            {
                await _otpService.SendOtpBySmsAsync(
                    request.Value,
                    otp);
            }
            else if (request.ContactMethod.Equals(
                "email",
                StringComparison.OrdinalIgnoreCase))
            {
                await _otpService.SendOtpByEmailAsync(
                    request.Value,
                    otp);
            }
            else
            {
                return BadRequest(
                    ApiResponse<object>.Fail(
                        "Invalid contact method."));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send OTP to {Contact}",
                request.Value);

            return BadRequest(
                ApiResponse<object>.Fail(
                    "Failed to send OTP."));
        }

        return Ok(
            ApiResponse<object>.Success(
                "If an account exists, a verification code has been sent."));
    }

    [HttpPost("verify-otp")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Validation failed.",
                    GetModelErrors()));
        }

        var user =
            await FindUserByEmailOrPhoneAsync(
                request.EmailOrPhone);

        if (user is null)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "User not found."));
        }

        TimeZoneInfo egyptTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                "Egypt Standard Time");

        var egyptNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                egyptTimeZone);

        if (user.OtpCode != request.OtpCode ||
            user.OtpExpiresAt == null ||
            user.OtpExpiresAt < egyptNow)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Invalid or expired OTP code."));
        }

        user.OtpCode = null;
        user.OtpExpiresAt = null;

        await _userManager.UpdateAsync(user);

        return Ok(
            ApiResponse<object>.Success(
                "OTP verified successfully."));
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Validation failed.",
                    GetModelErrors()));
        }

        var user =
            await FindUserByEmailOrPhoneAsync(
                request.EmailOrPhone);

        if (user is null)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Invalid request."));
        }

        if (!user.IsOtpVerified)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "OTP not verified."));
        }

        if (await _userManager.HasPasswordAsync(user))
        {
            var removeResult =
                await _userManager.RemovePasswordAsync(user);

            if (!removeResult.Succeeded)
            {
                return BadRequest(
                    ApiResponse<object>.Fail(
                        "Password reset failed.",
                        removeResult.Errors.Select(
                            e => e.Description)));
            }
        }

        var addResult =
            await _userManager.AddPasswordAsync(
                user,
                request.NewPassword);

        if (!addResult.Succeeded)
        {
            return BadRequest(
                ApiResponse<object>.Fail(
                    "Password reset failed.",
                    addResult.Errors.Select(
                        e => e.Description)));
        }

        user.OtpCode = null;
        user.OtpExpiresAt = null;

        await _userManager.UpdateAsync(user);

        return Ok(
            ApiResponse<object>.Success(
                "Password has been reset successfully."));
    }

    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public IActionResult Logout()
    {
        return Ok(
            ApiResponse<object>.Success(
                "Logged out. Please remove the JWT token from your client storage."));
    }

    private async Task<ApplicationUser?> FindUserByEmailOrPhoneAsync(
        string emailOrPhone)
    {
        if (IsPhoneNumber(emailOrPhone))
        {
            return await _userManager.Users
                .FirstOrDefaultAsync(
                    u => u.PhoneNumber == emailOrPhone);
        }

        return await _userManager.FindByEmailAsync(
            emailOrPhone);
    }

    private static bool IsPhoneNumber(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            value,
            @"^\+[1-9]\d{6,14}$");

    private IEnumerable<string> GetModelErrors() =>
        ModelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage);
}

file sealed class FacebookUserInfo
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }
}