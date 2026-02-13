using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PasswordManagerApi.Data;
using PasswordManagerApi.DTOs;
using PasswordManagerApi.Services;
using PasswordManagerApi.Tests.Helpers;
using Xunit;

namespace PasswordManagerApi.Tests.Integration;

public class GetPasswordByIdEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GetPasswordByIdEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetPasswordById_WithValidEntry_ReturnsDecryptedPassword()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser1");
        var expectedPassword = "MySecret@123";
        var entry = await builder.CreatePasswordEntryAsync(user.Id, expectedPassword, "Test Entry");

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/passwords/{entry.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        result.Should().NotBeNull();
        result!.Password.Should().Be(expectedPassword);
        result.Title.Should().Be("Test Entry");
    }

    [Fact]
    public async Task GetPasswordById_WithCorruptedData_ThrowsUnhandledException()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser2");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "ValidPassword", "Test Entry");

        // Corrupt the encrypted password
        await DatabaseHelper.CorruptPasswordEntryAsync(db, entry.Id);

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/passwords/{entry.Id}");

        // Assert
        // EXPECTED TO FAIL: No try-catch around cipher.Decrypt() at line 200
        // This should return 500 Internal Server Error or a handled error response
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetPasswordById_WithNullEncryptedPassword_ThrowsException()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser3");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "ValidPassword", "Test Entry");

        // Set encrypted password to null
        await DatabaseHelper.SetNullPasswordAsync(db, entry.Id);

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/passwords/{entry.Id}");

        // Assert
        // EXPECTED TO FAIL: No null check before cipher.Decrypt()
        // Should throw ArgumentNullException
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetPasswordById_WithEmptyEncryptedPassword_ThrowsException()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser4");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "ValidPassword", "Test Entry");

        // Set encrypted password to empty string
        await DatabaseHelper.SetEmptyPasswordAsync(db, entry.Id);

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/passwords/{entry.Id}");

        // Assert
        // EXPECTED TO FAIL: No validation for empty encrypted passwords
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetPasswordById_WithSpecialCharacters_DecryptsCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser5");
        var specialPassword = "<script>alert('xss')</script>";
        var entry = await builder.CreatePasswordEntryAsync(user.Id, specialPassword, "XSS Test Entry");

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/passwords/{entry.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        result.Should().NotBeNull();
        result!.Password.Should().Be(specialPassword, "special characters should be preserved exactly");
    }

    [Fact]
    public async Task GetPasswordById_OtherUsersEntry_Returns404()
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

        // Try to access with user B's token
        var tokenB = JwtTokenHelper.CreateToken(userB, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        // Act
        var response = await _client.GetAsync($"/api/passwords/{entry.Id}");

        // Assert
        // Should return 404 because ownership check filters it out
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPasswordById_NonexistentId_Returns404()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser6");

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/passwords/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
