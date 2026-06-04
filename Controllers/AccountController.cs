using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartKitchen.API.DTOs;
using SmartKitchen.API.Models;
using SmartKitchen.API.Services;
using System.Security.Claims;

namespace SmartKitchen.API.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
[Produces("application/json")]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AccountController> _logger;
    private readonly IMediaStorageService _mediaStorage;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        ILogger<AccountController> logger,
        IMediaStorageService mediaStorage)
    {
        _userManager = userManager;
        _logger = logger;
        _mediaStorage = mediaStorage;
    }

    [HttpGet("profile")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (userId is null)
            return Unauthorized(ApiResponse<object>.Fail("Unable to identify the authenticated user."));

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User account not found."));

        return Ok(ApiResponse<ProfileDto>.Success("Profile retrieved.", new ProfileDto
        {
            Name = user.FullName,
            Email = user.Email,
            ProfileImageUrl = user.ProfileImageUrl,
            Location = user.Location
        }));
    }

    [HttpPut("profile")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Validation failed.",
                ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (userId is null)
            return Unauthorized(ApiResponse<object>.Fail("Unable to identify the authenticated user."));

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User account not found."));

        if (!string.IsNullOrWhiteSpace(request.Name))
            user.FullName = request.Name.Trim();

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            !string.Equals(user.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailInUse = await _userManager.FindByEmailAsync(request.Email);
            if (emailInUse is not null && emailInUse.Id != user.Id)
                return BadRequest(ApiResponse<object>.Fail("Email is already in use."));

            user.Email = request.Email.Trim();
            user.UserName = request.Email.Trim();
        }

        if (request.ProfileImageUrl is not null)
            user.ProfileImageUrl = request.ProfileImageUrl;

        if (request.Location is not null)
            user.Location = request.Location.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogError("Failed to update profile for user {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return StatusCode(500, ApiResponse<object>.Fail("Failed to update profile.",
                result.Errors.Select(e => e.Description)));
        }

        return Ok(ApiResponse<ProfileDto>.Success("Profile updated.", new ProfileDto
        {
            Name = user.FullName,
            Email = user.Email,
            ProfileImageUrl = user.ProfileImageUrl,
            Location = user.Location
        }));
    }

    [HttpDelete("delete")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 401)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> DeleteAccount()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (userId is null)
            return Unauthorized(ApiResponse<object>.Fail("Unable to identify the authenticated user."));

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User account not found."));

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogError("Failed to delete user {UserId}: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));

            return StatusCode(500, ApiResponse<object>.Fail("Failed to delete account.",
                result.Errors.Select(e => e.Description)));
        }

        _logger.LogInformation("User {UserId} deleted their account", userId);
        return Ok(ApiResponse<object>.Success("Your account has been permanently deleted."));
    }

    [HttpPost("/api/profile/photo")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> UploadProfilePhoto(
        [FromForm] ProfilePhotoUploadRequest request)
    {
        if (request.File == null)
            return BadRequest(ApiResponse<object>.Fail("File is required."));

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (userId is null)
            return Unauthorized(ApiResponse<object>.Fail("Unable to identify the authenticated user."));

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User account not found."));

        try
        {
            var asset = await _mediaStorage.SaveAsync(
                request.File,
                MediaType.Image,
                userId,
                HttpContext.RequestAborted);

            user.ProfileImageUrl = asset.Url;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return StatusCode(500, ApiResponse<object>.Fail(
                    "Failed to update profile photo.",
                    result.Errors.Select(e => e.Description)));

            return Ok(ApiResponse<ProfileDto>.Success("Profile photo uploaded.", ToProfileDto(user)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
    }

    [HttpDelete("/api/profile/photo")]
    [ProducesResponseType(typeof(ApiResponse<ProfileDto>), 200)]
    public async Task<IActionResult> DeleteProfilePhoto()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        if (userId is null)
            return Unauthorized(ApiResponse<object>.Fail("Unable to identify the authenticated user."));

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User account not found."));

        user.ProfileImageUrl = null;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return StatusCode(500, ApiResponse<object>.Fail(
                "Failed to delete profile photo.",
                result.Errors.Select(e => e.Description)));

        return Ok(ApiResponse<ProfileDto>.Success("Profile photo deleted.", ToProfileDto(user)));
    }

    private static ProfileDto ToProfileDto(ApplicationUser user) =>
        new()
        {
            Name = user.FullName,
            Email = user.Email,
            ProfileImageUrl = user.ProfileImageUrl,
            Location = user.Location
        };
}

public class ProfilePhotoUploadRequest
{
    public IFormFile File { get; set; } = null!;
}
