using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PasswordManagerApi.DTOs;
using PasswordManagerApi.Tests.Helpers;
using Xunit;

namespace PasswordManagerApi.Tests.Integration;

[Collection("Sequential")]
public class CreatePasswordEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public CreatePasswordEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var username = $"testuser_{Guid.NewGuid()}";
        var password = "TestPassword123!";

        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Password = password
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return authResponse!.Token;
    }

    [Fact]
    public async Task CreatePassword_WithValidData_ReturnsCreated()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePasswordEntryRequest
        {
            Title = "Test Service",
            LoginUsername = "testuser@example.com",
            Password = "SecurePassword123!",
            Website = "https://example.com",
            Notes = "Test notes"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        created.Should().NotBeNull();
        created!.Id.Should().BeGreaterThan(0);
        created.Title.Should().Be("Test Service");
        created.LoginUsername.Should().Be("testuser@example.com");
        created.Password.Should().Be("SecurePassword123!");
        created.Website.Should().Be("https://example.com");
        created.Notes.Should().Be("Test notes");
        created.PasswordStrength.Should().NotBeNull();
    }

    [Fact]
    public async Task CreatePassword_ReturnsPasswordStrength()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePasswordEntryRequest
        {
            Title = "Strength Test",
            Password = "VeryStr0ng!P@ssw0rd"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        created!.PasswordStrength.Should().NotBeNull();
        created.PasswordStrength.Should().BeOneOf(PasswordStrength.Strong, PasswordStrength.VeryStrong);
    }

    [Theory]
    [InlineData("", "password")]           // Empty title
    [InlineData("   ", "password")]        // Whitespace title
    [InlineData("Title", "")]              // Empty password
    [InlineData("Title", "   ")]           // Whitespace password
    [InlineData(null, "password")]         // Null title
    [InlineData("Title", null)]            // Null password
    public async Task CreatePassword_WithInvalidInput_ReturnsBadRequest(string title, string password)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePasswordEntryRequest
        {
            Title = title,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePassword_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - No auth token
        var request = new CreatePasswordEntryRequest
        {
            Title = "Test",
            Password = "password"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePassword_WithOnlyRequiredFields_Succeeds()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePasswordEntryRequest
        {
            Title = "Minimal Entry",
            Password = "MinimalPass123!"
            // LoginUsername, Website, Notes are optional
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        created!.Title.Should().Be("Minimal Entry");
        created.LoginUsername.Should().BeNull();
        created.Website.Should().BeNull();
        created.Notes.Should().BeNull();
    }

    [Fact]
    public async Task CreatePassword_TrimsWhitespaceFromFields()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePasswordEntryRequest
        {
            Title = "  Trim Test  ",
            LoginUsername = "  user@example.com  ",
            Password = "password",
            Website = "  https://example.com  ",
            Notes = "  test notes  "
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);

        // Assert
        var created = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        created!.Title.Should().Be("Trim Test");
        created.LoginUsername.Should().Be("user@example.com");
        created.Website.Should().Be("https://example.com");
        created.Notes.Should().Be("test notes");
    }

    [Fact]
    public async Task CreatePassword_WithUnicodeCharacters_Succeeds()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePasswordEntryRequest
        {
            Title = "测试服务",
            LoginUsername = "用户@example.com",
            Password = "密碼123!🔐",
            Notes = "笔记 📝"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        created!.Title.Should().Be("测试服务");
        created.Password.Should().Be("密碼123!🔐");
    }

    [Fact]
    public async Task CreatePassword_WithSpecialCharacters_Succeeds()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePasswordEntryRequest
        {
            Title = "Special <>&\"' Test",
            Password = "P@$$w0rd!<>&\"'",
            Notes = "Notes with <script>alert('xss')</script>"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        created!.Title.Should().Be("Special <>&\"' Test");
        created.Password.Should().Be("P@$$w0rd!<>&\"'");
    }

    [Fact]
    public async Task CreatePassword_ReturnsLocationHeader()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePasswordEntryRequest
        {
            Title = "Location Test",
            Password = "password"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);

        // Assert
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/api/passwords/");
    }

    [Fact]
    public async Task CreatePassword_PasswordIsEncrypted()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var plaintextPassword = "PlaintextPassword123!";
        var request = new CreatePasswordEntryRequest
        {
            Title = "Encryption Test",
            Password = plaintextPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);
        var created = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();

        // Retrieve and verify
        var getResponse = await _client.GetAsync($"/api/passwords/{created!.Id}");
        var retrieved = await getResponse.Content.ReadFromJsonAsync<PasswordEntryResponse>();

        // Assert
        retrieved!.Password.Should().Be(plaintextPassword, "Password should be decrypted when retrieved");
    }

    [Fact]
    public async Task CreatePassword_SetsCreatedAndUpdatedTimestamps()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var before = DateTime.UtcNow;

        var request = new CreatePasswordEntryRequest
        {
            Title = "Timestamp Test",
            Password = "password"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);
        var after = DateTime.UtcNow;

        // Assert
        var created = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        created!.CreatedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        created.UpdatedAtUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        created.CreatedAtUtc.Should().Be(created.UpdatedAtUtc, "Initial timestamps should match");
    }

    [Fact]
    public async Task CreatePassword_MultipleEntries_CreatesIndependentEntries()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Create multiple entries
        var response1 = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = "Entry 1",
            Password = "password1"
        });

        var response2 = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = "Entry 2",
            Password = "password2"
        });

        // Assert
        var entry1 = await response1.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        var entry2 = await response2.Content.ReadFromJsonAsync<PasswordEntryResponse>();

        entry1!.Id.Should().NotBe(entry2!.Id);
        entry1.Password.Should().Be("password1");
        entry2.Password.Should().Be("password2");
    }

    [Fact]
    public async Task CreatePassword_VeryLongFields_Succeeds()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePasswordEntryRequest
        {
            Title = new string('A', 500),
            Password = new string('B', 500),
            Notes = new string('C', 1000)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
