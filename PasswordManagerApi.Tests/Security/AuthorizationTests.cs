using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PasswordManagerApi.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PasswordManagerApi.Data;
using PasswordManagerApi.Services;
using PasswordManagerApi.Tests.Helpers;
using Xunit;

namespace PasswordManagerApi.Tests.Security;

public class AuthorizationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthorizationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetPassword_WithoutAuthentication_Returns401()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("authtest1");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "SecretPassword", "Test Entry");

        // Don't set Authorization header

        // Act
        var response = await _client.GetAsync($"/api/passwords/{entry.Id}");

        // Assert
        // Should return 401 Unauthorized before attempting any decryption
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPassword_WithInvalidToken_Returns401()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("authtest2");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "SecretPassword", "Test Entry");

        // Set invalid/malformed token
        var invalidToken = JwtTokenHelper.CreateInvalidToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        // Act
        var response = await _client.GetAsync($"/api/passwords/{entry.Id}");

        // Assert
        // Should return 401 Unauthorized for invalid token
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPassword_WithExpiredToken_Returns401()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("authtest3");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "SecretPassword", "Test Entry");

        // Create expired token
        var expiredToken = JwtTokenHelper.CreateExpiredToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await _client.GetAsync($"/api/passwords/{entry.Id}");

        // Assert
        // Should return 401 Unauthorized for expired token
        // ClockSkew is set to Zero in Program.cs line 45, so expired tokens fail immediately
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPassword_DifferentUser_Returns404NotUnauthorized()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var userA = await builder.CreateUserAsync("userA");
        var userB = await builder.CreateUserAsync("userB");

        // Create entry for user A
        var entry = await builder.CreatePasswordEntryAsync(userA.Id, "UserAPassword", "User A Entry");

        // Try to access with user B's valid token
        var tokenB = JwtTokenHelper.CreateToken(userB, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        // Act
        var response = await _client.GetAsync($"/api/passwords/{entry.Id}");

        // Assert
        // Should return 404 Not Found (not 403 Forbidden)
        // This is because the query filters by userId, so entry doesn't exist for user B
        // Line 189: FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAllPasswords_WithoutAuthentication_Returns401()
    {
        // Arrange
        // Don't set Authorization header

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        // Should return 401 Unauthorized before attempting any decryption
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllPasswords_WithInvalidToken_Returns401()
    {
        // Arrange
        var invalidToken = JwtTokenHelper.CreateInvalidToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePassword_WithoutAuthentication_Returns401()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("authtest4");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "Password", "Entry");

        // Don't set Authorization header
        var updateRequest = new UpdatePasswordEntryRequest(
            Title: "Hacked",
            Password: null,
            LoginUsername: null,
            Website: null,
            Notes: null);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/passwords/{entry.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
