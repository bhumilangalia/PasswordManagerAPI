using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using PasswordManagerApi.Services;
using PasswordManagerApi.Tests.TestData;
using Xunit;

namespace PasswordManagerApi.Tests.Unit;

public class PasswordCipherServiceTests
{
    private readonly IPasswordCipherService _cipherService;

    public PasswordCipherServiceTests()
    {
        // Create a real DataProtection provider for testing
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDataProtection();
        var services = serviceCollection.BuildServiceProvider();
        var dataProtectionProvider = services.GetRequiredService<IDataProtectionProvider>();
        _cipherService = new PasswordCipherService(dataProtectionProvider);
    }

    [Fact]
    public void EncryptDecrypt_WithValidPassword_ReturnsOriginalPlaintext()
    {
        // Arrange
        var plaintext = "Test@123";

        // Act
        var encrypted = _cipherService.Encrypt(plaintext);
        var decrypted = _cipherService.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(plaintext);
        encrypted.Should().NotBe(plaintext); // Encrypted should be different
    }

    [Fact]
    public void Decrypt_WithNullInput_ThrowsArgumentNullException()
    {
        // Arrange
        string? nullInput = null;

        // Act
        Action act = () => _cipherService.Decrypt(nullInput!);

        // Assert
        // EXPECTED TO FAIL: No null check in PasswordCipherService.Decrypt
        // Current implementation will throw from IDataProtector.Unprotect
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Decrypt_WithEmptyString_ThrowsCryptographicException()
    {
        // Arrange
        var emptyInput = "";

        // Act
        Action act = () => _cipherService.Decrypt(emptyInput);

        // Assert
        // EXPECTED TO FAIL: No validation for empty strings
        // IDataProtector.Unprotect will throw CryptographicException
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_WithWhitespace_ThrowsCryptographicException()
    {
        // Arrange
        var whitespaceInput = "   ";

        // Act
        Action act = () => _cipherService.Decrypt(whitespaceInput);

        // Assert
        // EXPECTED TO FAIL: No validation for whitespace
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ThrowsCryptographicException()
    {
        // Arrange
        var invalidBase64 = "NotValidBase64!@#$";

        // Act
        Action act = () => _cipherService.Decrypt(invalidBase64);

        // Assert
        // EXPECTED TO FAIL: No error handling around Unprotect call
        // This will throw but exception may not be handled properly
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Decrypt_WithTamperedData_ThrowsCryptographicException()
    {
        // Arrange
        var validPassword = "Test@123";
        var encrypted = _cipherService.Encrypt(validPassword);

        // Tamper with the encrypted data by modifying last character
        var tampered = encrypted.Substring(0, encrypted.Length - 1) + "X";

        // Act
        Action act = () => _cipherService.Decrypt(tampered);

        // Assert
        // EXPECTED TO FAIL: No error handling for data integrity failures
        // IDataProtector should detect tampering and throw CryptographicException
        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void EncryptDecrypt_WithSpecialCharacters_PreservesData()
    {
        // Arrange
        var password = "P@$$w0rd!<>&\"'";

        // Act
        var encrypted = _cipherService.Encrypt(password);
        var decrypted = _cipherService.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(password);
    }

    [Fact]
    public void EncryptDecrypt_WithUnicodeCharacters_PreservesData()
    {
        // Arrange
        var password = "密碼🔐Пароль";

        // Act
        var encrypted = _cipherService.Encrypt(password);
        var decrypted = _cipherService.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(password);
    }

    [Fact]
    public void EncryptDecrypt_WithVeryLongPassword_HandlesCorrectly()
    {
        // Arrange
        var password = new string('a', 10000);

        // Act
        var encrypted = _cipherService.Encrypt(password);
        var decrypted = _cipherService.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(password);
        decrypted.Length.Should().Be(10000);
    }

    [Fact]
    public void EncryptDecrypt_WithEmptyPassword_RoundTripsCorrectly()
    {
        // Arrange
        var password = "";

        // Act
        var encrypted = _cipherService.Encrypt(password);
        var decrypted = _cipherService.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(password);
        decrypted.Should().BeEmpty();
    }

    [Fact]
    public void Encrypt_SamePasswordTwice_ProducesDifferentCiphertext()
    {
        // Arrange
        var password = "password123";

        // Act
        var encrypted1 = _cipherService.Encrypt(password);
        var encrypted2 = _cipherService.Encrypt(password);

        // Assert
        // Note: ASP.NET Data Protection may or may not use random IVs
        // If it does, encrypted values should differ
        // Both should decrypt to same plaintext
        var decrypted1 = _cipherService.Decrypt(encrypted1);
        var decrypted2 = _cipherService.Decrypt(encrypted2);

        decrypted1.Should().Be(password);
        decrypted2.Should().Be(password);

        // This assertion may fail if Data Protection uses deterministic encryption
        // That's okay - the test documents the behavior
        encrypted1.Should().NotBe(encrypted2, "encryption should use random IVs for security");
    }

    [Theory]
    [MemberData(nameof(GetValidPasswords))]
    public void EncryptDecrypt_WithVariousValidPasswords_PreservesData(string password)
    {
        // Act
        var encrypted = _cipherService.Encrypt(password);
        var decrypted = _cipherService.Decrypt(encrypted);

        // Assert
        decrypted.Should().Be(password);
    }

    public static IEnumerable<object[]> GetValidPasswords()
    {
        return PasswordTestData.ValidPasswords
            .Where(p => p != null) // Exclude null for this test
            .Select(p => new object[] { p });
    }
}
