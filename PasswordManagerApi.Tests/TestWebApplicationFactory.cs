using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PasswordManagerApi.Data;

namespace PasswordManagerApi.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Add test JWT configuration with high priority
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear existing sources and add our test configuration first
            config.Sources.Clear();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "TestJwtSecretKey12345678901234567890123456789012345678901234567890",
                ["Jwt:Issuer"] = "PasswordManagerApi",
                ["Jwt:Audience"] = "PasswordManagerApiUsers"
            });
            // Add default configuration sources back
            config.AddJsonFile("appsettings.json", optional: true);
            config.AddEnvironmentVariables();
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL Entity Framework Core service registrations
            // This includes DbContext, DbContextOptions, and any EF Core internal services
            var descriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    (d.ServiceType.Namespace != null && d.ServiceType.Namespace.StartsWith("Microsoft.EntityFrameworkCore")))
                .ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            // Register ONLY InMemory database
            // Use unique database name per test for complete isolation
            var databaseName = $"TestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>(options =>
            {
                // Clear any existing configurations
                options.UseInMemoryDatabase(databaseName);
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });

            // Keep real DataProtection for actual encryption/decryption testing
        });
    }

    public AppDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }
}
