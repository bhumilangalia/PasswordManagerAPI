using FluentAssertions;
using PasswordManagerApi.DTOs;
using PasswordManagerApi.Services;
using Xunit;

namespace PasswordManagerApi.Tests.Unit;

public class PasswordStrengthServiceTests
{
    private readonly IPasswordStrengthService _strengthService;

    public PasswordStrengthServiceTests()
    {
        _strengthService = new PasswordStrengthService();
    }

    #region User Account Password Validation (Strict)

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidatePassword_UserAccount_EmptyPassword_IsInvalid(string password)
    {
        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.UserAccount);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Feedback.Should().Contain("required");
    }

    [Theory]
    [InlineData("Short1!")]  // 7 chars
    [InlineData("Pass1!")]   // 6 chars
    public void ValidatePassword_UserAccount_TooShort_IsInvalid(string password)
    {
        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.UserAccount);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Feedback.Should().Contain("8 characters");
    }

    [Theory]
    [InlineData("password123!")]  // No uppercase
    [InlineData("PASSWORD123!")]  // No lowercase
    [InlineData("PasswordABC!")]  // No digit
    [InlineData("Password1234")]  // No special char
    public void ValidatePassword_UserAccount_MissingRequirement_IsInvalid(string password)
    {
        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.UserAccount);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Feedback.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("Password123!", PasswordStrength.Strong)]
    [InlineData("MyP@ssw0rd!", PasswordStrength.Strong)]
    [InlineData("SecurePass123!", PasswordStrength.Strong)]
    public void ValidatePassword_UserAccount_ValidStrongPassword_IsValid(string password, PasswordStrength expectedStrength)
    {
        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.UserAccount);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Strength.Should().Be(expectedStrength);
        result.Score.Should().BeGreaterOrEqualTo(60);
    }

    [Fact]
    public void ValidatePassword_UserAccount_VeryStrongPassword_HasHighScore()
    {
        // Arrange
        var password = "MyV3ry$ecur3P@ssw0rd!2024";

        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.UserAccount);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Strength.Should().Be(PasswordStrength.VeryStrong);
        result.Score.Should().BeGreaterOrEqualTo(80);
    }

    [Theory]
    [InlineData("password")]      // Common password
    [InlineData("12345678")]      // Common pattern
    [InlineData("qwerty123")]     // Common keyboard pattern
    public void ValidatePassword_UserAccount_CommonPassword_HasWarning(string password)
    {
        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.UserAccount);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Suggestions.Should().NotBeEmpty();
    }

    #endregion

    #region Password Entry Validation (Lenient)

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ValidatePassword_PasswordEntry_EmptyPassword_IsInvalid(string password)
    {
        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.PasswordEntry);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("weak")]         // Very weak - 4 chars
    [InlineData("password")]     // Weak - common, no special chars
    [InlineData("Password1")]    // Medium - has requirements but short
    [InlineData("P@ssw0rd123")]  // Strong - all requirements
    [InlineData("MyV3ry$tr0ng!P@ssw0rd")] // Very strong
    public void ValidatePassword_PasswordEntry_AllPasswordsValid_DifferentStrengths(string password)
    {
        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.PasswordEntry);

        // Assert - All should be valid for password entries (lenient mode)
        result.IsValid.Should().BeTrue();
        result.Strength.Should().BeOneOf(PasswordStrength.VeryWeak, PasswordStrength.Weak,
            PasswordStrength.Medium, PasswordStrength.Strong, PasswordStrength.VeryStrong);
    }

    [Fact]
    public void ValidatePassword_PasswordEntry_VeryWeakPassword_StillValid()
    {
        // Arrange
        var password = "1234"; // Very weak password

        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.PasswordEntry);

        // Assert
        result.IsValid.Should().BeTrue("Password entries allow weak passwords");
        result.Strength.Should().Be(PasswordStrength.VeryWeak);
        result.Suggestions.Should().NotBeEmpty("Weak passwords should have suggestions");
    }

    [Fact]
    public void ValidatePassword_PasswordEntry_MediumPassword_IsValid()
    {
        // Arrange
        var password = "MyPass123"; // Medium strength

        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.PasswordEntry);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Strength.Should().Be(PasswordStrength.Medium);
        result.Score.Should().BeInRange(40, 59);
    }

    #endregion

    #region Strength Scoring

    [Theory]
    [InlineData("1234", 0, 19)]                          // Very weak
    [InlineData("password", 20, 39)]                     // Weak
    [InlineData("Password1", 40, 59)]                    // Medium
    [InlineData("P@ssw0rd123", 60, 79)]                 // Strong
    [InlineData("MyV3ry$tr0ng!P@ssw0rd", 80, 100)]     // Very strong
    public void ValidatePassword_ScoreRanges_MatchStrengthLevel(string password, int minScore, int maxScore)
    {
        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.PasswordEntry);

        // Assert
        result.Score.Should().BeInRange(minScore, maxScore);
    }

    [Fact]
    public void ValidatePassword_LongerPassword_HasHigherScore()
    {
        // Arrange
        var shortPassword = "P@ss1";
        var longPassword = "P@ssw0rd123456789!";

        // Act
        var shortResult = _strengthService.ValidatePassword(shortPassword, PasswordValidationMode.PasswordEntry);
        var longResult = _strengthService.ValidatePassword(longPassword, PasswordValidationMode.PasswordEntry);

        // Assert
        longResult.Score.Should().BeGreaterThan(shortResult.Score);
    }

    [Fact]
    public void ValidatePassword_MoreCharacterVariety_HasHigherScore()
    {
        // Arrange
        var simplePassword = "password1234";  // Only lowercase + digits
        var complexPassword = "P@ssw0rd123!"; // Upper + lower + digits + special

        // Act
        var simpleResult = _strengthService.ValidatePassword(simplePassword, PasswordValidationMode.PasswordEntry);
        var complexResult = _strengthService.ValidatePassword(complexPassword, PasswordValidationMode.PasswordEntry);

        // Assert
        complexResult.Score.Should().BeGreaterThan(simpleResult.Score);
    }

    #endregion

    #region Feedback and Suggestions

    [Fact]
    public void ValidatePassword_WeakPassword_ProvidesSuggestions()
    {
        // Arrange
        var password = "weak";

        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.PasswordEntry);

        // Assert
        result.Suggestions.Should().NotBeEmpty();
        result.Suggestions.Should().Contain(s => s.Contains("longer") || s.Contains("characters"));
    }

    [Fact]
    public void ValidatePassword_NoUppercase_SuggestsUppercase()
    {
        // Arrange
        var password = "password123!";

        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.UserAccount);

        // Assert
        result.Feedback.Should().Contain("uppercase");
    }

    [Fact]
    public void ValidatePassword_NoLowercase_SuggestsLowercase()
    {
        // Arrange
        var password = "PASSWORD123!";

        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.UserAccount);

        // Assert
        result.Feedback.Should().Contain("lowercase");
    }

    [Fact]
    public void ValidatePassword_NoDigits_SuggestsDigits()
    {
        // Arrange
        var password = "PasswordABC!";

        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.UserAccount);

        // Assert
        result.Feedback.Should().Contain("digit" );
    }

    [Fact]
    public void ValidatePassword_NoSpecialChars_SuggestsSpecialChars()
    {
        // Arrange
        var password = "Password1234";

        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.UserAccount);

        // Assert
        result.Feedback.Should().Contain("special");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidatePassword_UnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var password = "P@ssw0rd密碼🔐";

        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.PasswordEntry);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ValidatePassword_VeryLongPassword_HandlesCorrectly()
    {
        // Arrange
        var password = new string('A', 1000) + "1!";

        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.PasswordEntry);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Score.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("   Password123!   ")]  // Leading/trailing spaces
    [InlineData("Pass word123!")]       // Space in middle
    public void ValidatePassword_PasswordWithSpaces_HandlesCorrectly(string password)
    {
        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.PasswordEntry);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidatePassword_AllSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var password = "!@#$%^&*()_+-=[]{}|;:,.<>?";

        // Act
        var result = _strengthService.ValidatePassword(password, PasswordValidationMode.PasswordEntry);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ValidatePassword_RepeatingCharacters_LowerScore()
    {
        // Arrange
        var repeating = "aaaaaaaaA1!";
        var varied = "AbCdEfGh1!";

        // Act
        var repeatingResult = _strengthService.ValidatePassword(repeating, PasswordValidationMode.PasswordEntry);
        var variedResult = _strengthService.ValidatePassword(varied, PasswordValidationMode.PasswordEntry);

        // Assert
        variedResult.Score.Should().BeGreaterThan(repeatingResult.Score);
    }

    #endregion

    #region Validation Modes

    [Fact]
    public void ValidatePassword_SamePassword_DifferentModes_DifferentResults()
    {
        // Arrange
        var weakPassword = "simple"; // Doesn't meet user account requirements

        // Act
        var strictResult = _strengthService.ValidatePassword(weakPassword, PasswordValidationMode.UserAccount);
        var lenientResult = _strengthService.ValidatePassword(weakPassword, PasswordValidationMode.PasswordEntry);

        // Assert
        strictResult.IsValid.Should().BeFalse("User account mode is strict");
        lenientResult.IsValid.Should().BeTrue("Password entry mode is lenient");
    }

    [Theory]
    [InlineData(PasswordValidationMode.UserAccount)]
    [InlineData(PasswordValidationMode.PasswordEntry)]
    public void ValidatePassword_EmptyPassword_InvalidRegardlessOfMode(PasswordValidationMode mode)
    {
        // Act
        var result = _strengthService.ValidatePassword("", mode);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    #endregion
}
