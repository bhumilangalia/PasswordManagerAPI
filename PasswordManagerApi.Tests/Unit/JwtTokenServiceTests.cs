using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using PasswordManagerApi.Models;
using PasswordManagerApi.Services;
using Xunit;

namespace PasswordManagerApi.Tests.Unit;

public class JwtTokenServiceTests
{
    private readonly IJwtTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public JwtTokenServiceTests()
    {
        // Setup configuration with test JWT settings
        var configDict = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "TestSecretKey12345678901234567890123456789012345678901234567890",
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience"
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        _tokenService = new JwtTokenService(_configuration);
    }

    [Fact]
    public void CreateToken_WithValidUser_ReturnsNonEmptyToken()
    {
        // Arrange
        var user = new AppUser
        {
            Id = 1,
            Username = "testuser",
            NormalizedUsername = "testuser"
        };

        // Act
        var token = _tokenService.CreateToken(user);

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void CreateToken_WithValidUser_TokenIsValidJwt()
    {
        // Arrange
        var user = new AppUser
        {
            Id = 1,
            Username = "testuser",
            NormalizedUsername = "testuser"
        };

        // Act
        var token = _tokenService.CreateToken(user);

        // Assert - should be parseable as JWT
        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(token));
    }

    [Fact]
    public void CreateToken_WithValidUser_ContainsUserIdClaim()
    {
        // Arrange
        var user = new AppUser
        {
            Id = 42,
            Username = "testuser",
            NormalizedUsername = "testuser"
        };

        // Act
        var token = _tokenService.CreateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);

        Assert.NotNull(userIdClaim);
        Assert.Equal("42", userIdClaim.Value);
    }

    [Fact]
    public void CreateToken_WithValidUser_ContainsUsernameClaim()
    {
        // Arrange
        var user = new AppUser
        {
            Id = 1,
            Username = "johndoe",
            NormalizedUsername = "johndoe"
        };

        // Act
        var token = _tokenService.CreateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        var usernameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);

        Assert.NotNull(usernameClaim);
        Assert.Equal("johndoe", usernameClaim.Value);
    }

    [Fact]
    public void CreateToken_WithValidUser_ContainsCorrectIssuer()
    {
        // Arrange
        var user = new AppUser
        {
            Id = 1,
            Username = "testuser",
            NormalizedUsername = "testuser"
        };

        // Act
        var token = _tokenService.CreateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Equal("TestIssuer", jwtToken.Issuer);
    }

    [Fact]
    public void CreateToken_WithValidUser_ContainsCorrectAudience()
    {
        // Arrange
        var user = new AppUser
        {
            Id = 1,
            Username = "testuser",
            NormalizedUsername = "testuser"
        };

        // Act
        var token = _tokenService.CreateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.Contains("TestAudience", jwtToken.Audiences);
    }

    [Fact]
    public void CreateToken_WithValidUser_HasExpirationSet()
    {
        // Arrange
        var user = new AppUser
        {
            Id = 1,
            Username = "testuser",
            NormalizedUsername = "testuser"
        };
        var beforeCreation = DateTime.UtcNow;

        // Act
        var token = _tokenService.CreateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.NotNull(jwtToken.ValidTo);
        Assert.True(jwtToken.ValidTo > beforeCreation);
        // Token should expire in approximately 2 hours (allow 1 minute tolerance)
        var expectedExpiry = beforeCreation.AddHours(2);
        Assert.True(Math.Abs((jwtToken.ValidTo - expectedExpiry).TotalMinutes) < 1);
    }

    [Fact]
    public void CreateToken_CalledTwiceForSameUser_GeneratesDifferentTokens()
    {
        // Arrange
        var user = new AppUser
        {
            Id = 1,
            Username = "testuser",
            NormalizedUsername = "testuser"
        };

        // Act
        var token1 = _tokenService.CreateToken(user);
        System.Threading.Thread.Sleep(10); // Small delay to ensure different timestamps
        var token2 = _tokenService.CreateToken(user);

        // Assert - tokens should be different due to different issued-at timestamps
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void CreateToken_WithDifferentUsers_GeneratesDifferentTokens()
    {
        // Arrange
        var user1 = new AppUser { Id = 1, Username = "user1", NormalizedUsername = "user1" };
        var user2 = new AppUser { Id = 2, Username = "user2", NormalizedUsername = "user2" };

        // Act
        var token1 = _tokenService.CreateToken(user1);
        var token2 = _tokenService.CreateToken(user2);

        // Assert
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void CreateToken_WithSpecialCharactersInUsername_TokenIsValid()
    {
        // Arrange
        var user = new AppUser
        {
            Id = 1,
            Username = "user@example.com",
            NormalizedUsername = "user@example.com"
        };

        // Act
        var token = _tokenService.CreateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(token));

        var jwtToken = handler.ReadJwtToken(token);
        var usernameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
        Assert.Equal("user@example.com", usernameClaim?.Value);
    }

    [Fact]
    public void CreateToken_TokenHasIssuedAtClaim()
    {
        // Arrange
        var user = new AppUser
        {
            Id = 1,
            Username = "testuser",
            NormalizedUsername = "testuser"
        };
        var beforeCreation = DateTime.UtcNow;

        // Act
        var token = _tokenService.CreateToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        Assert.NotNull(jwtToken.ValidFrom);
        Assert.True(jwtToken.ValidFrom <= DateTime.UtcNow);
        Assert.True(jwtToken.ValidFrom >= beforeCreation.AddMinutes(-1));
    }
}
