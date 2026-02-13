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
            // Remove ALL DbContext-related registrations
            // This prevents the "multiple database providers" error
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll(typeof(AppDbContext));

            // Add in-memory database for testing
            // Each test gets a unique database to ensure complete isolation
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}");
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
