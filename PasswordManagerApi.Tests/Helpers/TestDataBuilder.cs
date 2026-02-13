using Microsoft.AspNetCore.Identity;
using PasswordManagerApi.Data;
using PasswordManagerApi.Models;
using PasswordManagerApi.Services;

namespace PasswordManagerApi.Tests.Helpers;

public class TestDataBuilder
{
    private readonly AppDbContext _db;
    private readonly IPasswordCipherService _cipher;

    public TestDataBuilder(AppDbContext db, IPasswordCipherService cipher)
    {
        _db = db;
        _cipher = cipher;
    }

    public async Task<AppUser> CreateUserAsync(string username = "testuser", string password = "Test@123")
    {
        var user = new AppUser
        {
            Username = username,
            NormalizedUsername = username.ToLowerInvariant(),
            PasswordHash = new PasswordHasher<AppUser>().HashPassword(null!, password)
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<PasswordEntry> CreatePasswordEntryAsync(
        int userId,
        string password = "Test@123",
        string title = "Test Entry",
        string? loginUsername = "user@example.com",
        string? website = "https://example.com",
        string? notes = "Test notes")
    {
        var entry = new PasswordEntry
        {
            UserId = userId,
            Title = title,
            EncryptedPassword = _cipher.Encrypt(password),
            LoginUsername = loginUsername,
            Website = website,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.PasswordEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<List<PasswordEntry>> CreateMultiplePasswordEntriesAsync(
        int userId,
        int count,
        string baseTitle = "Entry")
    {
        var entries = new List<PasswordEntry>();
        for (int i = 1; i <= count; i++)
        {
            var entry = await CreatePasswordEntryAsync(
                userId,
                password: $"Password{i}@123",
                title: $"{baseTitle} {i}",
                loginUsername: $"user{i}@example.com",
                website: $"https://example{i}.com",
                notes: $"Notes for entry {i}");
            entries.Add(entry);
        }
        return entries;
    }
}
