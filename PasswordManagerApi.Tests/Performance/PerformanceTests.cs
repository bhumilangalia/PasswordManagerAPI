using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using PasswordManagerApi.DTOs;
using PasswordManagerApi.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace PasswordManagerApi.Tests.Performance;

[Collection("Sequential")]
public class PerformanceTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;

    public PerformanceTests(TestWebApplicationFactory factory, ITestOutputHelper output)
    {
        _client = factory.CreateClient();
        _output = output;
    }

    private async Task<string> GetAuthTokenAsync()
    {
        var username = $"perfuser_{Guid.NewGuid()}";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Password = "PerfTest123!"
        });

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = "PerfTest123!"
        });

        var auth = await JsonHelper.DeserializeAsync<AuthResponse>(loginResponse.Content);
        return auth!.Token;
    }

    [Fact]
    public async Task GetAllPasswords_With100Entries_CompletesQuickly()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create 100 password entries
        for (int i = 0; i < 100; i++)
        {
            await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
            {
                Title = $"Entry {i}",
                Password = $"Password{i}123!"
            });
        }

        // Act
        var sw = Stopwatch.StartNew();
        var response = await _client.GetAsync("/api/passwords");
        sw.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var passwords = await JsonHelper.DeserializeAsync<List<PasswordEntryResponse>>(response.Content);
        passwords.Should().HaveCount(100);

        _output.WriteLine($"Time to retrieve 100 entries: {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(5000, "Should retrieve 100 entries in under 5 seconds");
    }

    [Fact]
    public async Task CreatePassword_ResponseTime_IsAcceptable()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var request = new CreatePasswordEntryRequest
        {
            Title = "Performance Test",
            Password = "TestPassword123!"
        };

        // Act
        var sw = Stopwatch.StartNew();
        var response = await _client.PostAsJsonAsync("/api/passwords", request);
        sw.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _output.WriteLine($"Create password response time: {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(1000, "Create should complete in under 1 second");
    }

    [Fact]
    public async Task Login_ResponseTime_IsAcceptable()
    {
        // Arrange
        var username = $"loginperf_{Guid.NewGuid()}";
        var password = "LoginPerf123!";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Username = username,
            Password = password
        });

        // Act
        var sw = Stopwatch.StartNew();
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Username = username,
            Password = password
        });
        sw.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _output.WriteLine($"Login response time: {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(1000, "Login should complete in under 1 second");
    }

    [Fact]
    public async Task Register_ResponseTime_IsAcceptable()
    {
        // Arrange
        var username = $"regperf_{Guid.NewGuid()}";
        var request = new RegisterRequest
        {
            Username = username,
            Password = "RegPerf123!"
        };

        // Act
        var sw = Stopwatch.StartNew();
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        sw.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _output.WriteLine($"Register response time: {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(2000, "Register should complete in under 2 seconds (includes password hashing)");
    }

    [Fact]
    public async Task UpdatePassword_ResponseTime_IsAcceptable()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = "Update Test",
            Password = "Original123!"
        });

        var created = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(createResponse.Content);

        var updateRequest = new UpdatePasswordEntryRequest
        {
            Title = "Updated Title",
            Password = "Updated123!"
        };

        // Act
        var sw = Stopwatch.StartNew();
        var response = await _client.PutAsJsonAsync($"/api/passwords/{created!.Id}", updateRequest);
        sw.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _output.WriteLine($"Update password response time: {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(1000, "Update should complete in under 1 second");
    }

    [Fact]
    public async Task DeletePassword_ResponseTime_IsAcceptable()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var createResponse = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = "Delete Test",
            Password = "Delete123!"
        });

        var created = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(createResponse.Content);

        // Act
        var sw = Stopwatch.StartNew();
        var response = await _client.DeleteAsync($"/api/passwords/{created!.Id}");
        sw.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        _output.WriteLine($"Delete password response time: {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(500, "Delete should complete in under 500ms");
    }

    [Fact]
    public async Task ConcurrentCreates_Handle10SimultaneousRequests()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var tasks = new List<Task<HttpResponseMessage>>();

        // Act
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 10; i++)
        {
            var task = _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
            {
                Title = $"Concurrent {i}",
                Password = $"Password{i}123!"
            });

            tasks.Add(task);
        }

        var responses = await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.Created));
        _output.WriteLine($"10 concurrent creates completed in: {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(5000, "10 concurrent requests should complete in under 5 seconds");
    }

    [Fact]
    public async Task PasswordEncryptionDecryption_PerformanceIsAcceptable()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Create password (encrypts)
        var createSw = Stopwatch.StartNew();
        var createResponse = await _client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
        {
            Title = "Encryption Test",
            Password = "VeryLongPasswordToTestEncryptionPerformance123!@#"
        });
        createSw.Stop();

        var created = await JsonHelper.DeserializeAsync<PasswordEntryResponse>(createResponse.Content);

        // Act - Retrieve password (decrypts)
        var getSw = Stopwatch.StartNew();
        var getResponse = await _client.GetAsync($"/api/passwords/{created!.Id}");
        getSw.Stop();

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        _output.WriteLine($"Encryption time: {createSw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Decryption time: {getSw.ElapsedMilliseconds}ms");

        createSw.ElapsedMilliseconds.Should().BeLessThan(1000, "Encryption should be fast");
        getSw.ElapsedMilliseconds.Should().BeLessThan(500, "Decryption should be fast");
    }

    [Fact]
    public async Task GetAllPasswords_WithManyEntries_ReturnsInReasonableTime()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create 50 entries
        var createTasks = new List<Task>();
        for (int i = 0; i < 50; i++)
        {
            createTasks.Add(_client.PostAsJsonAsync("/api/passwords", new CreatePasswordEntryRequest
            {
                Title = $"Bulk Entry {i}",
                Password = $"BulkPassword{i}123!",
                Notes = $"Notes for entry {i}"
            }));
        }
        await Task.WhenAll(createTasks);

        // Act - Retrieve all (decrypts all)
        var sw = Stopwatch.StartNew();
        var response = await _client.GetAsync("/api/passwords");
        sw.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var passwords = await JsonHelper.DeserializeAsync<List<PasswordEntryResponse>>(response.Content);
        passwords.Should().HaveCountGreaterOrEqualTo(50);

        _output.WriteLine($"Time to decrypt 50+ passwords: {sw.ElapsedMilliseconds}ms");
        sw.ElapsedMilliseconds.Should().BeLessThan(3000, "Bulk decryption should complete in reasonable time");
    }

    [Fact]
    public async Task RateLimiter_DoesNotSignificantlySlowDownNormalRequests()
    {
        // Arrange
        var username = $"ratelimitperf_{Guid.NewGuid()}";

        // Act - Make 3 requests (well under rate limit)
        var times = new List<long>();

        for (int i = 0; i < 3; i++)
        {
            var sw = Stopwatch.StartNew();
            var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Username = username,
                Password = "Password123!"
            });
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
        }

        // Assert
        var avgTime = times.Average();
        _output.WriteLine($"Average response time with rate limiter: {avgTime}ms");
        avgTime.Should().BeLessThan(500, "Rate limiter should not significantly impact normal requests");
    }

    [Fact]
    public async Task SecurityHeaders_DoNotSignificantlyImpactPerformance()
    {
        // Arrange
        var token = await GetAuthTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act - Make multiple requests and measure
        var times = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var sw = Stopwatch.StartNew();
            await _client.GetAsync("/api/passwords");
            sw.Stop();
            times.Add(sw.ElapsedMilliseconds);
        }

        // Assert
        var avgTime = times.Average();
        _output.WriteLine($"Average GET time with security headers: {avgTime}ms");
        avgTime.Should().BeLessThan(500, "Security headers should have minimal performance impact");
    }
}
