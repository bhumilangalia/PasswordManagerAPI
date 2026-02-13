using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PasswordManagerApi.Data;
using PasswordManagerApi.DTOs;
using PasswordManagerApi.Middleware;
using PasswordManagerApi.Models;
using PasswordManagerApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to suppress Server header (security: prevent information disclosure)
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Database configuration (tests will replace with InMemory in TestWebApplicationFactory)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddDataProtection();
builder.Services.AddScoped<IPasswordCipherService, PasswordCipherService>();
builder.Services.AddScoped<IPasswordStrengthService, PasswordStrengthService>();

// Rate limiting for authentication endpoints (prevent brute force)
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;  // 5 attempts per minute
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;  // Reject immediately
    });

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            message = "Too many requests. Please try again later."
        }, cancellationToken: token);
    };
});

var jwtSection = builder.Configuration.GetSection("Jwt");
// Priority: Environment variable > appsettings.{Environment}.json > error
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? Environment.GetEnvironmentVariable("JWT_SECRET_KEY")
    ?? throw new InvalidOperationException(
        "JWT secret key not configured. Set Jwt:Key in appsettings.json or JWT_SECRET_KEY environment variable.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

var app = builder.Build();

// Skip database initialization during testing (uses InMemory provider)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Production-only: Enforce HTTPS
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new { message = "Password Manager API is running." }));

var authGroup = app.MapGroup("/api/auth")
    .RequireRateLimiting("auth");

authGroup.MapPost("/register", async (
    RegisterRequest request,
    AppDbContext db,
    IPasswordHasher<AppUser> hasher,
    IPasswordStrengthService strengthService) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Username and password are required." });
    }

    var strengthResult = strengthService.ValidatePassword(
        request.Password,
        PasswordValidationMode.UserAccount);

    if (!strengthResult.IsValid)
    {
        return Results.BadRequest(new
        {
            message = "Password does not meet security requirements.",
            strength = strengthResult.Strength.ToString(),
            score = strengthResult.Score,
            feedback = strengthResult.Feedback,
            suggestions = strengthResult.Suggestions
        });
    }

    var normalizedUsername = request.Username.Trim().ToLowerInvariant();
    var exists = await db.Users.AnyAsync(u => u.NormalizedUsername == normalizedUsername);
    if (exists)
    {
        return Results.Conflict(new { message = "Username already exists." });
    }

    var user = new AppUser
    {
        Username = request.Username.Trim(),
        NormalizedUsername = normalizedUsername
    };

    user.PasswordHash = hasher.HashPassword(user, request.Password);
    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/api/users/{user.Id}", new { message = "User registered successfully." });
});

authGroup.MapPost("/login", async (
    LoginRequest request,
    AppDbContext db,
    IPasswordHasher<AppUser> hasher,
    IJwtTokenService tokenService) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Username and password are required." });
    }

    var normalizedUsername = request.Username.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedUsername == normalizedUsername);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var verifyResult = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
    if (verifyResult == PasswordVerificationResult.Failed)
    {
        return Results.Unauthorized();
    }

    var token = tokenService.CreateToken(user);
    return Results.Ok(new AuthResponse(token, DateTime.UtcNow.AddHours(2)));
});

var passwordsGroup = app.MapGroup("/api/passwords").RequireAuthorization();

