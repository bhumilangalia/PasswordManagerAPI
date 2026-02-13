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

public class UpdatePasswordEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UpdatePasswordEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task UpdatePassword_WithNewPassword_ReturnsDecryptedNewPassword()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser1");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "OldPassword@123", "Test Entry");

        var updateRequest = new UpdatePasswordEntryRequest
        {
            Title ="Updated Entry",
            Password ="NewPassword@456",
            LoginUsername =null,
            Website =null,
            Notes = null
        };

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/passwords/{entry.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(response.Content);
        result.Should().NotBeNull();
        result!.Password.Should().Be("NewPassword@456");
        result.Title.Should().Be("Updated Entry");
    }

    [Fact]
    public async Task UpdatePassword_OnlyTitleChange_DecryptsExistingPassword()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser2");
        var existingPassword = "ExistingPassword@123";
        var entry = await builder.CreatePasswordEntryAsync(user.Id, existingPassword, "Old Title");

        var updateRequest = new UpdatePasswordEntryRequest
        {
            Title ="New Title",
            Password =null,
            LoginUsername =null,
            Website =null,
            Notes = null
        };

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/passwords/{entry.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(response.Content);
        result.Should().NotBeNull();
        result!.Password.Should().Be(existingPassword);
        result.Title.Should().Be("New Title");
    }

    [Fact]
    public async Task UpdatePassword_ExistingPasswordCorrupted_TitleChange_ThrowsException()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser3");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "ValidPassword", "Old Title");

        // Corrupt the existing password
        await DatabaseHelper.CorruptPasswordEntryAsync(db, entry.Id);

        var updateRequest = new UpdatePasswordEntryRequest
        {
            Title ="New Title",
            Password =null,
            LoginUsername =null,
            Website =null,
            Notes = null
        };

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/passwords/{entry.Id}", updateRequest);

        // Assert
        // EXPECTED TO FAIL: Line 313 calls cipher.Decrypt(entry.EncryptedPassword)
        // Even though we're only updating the title, it still decrypts for response
        // This will throw CryptographicException with no error handling
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task UpdatePassword_NewPasswordProvided_DoesNotDecryptOld()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser4");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "OldPassword", "Test Entry");

        // Corrupt the old password
        await DatabaseHelper.CorruptPasswordEntryAsync(db, entry.Id);

        var updateRequest = new UpdatePasswordEntryRequest
        {
            Title ="Updated Entry",
            Password ="NewValidPassword@123",
            LoginUsername =null,
            Website =null,
            Notes = null
        };

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/passwords/{entry.Id}", updateRequest);

        // Assert
        // When new password is provided, the API correctly uses the new plaintext password
        // without attempting to decrypt the old (corrupted) password (Program.cs lines 396-401)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(response.Content);
        result.Should().NotBeNull();
        result!.Password.Should().Be("NewValidPassword@123");
        result.Title.Should().Be("Updated Entry");
    }

    [Fact]
    public async Task UpdatePassword_WithUnicodePassword_PreservesUnicode()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser5");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "OldPassword", "Test Entry");

        var unicodePassword = "密碼🔐Пароль@123";
        var updateRequest = new UpdatePasswordEntryRequest
        {
            Title =null,
            Password =unicodePassword,
            LoginUsername =null,
            Website =null,
            Notes = null
        };

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/passwords/{entry.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(response.Content);
        result.Should().NotBeNull();
        result!.Password.Should().Be(unicodePassword);
    }

    [Fact]
    public async Task UpdatePassword_OtherUsersEntry_Returns404()
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

        // Try to update with user B's token
        var updateRequest = new UpdatePasswordEntryRequest
        {
            Title ="Hacked Title",
            Password =null,
            LoginUsername =null,
            Website =null,
            Notes = null
        };

        var tokenB = JwtTokenHelper.CreateToken(userB, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/passwords/{entry.Id}", updateRequest);

        // Assert
        // Should return 404 because ownership check filters it out (lines 270-273)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
