using System.Net;
using FluentAssertions;
using Xunit;

namespace PasswordManagerApi.Tests.Integration;

[Collection("Sequential")]
public class SecurityHeadersTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityHeadersTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RootEndpoint_ShouldNotExposeServerHeader()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Headers.Contains("Server").Should().BeFalse(
            "Server header exposes technology stack and should be removed for security");
    }

    [Fact]
    public async Task ApiEndpoint_ShouldNotExposeServerHeader()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/passwords");

        // Assert
        response.Headers.Contains("Server").Should().BeFalse(
            "Server header exposes technology stack and should be removed for security");
    }

    [Fact]
    public async Task Response_ShouldNotExposeXPoweredByHeader()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Headers.Contains("X-Powered-By").Should().BeFalse(
            "X-Powered-By header exposes technology information and should be removed");
    }

    [Fact]
    public async Task Response_ShouldIncludeXContentTypeOptionsNoSniff()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Headers.TryGetValues("X-Content-Type-Options", out var values).Should().BeTrue();
        values.Should().Contain("nosniff", "X-Content-Type-Options prevents MIME type sniffing");
    }

    [Fact]
    public async Task Response_ShouldIncludeXFrameOptionsDeny()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Headers.TryGetValues("X-Frame-Options", out var values).Should().BeTrue();
        values.Should().Contain("DENY", "X-Frame-Options prevents clickjacking attacks");
    }

    [Fact]
    public async Task Response_ShouldIncludeXXssProtection()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Headers.TryGetValues("X-XSS-Protection", out var values).Should().BeTrue();
        values.Should().Contain("1; mode=block", "X-XSS-Protection provides defense in depth");
    }

    [Fact]
    public async Task Response_ShouldIncludeContentSecurityPolicy()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Headers.TryGetValues("Content-Security-Policy", out var values).Should().BeTrue();
        values.Should().Contain(v => v.Contains("default-src 'none'"),
            "CSP should restrict all sources by default");
        values.Should().Contain(v => v.Contains("frame-ancestors 'none'"),
            "CSP should prevent framing");
    }

    [Fact]
    public async Task Response_ShouldIncludeReferrerPolicyNoReferrer()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Headers.TryGetValues("Referrer-Policy", out var values).Should().BeTrue();
        values.Should().Contain("no-referrer", "Referrer-Policy prevents information leakage");
    }

    [Fact]
    public async Task Response_ShouldIncludePermissionsPolicy()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/");

        // Assert
        response.Headers.TryGetValues("Permissions-Policy", out var values).Should().BeTrue();
        values.Should().Contain(v => v.Contains("geolocation=()"),
            "Permissions-Policy should disable geolocation");
        values.Should().Contain(v => v.Contains("microphone=()"),
            "Permissions-Policy should disable microphone");
        values.Should().Contain(v => v.Contains("camera=()"),
            "Permissions-Policy should disable camera");
    }

    [Fact]
    public async Task AllEndpoints_ShouldHaveConsistentSecurityHeaders()
    {
        // Arrange
        var endpoints = new[]
        {
            "/",
            "/api/passwords",
            "/api/auth/login",
            "/api/auth/register"
        };

        // Act & Assert
        foreach (var endpoint in endpoints)
        {
            var response = await _client.GetAsync(endpoint);

            // Verify all security headers are present on every endpoint
            response.Headers.Contains("Server").Should().BeFalse($"Server header should not be present on {endpoint}");
            response.Headers.TryGetValues("X-Content-Type-Options", out _).Should().BeTrue($"X-Content-Type-Options missing on {endpoint}");
            response.Headers.TryGetValues("X-Frame-Options", out _).Should().BeTrue($"X-Frame-Options missing on {endpoint}");
            response.Headers.TryGetValues("Content-Security-Policy", out _).Should().BeTrue($"CSP missing on {endpoint}");
            response.Headers.TryGetValues("Referrer-Policy", out _).Should().BeTrue($"Referrer-Policy missing on {endpoint}");
        }
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task AllHttpMethods_ShouldIncludeSecurityHeaders(string method)
    {
        // Arrange
        var request = new HttpRequestMessage(new HttpMethod(method), "/");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.Headers.Contains("Server").Should().BeFalse(
            $"Server header should not be present for {method} requests");
        response.Headers.TryGetValues("X-Content-Type-Options", out _).Should().BeTrue(
            $"Security headers should be present for {method} requests");
    }

    [Fact]
    public async Task ErrorResponses_ShouldStillIncludeSecurityHeaders()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/nonexistent-endpoint");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Headers.Contains("Server").Should().BeFalse(
            "Server header should not be present even on error responses");
        response.Headers.TryGetValues("X-Content-Type-Options", out _).Should().BeTrue(
            "Security headers should be present even on error responses");
    }
}
