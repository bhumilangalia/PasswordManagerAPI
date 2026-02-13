using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PasswordManagerApi.Models;

namespace PasswordManagerApi.Tests.Helpers;

public static class JwtTokenHelper
{
    /// <summary>
    /// Creates a valid JWT token for the given user.
    /// </summary>
    public static string CreateToken(AppUser user, IConfiguration config)
    {
        var jwtSection = config.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("JWT key missing.");
        var issuer = jwtSection["Issuer"] ?? "PasswordManagerApi";
        var audience = jwtSection["Audience"] ?? "PasswordManagerApiUsers";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(2);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Creates an expired JWT token for testing authentication failures.
    /// </summary>
    public static string CreateExpiredToken(AppUser user, IConfiguration config)
    {
        var jwtSection = config.GetSection("Jwt");
        var key = jwtSection["Key"] ?? throw new InvalidOperationException("JWT key missing.");
        var issuer = jwtSection["Issuer"] ?? "PasswordManagerApi";
        var audience = jwtSection["Audience"] ?? "PasswordManagerApiUsers";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        // Set expiration to 1 hour in the past
        var expires = DateTime.UtcNow.AddHours(-1);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Creates an invalid JWT token (malformed) for testing.
    /// </summary>
    public static string CreateInvalidToken()
    {
        return "Invalid.JWT.Token";
    }

    /// <summary>
    /// Creates a JWT token with a different signing key for testing validation failures.
    /// </summary>
    public static string CreateTokenWithWrongKey(AppUser user, IConfiguration config)
    {
        var jwtSection = config.GetSection("Jwt");
        var issuer = jwtSection["Issuer"] ?? "PasswordManagerApi";
        var audience = jwtSection["Audience"] ?? "PasswordManagerApiUsers";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username)
        };

        // Use a different key than the one in configuration
        var wrongKey = "WrongSecretKeyThatDoesNotMatchConfigurationKey123456";
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(wrongKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(2);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
