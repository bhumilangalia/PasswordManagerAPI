using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace PasswordManagerApi.Services;

public class PasswordCipherService(IDataProtectionProvider provider) : IPasswordCipherService
{
    private readonly IDataProtector _protector = provider.CreateProtector("PasswordManagerApi.PasswordCipher.v1");

    public string Encrypt(string plainText)
    {
        return _protector.Protect(plainText);
    }

    public string Decrypt(string cipherText)
    {
        // Validate input
        if (cipherText == null)
        {
            throw new ArgumentNullException(nameof(cipherText), "Encrypted password cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(cipherText))
        {
            throw new CryptographicException("Encrypted password cannot be empty or whitespace.");
        }

        try
        {
            return _protector.Unprotect(cipherText);
        }
        catch (CryptographicException)
        {
            // Re-throw CryptographicException with more context
            // This happens when data is corrupted, tampered with, or encryption keys are unavailable
            throw new CryptographicException("Failed to decrypt password. The data may be corrupted or tampered with.");
        }
    }
}
