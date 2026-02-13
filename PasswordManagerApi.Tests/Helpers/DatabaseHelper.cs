using Microsoft.EntityFrameworkCore;
using PasswordManagerApi.Data;

namespace PasswordManagerApi.Tests.Helpers;

public static class DatabaseHelper
{
    /// <summary>
    /// Corrupts a password entry's encrypted password with invalid data.
    /// This simulates database corruption or tampering.
    /// </summary>
    public static async Task CorruptPasswordEntryAsync(AppDbContext db, int entryId)
    {
        var entry = await db.PasswordEntries.FindAsync(entryId);
        if (entry != null)
        {
            // Set to invalid base64 that will fail decryption
            entry.EncryptedPassword = "CorruptedData!@#$NotValidBase64";
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Sets a password entry's encrypted password to null.
    /// This tests null validation in decryption.
    /// </summary>
    public static async Task SetNullPasswordAsync(AppDbContext db, int entryId)
    {
        var entry = await db.PasswordEntries.FindAsync(entryId);
        if (entry != null)
        {
            entry.EncryptedPassword = null!; // Force null to test error handling
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Sets a password entry's encrypted password to an empty string.
    /// This tests empty string validation in decryption.
    /// </summary>
    public static async Task SetEmptyPasswordAsync(AppDbContext db, int entryId)
    {
        var entry = await db.PasswordEntries.FindAsync(entryId);
        if (entry != null)
        {
            entry.EncryptedPassword = "";
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Sets a password entry's encrypted password to whitespace.
    /// This tests whitespace validation in decryption.
    /// </summary>
    public static async Task SetWhitespacePasswordAsync(AppDbContext db, int entryId)
    {
        var entry = await db.PasswordEntries.FindAsync(entryId);
        if (entry != null)
        {
            entry.EncryptedPassword = "   ";
            await db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Tampers with encrypted data by modifying characters.
    /// This simulates data corruption or tampering that should fail integrity checks.
    /// </summary>
    public static async Task TamperWithEncryptedPasswordAsync(AppDbContext db, int entryId, string encryptedPassword)
    {
        var entry = await db.PasswordEntries.FindAsync(entryId);
        if (entry != null)
        {
            // Modify the last character to break integrity
            if (encryptedPassword.Length > 0)
            {
                var tampered = encryptedPassword.Substring(0, encryptedPassword.Length - 1) + "X";
                entry.EncryptedPassword = tampered;
                await db.SaveChangesAsync();
            }
        }
    }
}
