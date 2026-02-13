using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using PasswordManagerApi.DTOs;
using Xunit;

namespace PasswordManagerApi.Tests.Integration;

[Collection("Sequential")]
public class AuthenticationEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthenticationEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    #region Register Endpoint Tests

    [Fact]
    public async Task Register_WithValidCredentials_ReturnsCreated()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = $"newuser_{Guid.NewGuid()}",
            Password = "SecurePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        content.Should().ContainKey("message");
        content!["message"].Should().Contain("registered successfully");
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ReturnsConflict()
    {
        // Arrange
        var username = $"duplicate_{Guid.NewGuid()}";
        var request1 = new RegisterRequest { Username = username, Password = "Password123!" };
        var request2 = new RegisterRequest { Username = username, Password = "Different123!" };

        // Act
        await _client.PostAsJsonAsync("/api/auth/register", request1);
        var response = await _client.PostAsJsonAsync("/api/auth/register", request2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        content!["message"].Should().Contain("already exists");
    }

    [Theory]
    [InlineData("", "Password123!")]           // Empty username
    [InlineData("   ", "Password123!")]        // Whitespace username
    [InlineData("user", "")]                   // Empty password
    [InlineData("user", "   ")]                // Whitespace password
    [InlineData(null, "Password123!")]         // Null username
    [InlineData("user", null)]                 // Null password
    public async Task Register_WithInvalidInput_ReturnsBadRequest(string username, string password)
    {
        // Arrange
        var request = new RegisterRequest { Username = username, Password = password };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("short")]                      // Too short
    [InlineData("nouppercaseornumbers!")]     // No uppercase or numbers
    [InlineData("NOLOWERCASE123!")]           // No lowercase
    [InlineData("NoSpecialChars123")]         // No special characters
    [InlineData("NoNum!")]                    // No numbers
    public async Task Register_WithWeakPassword_ReturnsBadRequest(string password)
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = $"user_{Guid.NewGuid()}",
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("password");
    }

    [Fact]
    public async Task Register_CaseInsensitiveUsername_ReturnsConflict()
    {
        // Arrange
        var usernameBase = $"testuser_{Guid.NewGuid()}";
        var request1 = new RegisterRequest { Username = usernameBase.ToLower(), Password = "Password123!" };
        var request2 = new RegisterRequest { Username = usernameBase.ToUpper(), Password = "Password123!" };

        // Act
        await _client.PostAsJsonAsync("/api/auth/register", request1);
        var response = await _client.PostAsJsonAsync("/api/auth/register", request2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict, "Usernames should be case-insensitive");
    }

    [Fact]
    public async Task Register_WithLeadingTrailingSpaces_TrimsUsername()
    {
        // Arrange
        var username = $"spaceuser_{Guid.NewGuid()}";
        var request = new RegisterRequest
        {
            Username = $"  {username}  ",
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Try to register again with trimmed username
        var request2 = new RegisterRequest { Username = username, Password = "Password123!" };
        var response2 = await _client.PostAsJsonAsync("/api/auth/register", request2);
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict, "Spaces should be trimmed");
    }

    [Fact]
    public async Task Register_WithUnicodeUsername_Succeeds()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = $"用户_{Guid.NewGuid()}",
            Password = "SecurePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    #endregion

    #region Login Endpoint Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithToken()
    {
        // Arrange - Register a user first
        var username = $"loginuser_{Guid.NewGuid()}";
        var password = "LoginPass123!";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Password = password
        });

        // Act
        var loginRequest = new LoginRequest { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        authResponse.Should().NotBeNull();
        authResponse!.Token.Should().NotBeNullOrEmpty();
        authResponse.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange - Register a user
        var username = $"loginuser_{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Password = "CorrectPass123!"
        });

        // Act - Try login with wrong password
        var loginRequest = new LoginRequest { Username = username, Password = "WrongPass123!" };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonexistentUser_ReturnsUnauthorized()
    {
        // Act
        var loginRequest = new LoginRequest
        {
            Username = $"nonexistent_{Guid.NewGuid()}",
            Password = "Password123!"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("", "Password123!")]
    [InlineData("user", "")]
    [InlineData("   ", "Password123!")]
    [InlineData("user", "   ")]
    [InlineData(null, "Password123!")]
    [InlineData("user", null)]
    public async Task Login_WithInvalidInput_ReturnsBadRequest(string username, string password)
    {
        // Arrange
        var request = new LoginRequest { Username = username, Password = password };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_CaseInsensitiveUsername_Succeeds()
    {
        // Arrange - Register with lowercase
        var username = $"casetest_{Guid.NewGuid()}";
        var password = "Password123!";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username.ToLower(),
            Password = password
        });

        // Act - Login with uppercase
        var loginRequest = new LoginRequest { Username = username.ToUpper(), Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_TokenContainsUserId()
    {
        // Arrange - Register and login
        var username = $"tokentest_{Guid.NewGuid()}";
        var password = "TokenTest123!";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Password = password
        });

        var loginRequest = new LoginRequest { Username = username, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Act - Use token to access protected endpoint
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResponse!.Token);

        var protectedResponse = await _client.GetAsync("/api/passwords");

        // Assert
        protectedResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Token should authenticate user");
    }

    [Fact]
    public async Task Login_MultipleSuccessfulLogins_GenerateDifferentTokens()
    {
        // Arrange
        var username = $"multilogin_{Guid.NewGuid()}";
        var password = "MultiLogin123!";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Password = password
        });

        var loginRequest = new LoginRequest { Username = username, Password = password };

        // Act
        var response1 = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        await Task.Delay(100); // Small delay
        var response2 = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        var auth1 = await response1.Content.ReadFromJsonAsync<AuthResponse>();
        var auth2 = await response2.Content.ReadFromJsonAsync<AuthResponse>();

        auth1!.Token.Should().NotBe(auth2!.Token, "Each login should generate a unique token");
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task Auth_RateLimiting_BlocksAfter5Requests()
    {
        // Arrange
        var username = $"ratelimit_{Guid.NewGuid()}";
        var loginRequest = new LoginRequest { Username = username, Password = "WrongPass123!" };

        // Act - Make 6 rapid requests
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 6; i++)
        {
            responses.Add(await _client.PostAsJsonAsync("/api/auth/login", loginRequest));
        }

        // Assert
        responses.Take(5).Should().AllSatisfy(r =>
            r.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "First 5 requests should process normally"));

        responses.Last().StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "6th request should be rate limited");
    }

    [Fact]
    public async Task Auth_RateLimiting_ReturnsCorrectErrorMessage()
    {
        // Arrange
        var username = $"ratelimitmsg_{Guid.NewGuid()}";
        var loginRequest = new LoginRequest { Username = username, Password = "WrongPass123!" };

        // Act - Trigger rate limit
        for (int i = 0; i < 6; i++)
        {
            await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        }

        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Too many requests");
    }

    #endregion

    #region Password Strength Feedback Tests

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsStrengthFeedback()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = $"weakpass_{Guid.NewGuid()}",
            Password = "weak"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("password");
        content.Should().Match(c => c.Contains("strength") || c.Contains("requirement"));
    }

    #endregion
}
