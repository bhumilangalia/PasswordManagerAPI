using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PasswordManagerApi.DTOs;
using Xunit;

namespace PasswordManagerApi.Tests.Integration;

[Collection("Sequential")]
public class DeletePasswordEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DeletePasswordEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string token, int entryId)> CreateAuthenticatedUserWithPassword()
    {
        // Register and login
        var username = $"deleteuser_{Guid.NewGuid()}";
        var password = "DeleteTest123!";

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
        var token = authResponse!.Token;

        // Create a password entry
        var client = new HttpClient { BaseAddress = _client.BaseAddress };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = "To Be Deleted",
            Password = "password123"
        });

        var entry = await createResponse.Content.ReadFromJsonAsync<PasswordEntryResponse>();

        return (token, entry!.Id);
    }

    [Fact]
    public async Task DeletePassword_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var (token, entryId) = await CreateAuthenticatedUserWithPassword();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync($"/api/passwords/{entryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeletePassword_DeletedEntry_CannotBeRetrieved()
    {
        // Arrange
        var (token, entryId) = await CreateAuthenticatedUserWithPassword();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Delete the entry
        await _client.DeleteAsync($"/api/passwords/{entryId}");

        // Try to retrieve deleted entry
        var getResponse = await _client.GetAsync($"/api/passwords/{entryId}");

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePassword_NonexistentId_ReturnsNotFound()
    {
        // Arrange
        var (token, _) = await CreateAuthenticatedUserWithPassword();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var nonexistentId = 999999;

        // Act
        var response = await _client.DeleteAsync($"/api/passwords/{nonexistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePassword_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - Create entry with auth, then remove auth header
        var (_, entryId) = await CreateAuthenticatedUserWithPassword();
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.DeleteAsync($"/api/passwords/{entryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletePassword_OtherUsersEntry_ReturnsNotFound()
    {
        // Arrange - Create entry for user 1
        var (token1, entryId) = await CreateAuthenticatedUserWithPassword();

        // Create user 2
        var username2 = $"deleteuser2_{Guid.NewGuid()}";
        var password2 = "DeleteTest123!";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username2,
            Password = password2
        });

        var loginResponse2 = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username2,
            Password = password2
        });
        var auth2 = await loginResponse2.Content.ReadFromJsonAsync<AuthResponse>();

        // Act - Try to delete user 1's entry as user 2
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth2!.Token);
        var response = await _client.DeleteAsync($"/api/passwords/{entryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "Users cannot delete other users' entries");
    }

    [Fact]
    public async Task DeletePassword_InvalidId_ReturnsBadRequest()
    {
        // Arrange
        var (token, _) = await CreateAuthenticatedUserWithPassword();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync("/api/passwords/invalid");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeletePassword_NegativeId_ReturnsNotFound()
    {
        // Arrange
        var (token, _) = await CreateAuthenticatedUserWithPassword();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync("/api/passwords/-1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePassword_ZeroId_ReturnsNotFound()
    {
        // Arrange
        var (token, _) = await CreateAuthenticatedUserWithPassword();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.DeleteAsync("/api/passwords/0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePassword_DeletedEntry_RemovedFromGetAllList()
    {
        // Arrange
        var (token, entryId) = await CreateAuthenticatedUserWithPassword();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Delete the entry
        await _client.DeleteAsync($"/api/passwords/{entryId}");

        // Get all passwords
        var getAllResponse = await _client.GetAsync("/api/passwords");
        var allPasswords = await getAllResponse.Content.ReadFromJsonAsync<List<PasswordEntryResponse>>();

        // Assert
        allPasswords.Should().NotContain(p => p.Id == entryId);
    }

    [Fact]
    public async Task DeletePassword_MultipleDeletes_AllSucceed()
    {
        // Arrange - Create multiple entries
        var username = $"multidelete_{Guid.NewGuid()}";
        var password = "MultiDelete123!";

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

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);

        // Create 3 entries
        var ids = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            var createResponse = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
            {
                Title = $"Entry {i}",
                Password = $"password{i}"
            });
            var entry = await createResponse.Content.ReadFromJsonAsync<PasswordEntryResponse>();
            ids.Add(entry!.Id);
        }

        // Act - Delete all entries
        foreach (var id in ids)
        {
            var response = await _client.DeleteAsync($"/api/passwords/{id}");
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        // Assert - Verify all deleted
        var getAllResponse = await _client.GetAsync("/api/passwords");
        var allPasswords = await getAllResponse.Content.ReadFromJsonAsync<List<PasswordEntryResponse>>();

        allPasswords.Should().BeEmpty();
    }

    [Fact]
    public async Task DeletePassword_DoubleDeletion_SecondReturnsNotFound()
    {
        // Arrange
        var (token, entryId) = await CreateAuthenticatedUserWithPassword();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Delete twice
        var firstResponse = await _client.DeleteAsync($"/api/passwords/{entryId}");
        var secondResponse = await _client.DeleteAsync($"/api/passwords/{entryId}");

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletePassword_DoesNotAffectOtherUsersEntries()
    {
        // Arrange - Create two users with entries
        var (token1, entryId1) = await CreateAuthenticatedUserWithPassword();

        var username2 = $"deleteuser2_{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username2,
            Password = "Password123!"
        });

        var login2 = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username2,
            Password = "Password123!"
        });
        var auth2 = await login2.Content.ReadFromJsonAsync<AuthResponse>();

        var client2 = new HttpClient { BaseAddress = _client.BaseAddress };
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth2!.Token);

        var create2 = await client2.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = "User 2 Entry",
            Password = "password"
        });
        var entry2 = await create2.Content.ReadFromJsonAsync<PasswordEntryResponse>();

        // Act - Delete user 1's entry
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);
        await _client.DeleteAsync($"/api/passwords/{entryId1}");

        // Assert - User 2's entry still exists
        var get2Response = await client2.GetAsync($"/api/passwords/{entry2!.Id}");
        get2Response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeletePassword_WithExpiredToken_ReturnsUnauthorized()
    {
        // Arrange
        var (token, entryId) = await CreateAuthenticatedUserWithPassword();

        // Simulate expired token by using invalid token
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "expired.token.here");

        // Act
        var response = await _client.DeleteAsync($"/api/passwords/{entryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
