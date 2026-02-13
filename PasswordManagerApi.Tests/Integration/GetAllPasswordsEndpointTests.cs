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

public class GetAllPasswordsEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GetAllPasswordsEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetAllPasswords_WithValidEncryptedData_ReturnsDecryptedPasswords()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser1");
        var entry1 = await builder.CreatePasswordEntryAsync(user.Id, "Password1@123", "Entry 1");
        var entry2 = await builder.CreatePasswordEntryAsync(user.Id, "Password2@456", "Entry 2");
        var entry3 = await builder.CreatePasswordEntryAsync(user.Id, "Password3@789", "Entry 3");

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var passwords = await response.Content.ReadFromJsonAsync<List<PasswordEntryResponse>>();
        passwords.Should().NotBeNull();
        passwords.Should().HaveCount(3);

        // Verify passwords are decrypted correctly
        passwords!.Should().Contain(p => p.Password == "Password1@123");
        passwords.Should().Contain(p => p.Password == "Password2@456");
        passwords.Should().Contain(p => p.Password == "Password3@789");
    }

    [Fact]
    public async Task GetAllPasswords_WithOneCorruptedEntry_ThrowsUnhandledException()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser2");
        var entry1 = await builder.CreatePasswordEntryAsync(user.Id, "ValidPassword1", "Entry 1");
        var entry2 = await builder.CreatePasswordEntryAsync(user.Id, "ValidPassword2", "Entry 2");
        var entry3 = await builder.CreatePasswordEntryAsync(user.Id, "ValidPassword3", "Entry 3");

        // Corrupt entry #2
        await DatabaseHelper.CorruptPasswordEntryAsync(db, entry2.Id);

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        // EXPECTED TO FAIL: No try-catch around cipher.Decrypt() at line 169
        // This will throw an unhandled CryptographicException
        // In a proper implementation, this should return 500 with error details
        // or skip corrupted entries with logging
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetAllPasswords_WithAllCorruptedEntries_ThrowsUnhandledException()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser3");
        var entry1 = await builder.CreatePasswordEntryAsync(user.Id, "Password1", "Entry 1");
        var entry2 = await builder.CreatePasswordEntryAsync(user.Id, "Password2", "Entry 2");

        // Corrupt all entries
        await DatabaseHelper.CorruptPasswordEntryAsync(db, entry1.Id);
        await DatabaseHelper.CorruptPasswordEntryAsync(db, entry2.Id);

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        // EXPECTED TO FAIL: Will throw on first corrupted entry
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetAllPasswords_WithNullEncryptedPassword_ThrowsUnhandledException()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser4");
        var entry = await builder.CreatePasswordEntryAsync(user.Id, "ValidPassword", "Entry 1");

        // Set encrypted password to null
        await DatabaseHelper.SetNullPasswordAsync(db, entry.Id);

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        // EXPECTED TO FAIL: No null check before calling cipher.Decrypt()
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetAllPasswords_WithUnicodePasswords_DecryptsCorrectly()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser5");
        var unicodePassword1 = "密碼🔐Test";
        var unicodePassword2 = "Пароль123!";
        await builder.CreatePasswordEntryAsync(user.Id, unicodePassword1, "Unicode Entry 1");
        await builder.CreatePasswordEntryAsync(user.Id, unicodePassword2, "Unicode Entry 2");

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var passwords = await response.Content.ReadFromJsonAsync<List<PasswordEntryResponse>>();
        passwords.Should().NotBeNull();
        passwords!.Should().Contain(p => p.Password == unicodePassword1);
        passwords.Should().Contain(p => p.Password == unicodePassword2);
    }

    [Fact]
    public async Task GetAllPasswords_WithLargeDataset_PerformsEfficiently()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser6");
        var entries = await builder.CreateMultiplePasswordEntriesAsync(user.Id, 500, "Entry");

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await _client.GetAsync("/api/passwords");
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var passwords = await response.Content.ReadFromJsonAsync<List<PasswordEntryResponse>>();
        passwords.Should().NotBeNull();
        passwords!.Should().HaveCount(500);

        // Performance baseline - should complete in reasonable time
        // Note: This is a baseline test, not a strict performance requirement
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10000, "decrypting 500 entries should be reasonably fast");
    }

    [Fact]
    public async Task GetAllPasswords_WithNoEntries_ReturnsEmptyArray()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cipher = scope.ServiceProvider.GetRequiredService<IPasswordCipherService>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var builder = new TestDataBuilder(db, cipher);

        var user = await builder.CreateUserAsync("testuser7");
        // Don't create any password entries

        var token = JwtTokenHelper.CreateToken(user, config);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var passwords = await response.Content.ReadFromJsonAsync<List<PasswordEntryResponse>>();
        passwords.Should().NotBeNull();
        passwords.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllPasswords_UnauthorizedUser_Returns401()
    {
        // Arrange
        // Don't set Authorization header

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
