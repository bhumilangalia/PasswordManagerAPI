using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PasswordManagerApi.Data;
using Xunit;

namespace PasswordManagerApi.Tests.Diagnostic;

public class DatabaseConfigurationTest : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DatabaseConfigurationTest(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void DatabaseProvider_ShouldBeInMemory()
    {
        // Arrange & Act
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var providerName = dbContext.Database.ProviderName;

        // Assert
        Assert.NotNull(providerName);
        Assert.Contains("InMemory", providerName);
        Assert.DoesNotContain("Sqlite", providerName);
    }

    [Fact]
    public void DatabaseConnection_ShouldWork()
    {
        // Arrange & Act
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // This should work without errors
        dbContext.Database.EnsureCreated();

        // Assert - if we get here, database configuration is correct
        Assert.True(true);
    }
}
