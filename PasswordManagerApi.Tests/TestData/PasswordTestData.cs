namespace PasswordManagerApi.Tests.TestData;

public static class PasswordTestData
{
    /// <summary>
    /// Collection of valid passwords for testing encryption/decryption.
    /// Includes various edge cases: special characters, Unicode, SQL injection attempts, XSS, long passwords.
    /// </summary>
    public static IEnumerable<string> ValidPasswords => new[]
    {
        "Simple123!",
        "P@$$w0rd",
        "密碼🔐Test",
        "Пароль123!",
        "<script>alert('xss')</script>",
        "'; DROP TABLE Users; --",
        "Test@123<>&\"'",
        new string('a', 10000), // Very long password
        "", // Empty password
        "   Leading and trailing spaces   "
    };

    /// <summary>
    /// Collection of special character passwords to test character encoding.
    /// </summary>
    public static IEnumerable<string> SpecialCharacterPasswords => new[]
    {
        "P@$$w0rd!<>&\"'",
        "<script>alert('xss')</script>",
        "'; DROP TABLE Users; --",
        "Test\nNewline\tTab",
        "Test\\Backslash/Forward",
        "Emoji🔐🔑🛡️"
    };

    /// <summary>
    /// Collection of Unicode passwords from different scripts.
    /// </summary>
    public static IEnumerable<string> UnicodePasswords => new[]
    {
        "密碼🔐Test",
        "Пароль123!",
        "مرور کلمہ",
        "パスワード123",
        "비밀번호123",
        "🔐🔑🛡️🔒"
    };

    /// <summary>
    /// Collection of corrupted/invalid encrypted data for testing error handling.
    /// </summary>
    public static IEnumerable<string> CorruptedData => new[]
    {
        "NotValidBase64!@#$",
        "CorruptedEncryptedData",
        "InvalidCipherText",
        "AAAA", // Valid base64 but too short
        Convert.ToBase64String(Guid.NewGuid().ToByteArray()) // Valid base64, wrong data
    };

    /// <summary>
    /// Collection of invalid inputs for validation testing.
    /// </summary>
    public static IEnumerable<string?> InvalidInputs => new string?[]
    {
        null,
        "",
        "   ",
        "\t\n\r"
    };
}