passwordsGroup.MapGet("/", async (
    ClaimsPrincipal principal,
    AppDbContext db,
    IPasswordCipherService cipher,
    ILogger<Program> logger) =>
{
    var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(userIdClaim, out var userId))
    {
        return Results.Unauthorized();
    }

    var entries = await db.PasswordEntries
        .Where(p => p.UserId == userId)
        .OrderByDescending(p => p.UpdatedAtUtc)
        .ToListAsync();

    try
    {
        var result = entries.Select(e => new PasswordEntryResponse(
            e.Id,
            e.Title,
            e.LoginUsername,
            e.Website,
            cipher.Decrypt(e.EncryptedPassword),
            e.Notes,
            e.CreatedAtUtc,
            e.UpdatedAtUtc,
            null)).ToList();

        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is CryptographicException or ArgumentNullException)
    {
        logger.LogError(ex, "Failed to decrypt password entry for user {UserId}", userId);
        return Results.Problem(
            title: "Decryption Error",
            detail: "Failed to decrypt one or more password entries. The data may be corrupted.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

passwordsGroup.MapGet("/{id:int}", async (
    int id,
    ClaimsPrincipal principal,
    AppDbContext db,
    IPasswordCipherService cipher,
    ILogger<Program> logger) =>
{
    var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(userIdClaim, out var userId))
    {
        return Results.Unauthorized();
    }

    var entry = await db.PasswordEntries.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
    if (entry is null)
    {
        return Results.NotFound(new { message = "Password entry not found." });
    }

    try
    {
        var decryptedPassword = cipher.Decrypt(entry.EncryptedPassword);

        return Results.Ok(new PasswordEntryResponse(
            entry.Id,
            entry.Title,
            entry.LoginUsername,
            entry.Website,
            decryptedPassword,
            entry.Notes,
            entry.CreatedAtUtc,
            entry.UpdatedAtUtc,
            null));
    }
    catch (Exception ex) when (ex is CryptographicException or ArgumentNullException)
    {
        logger.LogError(ex, "Failed to decrypt password entry {EntryId} for user {UserId}", id, userId);
        return Results.Problem(
            title: "Decryption Error",
            detail: "Failed to decrypt password entry. The data may be corrupted.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

passwordsGroup.MapPost("/", async (
    CreatePasswordEntryRequest request,
    ClaimsPrincipal principal,
    AppDbContext db,
    IPasswordCipherService cipher,
    IPasswordStrengthService strengthService) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Title and password are required." });
    }

    var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(userIdClaim, out var userId))
    {
        return Results.Unauthorized();
    }

    var strengthResult = strengthService.ValidatePassword(
        request.Password,
        PasswordValidationMode.PasswordEntry);

    var now = DateTime.UtcNow;
    var entry = new PasswordEntry
    {
        UserId = userId,
        Title = request.Title.Trim(),
        LoginUsername = request.LoginUsername?.Trim(),
        Website = request.Website?.Trim(),
        EncryptedPassword = cipher.Encrypt(request.Password),
        Notes = request.Notes?.Trim(),
        CreatedAtUtc = now,
        UpdatedAtUtc = now
    };

    db.PasswordEntries.Add(entry);
    await db.SaveChangesAsync();

    return Results.Created($"/api/passwords/{entry.Id}", new PasswordEntryResponse(
        entry.Id,
        entry.Title,
        entry.LoginUsername,
        entry.Website,
        request.Password,
        entry.Notes,
        entry.CreatedAtUtc,
        entry.UpdatedAtUtc,
        strengthResult.Strength));
});

passwordsGroup.MapPut("/{id:int}", async (
    int id,
    UpdatePasswordEntryRequest request,
    ClaimsPrincipal principal,
    AppDbContext db,
    IPasswordCipherService cipher,
    IPasswordStrengthService strengthService,
    ILogger<Program> logger) =>
{
    var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(userIdClaim, out var userId))
    {
        return Results.Unauthorized();
    }

    var entry = await db.PasswordEntries.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
    if (entry is null)
    {
        return Results.NotFound(new { message = "Password entry not found." });
    }

    if (!string.IsNullOrWhiteSpace(request.Title))
    {
        entry.Title = request.Title.Trim();
    }

    if (request.LoginUsername is not null)
    {
        entry.LoginUsername = request.LoginUsername.Trim();
    }

    if (request.Website is not null)
    {
        entry.Website = request.Website.Trim();
    }

    if (request.Notes is not null)
    {
        entry.Notes = request.Notes.Trim();
    }

    PasswordStrengthResult? strengthResult = null;
    string? updatedPassword = null;
    if (!string.IsNullOrWhiteSpace(request.Password))
    {
        strengthResult = strengthService.ValidatePassword(
            request.Password,
            PasswordValidationMode.PasswordEntry);
        entry.EncryptedPassword = cipher.Encrypt(request.Password);
        updatedPassword = request.Password; // Use the new plaintext password directly
    }

    entry.UpdatedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();

    // If password was updated, use the new plaintext; otherwise decrypt existing
    string responsePassword;
    if (updatedPassword != null)
    {
        responsePassword = updatedPassword;
    }
    else
    {
        try
        {
            responsePassword = cipher.Decrypt(entry.EncryptedPassword);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentNullException)
        {
            logger.LogError(ex, "Failed to decrypt password entry {EntryId} for user {UserId}", id, userId);
            return Results.Problem(
                title: "Decryption Error",
                detail: "Failed to decrypt password entry. The data may be corrupted.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    return Results.Ok(new PasswordEntryResponse(
        entry.Id,
        entry.Title,
        entry.LoginUsername,
        entry.Website,
        responsePassword,
        entry.Notes,
        entry.CreatedAtUtc,
        entry.UpdatedAtUtc,
        strengthResult?.Strength));
});

passwordsGroup.MapDelete("/{id:int}", async (
    int id,
    ClaimsPrincipal principal,
    AppDbContext db) =>
{
    var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!int.TryParse(userIdClaim, out var userId))
    {
        return Results.Unauthorized();
    }

    var entry = await db.PasswordEntries.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
    if (entry is null)
    {
        return Results.NotFound(new { message = "Password entry not found." });
    }

    db.PasswordEntries.Remove(entry);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

// Make Program class accessible to test project
public partial class Program { }
