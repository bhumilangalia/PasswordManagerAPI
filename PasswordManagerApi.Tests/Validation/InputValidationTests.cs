using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PasswordManagerApi.DTOs;
using PasswordManagerApi.Tests.Helpers;
using Xunit;

namespace PasswordManagerApi.Tests.Validation;

[Collection("Sequential")]
public class InputValidationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public InputValidationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var username = $"validuser_{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Password = "ValidPass123!"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = "ValidPass123!"
        });

        var auth = await JsonHelper.DeserializeAsync<AuthResponse>(loginResponse.Content);
        return auth!.Token;
    }

    #region SQL Injection Prevention

    [Theory]
    [InlineData("admin'--")]
    [InlineData("' OR '1'='1")]
    [InlineData("'; DROP TABLE Users; --")]
    [InlineData("1' UNION SELECT * FROM Users--")]
    public async Task Register_WithSQLInjectionAttempt_TreatsAsLiteralString(string maliciousUsername)
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = maliciousUsername,
            Password = "Password123!"
        });

        // Assert - Should succeed or fail validation, but never execute SQL
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);

        if (response.StatusCode == HttpStatusCode.Created)
        {
            // If created, should be able to login with exact string
            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = maliciousUsername,
                Password = "Password123!"
            });
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("1' UNION SELECT password FROM PasswordEntries--")]
    public async Task CreatePassword_WithSQLInjectionInTitle_TreatsAsLiteralString(string maliciousTitle)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = maliciousTitle,
            Password = "password"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(response.Content);
        created!.Title.Should().Be(maliciousTitle, "SQL injection attempts should be stored as literal strings");
    }

    #endregion

    #region XSS Prevention

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert('xss')>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("<svg onload=alert('xss')>")]
    public async Task CreatePassword_WithXSSPayload_StoresAndReturnsLiterally(string xssPayload)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = xssPayload,
            Password = "password",
            Notes = xssPayload
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(response.Content);
        created!.Title.Should().Be(xssPayload, "XSS payloads should be stored as literal strings");
        created.Notes.Should().Be(xssPayload);
    }

    [Fact]
    public async Task GetPassword_WithXSSInData_ReturnsJSONEncoded()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var xssPayload = "<script>alert('xss')</script>";
        var createResponse = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = xssPayload,
            Password = "password"
        });

        var created = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(createResponse.Content);

        // Act
        var getResponse = await _client.GetAsync($"/api/passwords/{created!.Id}");
        var retrieved = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(getResponse.Content);

        // Assert - XSS payload is stored and returned as literal string in valid JSON
        // The API correctly serializes the data as JSON (not HTML), so XSS is prevented
        retrieved!.Title.Should().Be(xssPayload, "XSS payload should be stored and returned as literal string");
        getResponse.Content.Headers.ContentType!.MediaType.Should().Be("application/json", "Response is JSON, not HTML");
    }

    #endregion

    #region Command Injection Prevention

    [Theory]
    [InlineData("test; rm -rf /")]
    [InlineData("test && shutdown /s")]
    [InlineData("test | cat /etc/passwd")]
    [InlineData("test`whoami`")]
    public async Task CreatePassword_WithCommandInjection_TreatsAsLiteralString(string commandPayload)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = commandPayload,
            Password = "password"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(response.Content);
        created!.Title.Should().Be(commandPayload);
    }

    #endregion

    #region Path Traversal Prevention

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32")]
    [InlineData("....//....//....//etc/passwd")]
    public async Task CreatePassword_WithPathTraversal_TreatsAsLiteralString(string pathPayload)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = pathPayload,
            Password = "password"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(response.Content);
        created!.Title.Should().Be(pathPayload);
    }

    #endregion

    #region Null and Empty Validation

    [Fact]
    public async Task CreatePassword_WithNullFields_ValidatesCorrectly()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePasswordEntryRequest
        {
            Title = null!,
            Password = null!
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task CreatePassword_WithWhitespaceOnlyRequired_ReturnsBadRequest(string whitespace)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = whitespace,
            Password = "password"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Length Validation

    [Fact]
    public async Task CreatePassword_WithVeryLongTitle_Succeeds()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var longTitle = new string('A', 10000);

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = longTitle,
            Password = "password"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(response.Content);
        created!.Title.Should().Be(longTitle);
    }

    [Fact]
    public async Task CreatePassword_WithVeryLongPassword_Succeeds()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var longPassword = new string('P', 10000);

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = "Test",
            Password = longPassword
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    #endregion

    #region Unicode and Special Character Validation

    [Theory]
    [InlineData("emoji 🔐💻📱")]
    [InlineData("中文标题")]
    [InlineData("العربية")]
    [InlineData("עברית")]
    [InlineData("Русский")]
    [InlineData("日本語")]
    public async Task CreatePassword_WithUnicodeTitle_Succeeds(string unicodeTitle)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = unicodeTitle,
            Password = "password"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(response.Content);
        created!.Title.Should().Be(unicodeTitle);
    }

    [Theory]
    [InlineData("Special !@#$%^&*()")]
    [InlineData("Quotes \"'`")]
    [InlineData("Brackets []{}()<>")]
    [InlineData("Slashes \\/")]
    [InlineData("Equals === !== <= >=")]
    public async Task CreatePassword_WithSpecialCharacters_Succeeds(string specialTitle)
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = specialTitle,
            Password = "password"
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(response.Content);
        created!.Title.Should().Be(specialTitle);
    }

    #endregion

    #region Content-Type Validation

    [Fact]
    public async Task CreatePassword_WithInvalidContentType_ReturnsBadRequest()
    {
        // Arrange
        var token = await GetAuthTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/passwords");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent("invalid json", System.Text.Encoding.UTF8, "text/plain");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    #endregion

    #region LDAP Injection Prevention

    [Theory]
    [InlineData("*")]
    [InlineData("*(objectClass=*)")]
    [InlineData("admin)(uid=*))(|(uid=*")]
    public async Task Register_WithLDAPInjection_TreatsAsLiteralString(string ldapPayload)
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = ldapPayload,
            Password = "Password123!"
        });

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.BadRequest);
    }

    #endregion
}
