using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PasswordManagerApi.DTOs;
using Xunit;

namespace PasswordManagerApi.Tests.EdgeCases;

[Collection("Sequential")]
public class EdgeCaseTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EdgeCaseTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var username = $"edgeuser_{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Password = "EdgeTest123!"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = "EdgeTest123!"
        });

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    #region Empty Collections

    [Fact]
    public async Task GetAllPasswords_WithNoEntries_ReturnsEmptyArray()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var passwords = await response.Content.ReadFromJsonAsync<List<PasswordEntryResponse>>();
        passwords.Should().NotBeNull();
        passwords.Should().BeEmpty();
    }

    #endregion

    #region Boundary Values

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task GetPassword_WithNonPositiveId_ReturnsNotFound(int id)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/passwords/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPassword_WithMaxIntId_ReturnsNotFound()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync($"/api/passwords/{int.MaxValue}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Malformed Requests

    [Fact]
    public async Task CreatePassword_WithMalformedJSON_ReturnsBadRequest()
    {
        // Arrange
        var token = await GetAuthTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/passwords");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("{invalid json", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreatePassword_WithEmptyBody_ReturnsBadRequest()
    {
        // Arrange
        var token = await GetAuthTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/passwords");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Authentication Edge Cases

    [Fact]
    public async Task GetPasswords_WithMalformedToken_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.valid.token");

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPasswords_WithEmptyToken_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "");

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPasswords_WithoutBearerPrefix_ReturnsUnauthorized()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Add("Authorization", token); // Missing "Bearer"

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPasswords_WithBasicAuthInsteadOfBearer_ReturnsUnauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", "dXNlcjpwYXNzd29yZA==");

        // Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Special Characters and Encoding

    [Theory]
    [InlineData("\0")]        // Null character
    [InlineData("\n\r\t")]    // Newlines and tabs
    [InlineData("\u200B")]    // Zero-width space
    [InlineData("\uFEFF")]    // Byte order mark
    public async Task CreatePassword_WithControlCharacters_Succeeds(string specialChars)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = $"Special{specialChars}Test",
            Password = "password"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreatePassword_WithSurrogatePairs_Succeeds()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var surrogateText = "😀😃😄😁"; // Emoji using surrogate pairs

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = surrogateText,
            Password = "password"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        created!.Title.Should().Be(surrogateText);
    }

    #endregion

    #region Concurrent Operations

    [Fact]
    public async Task ConcurrentUpdates_SameEntry_AllSucceed()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = "Concurrent Test",
            Password = "password"
        });

        var created = await createResponse.Content.ReadFromJsonAsync<PasswordEntryResponse>();

        // Act - 5 concurrent updates
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 5; i++)
        {
            var client = new HttpClient { BaseAddress = _client.BaseAddress };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var task = client.PutAsJsonAsync($"/api/passwords/{created!.Id}", new UpdatePasswordEntryRequest
            {
                Title = $"Updated {i}"
            });

            tasks.Add(task);
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All should succeed (last one wins)
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    #endregion

    #region Case Sensitivity

    [Fact]
    public async Task Username_IsCaseInsensitive_ForLogin()
    {
        // Arrange
        var username = $"casetest_{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username.ToLower(),
            Password = "Password123!"
        });

        // Act - Try all variations
        var lowerResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username.ToLower(),
            Password = "Password123!"
        });

        var upperResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username.ToUpper(),
            Password = "Password123!"
        });

        var mixedResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = char.ToUpper(username[0]) + username[1..].ToLower(),
            Password = "Password123!"
        });

        // Assert
        lowerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        upperResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        mixedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Password_IsCaseSensitive()
    {
        // Arrange
        var username = $"passcase_{Guid.NewGuid()}";
        var password = "PaSsWoRd123!";

        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Password = password
        });

        // Act
        var correctCase = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });

        var wrongCase = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password.ToUpper()
        });

        // Assert
        correctCase.StatusCode.Should().Be(HttpStatusCode.OK);
        wrongCase.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Resource Limits

    [Fact]
    public async Task CreatePassword_WithExtremelyLongPassword_Succeeds()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var veryLongPassword = new string('P', 100000); // 100KB password

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = "Very Long Password Test",
            Password = veryLongPassword
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    #endregion

    #region HTTP Method Validation

    [Fact]
    public async Task Passwords_UnsupportedHTTPMethod_ReturnsMethodNotAllowed()
    {
        // Arrange
        var token = await GetAuthTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/passwords");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    #endregion

    #region Whitespace Handling

    [Theory]
    [InlineData("  Title  ", "Title")]
    [InlineData("\tTitle\t", "Title")]
    [InlineData("\nTitle\n", "Title")]
    [InlineData("  \t\nTitle\n\t  ", "Title")]
    public async Task CreatePassword_TrimsWhitespace_FromAllFields(string input, string expected)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = input,
            LoginUsername = input,
            Website = input,
            Notes = input,
            Password = "password"
        });

        // Assert
        var created = await response.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        created!.Title.Should().Be(expected);
        created.LoginUsername.Should().Be(expected);
        created.Website.Should().Be(expected);
        created.Notes.Should().Be(expected);
    }

    #endregion

    #region Data Persistence

    [Fact]
    public async Task CreatedPassword_PersistsAcrossRequests()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var password = $"UniquePassword_{Guid.NewGuid()}";
        var createResponse = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = "Persistence Test",
            Password = password
        });

        var created = await createResponse.Content.ReadFromJsonAsync<PasswordEntryResponse>();

        // Act - Retrieve multiple times
        var get1 = await _client.GetAsync($"/api/passwords/{created!.Id}");
        var get2 = await _client.GetAsync($"/api/passwords/{created.Id}");

        // Assert
        var retrieved1 = await get1.Content.ReadFromJsonAsync<PasswordEntryResponse>();
        var retrieved2 = await get2.Content.ReadFromJsonAsync<PasswordEntryResponse>();

        retrieved1!.Password.Should().Be(password);
        retrieved2!.Password.Should().Be(password);
        retrieved1.Id.Should().Be(retrieved2.Id);
    }

    #endregion

    #region Invalid Route Parameters

    [Theory]
    [InlineData("/api/passwords/abc")]
    [InlineData("/api/passwords/1.5")]
    [InlineData("/api/passwords/1e10")]
    [InlineData("/api/passwords/null")]
    public async Task GetPassword_WithInvalidIdFormat_ReturnsBadRequest(string route)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync(route);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}
