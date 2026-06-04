using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SmartKitchen.API.Models;

namespace SmartKitchen.API.Services;

public class JwtTokenService
{
    private readonly IConfiguration _config;

    public JwtTokenService(IConfiguration config)
    {
        _config = config;
    }

    public (string token, DateTime expiresAt) CreateToken(
        ApplicationUser user)
    {
        var jwtSection =
            _config.GetSection("Jwt");

        var key =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    jwtSection["Key"]!));

        var claims =
            new List<Claim>
            {
                new(
                    JwtRegisteredClaimNames.Sub,
                    user.Id),

                new(
                    JwtRegisteredClaimNames.Email,
                    user.Email ?? string.Empty),

                new(
                    JwtRegisteredClaimNames.Name,
                    user.FullName),

                new(
                    JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString()),
            };

        TimeZoneInfo egyptTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById(
                "Egypt Standard Time");

        var egyptNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                egyptTimeZone);

        var expiresAt =
            egyptNow.AddMinutes(
                double.Parse(
                    jwtSection["ExpiresInMinutes"]
                    ?? "60"));

        var tokenDescriptor =
            new SecurityTokenDescriptor
            {
                Subject =
                    new ClaimsIdentity(claims),

                Expires = expiresAt,

                Issuer =
                    jwtSection["Issuer"],

                Audience =
                    jwtSection["Audience"],

                SigningCredentials =
                    new SigningCredentials(
                        key,
                        SecurityAlgorithms.HmacSha256)
            };

        var handler =
            new JwtSecurityTokenHandler();

        var securityToken =
            handler.CreateToken(
                tokenDescriptor);

        return (
            handler.WriteToken(securityToken),
            expiresAt);
    }
}