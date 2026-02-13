using System.Text.RegularExpressions;
using PasswordManagerApi.DTOs;

namespace PasswordManagerApi.Services;

public class PasswordStrengthService : IPasswordStrengthService
{
    private static readonly HashSet<string> CommonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "123456", "12345678", "qwerty", "abc123", "monkey", "1234567",
        "letmein", "trustno1", "dragon", "baseball", "iloveyou", "master", "sunshine",
        "ashley", "bailey", "passw0rd", "shadow", "123123", "654321", "superman",
        "qazwsx", "michael", "football", "password1", "password123", "admin", "welcome",
        "login", "starwars", "123qwe", "princess", "solo", "photoshop"
    };

    public PasswordStrengthResult ValidatePassword(string password, PasswordValidationMode mode)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return new PasswordStrengthResult(
                DTOs.PasswordStrength.VeryWeak,
                0,
                false,
                new List<string> { "Password cannot be empty." },
                new List<string> { "Create a password with at least 8 characters." }
            );
        }

        var score = CalculateStrengthScore(password, out var feedback);
        var strength = DetermineStrengthLevel(score);
        var isValid = DetermineValidity(strength, mode);
        var suggestions = GenerateSuggestions(password, feedback);

        return new PasswordStrengthResult(strength, score, isValid, feedback, suggestions);
    }

    private int CalculateStrengthScore(string password, out List<string> feedback)
    {
        feedback = new List<string>();
        var score = 0;

        // Length scoring
        if (password.Length < 8)
        {
            feedback.Add("Password is too short (minimum 8 characters).");
        }
        else if (password.Length >= 8 && password.Length < 12)
        {
            score += 20;
        }
        else if (password.Length >= 12)
        {
            score += 30;
        }

        // Character diversity
        var hasLower = password.Any(char.IsLower);
        var hasUpper = password.Any(char.IsUpper);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(ch => !char.IsLetterOrDigit(ch));

        if (hasLower)
        {
            score += 15;
        }
        else
        {
            feedback.Add("Missing lowercase letters.");
        }

        if (hasUpper)
        {
            score += 15;
        }
        else
        {
            feedback.Add("Missing uppercase letters.");
        }

        if (hasDigit)
        {
            score += 15;
        }
        else
        {
            feedback.Add("Missing numbers.");
        }

        if (hasSpecial)
        {
            score += 20;
        }
        else
        {
            feedback.Add("Missing special characters.");
        }

        // Common password penalty
        if (CommonPasswords.Contains(password))
        {
            score -= 30;
            feedback.Add("This is a commonly used password.");
        }

        // Sequential characters penalty (e.g., abc, 123, xyz)
        if (Regex.IsMatch(password, @"(abc|bcd|cde|def|efg|fgh|ghi|hij|ijk|jkl|klm|lmn|mno|nop|opq|pqr|qrs|rst|stu|tuv|uvw|vwx|wxy|xyz|012|123|234|345|456|567|678|789)", RegexOptions.IgnoreCase))
        {
            score -= 15;
            feedback.Add("Contains sequential characters (e.g., abc, 123).");
        }

        // Repeated characters penalty (e.g., aaa, 111)
        if (Regex.IsMatch(password, @"(.)\1{2,}"))
        {
            score -= 10;
            feedback.Add("Contains repeated characters (e.g., aaa, 111).");
        }

        // Ensure score is within 0-100 range
        score = Math.Max(0, Math.Min(100, score));

        return score;
    }

    private DTOs.PasswordStrength DetermineStrengthLevel(int score)
    {
        return score switch
        {
            >= 81 => DTOs.PasswordStrength.VeryStrong,
            >= 61 => DTOs.PasswordStrength.Strong,
            >= 41 => DTOs.PasswordStrength.Medium,
            >= 21 => DTOs.PasswordStrength.Weak,
            _ => DTOs.PasswordStrength.VeryWeak
        };
    }

    private bool DetermineValidity(DTOs.PasswordStrength strength, PasswordValidationMode mode)
    {
        return mode switch
        {
            PasswordValidationMode.UserAccount => strength >= DTOs.PasswordStrength.Medium,
            PasswordValidationMode.PasswordEntry => true,
            _ => false
        };
    }

    private List<string> GenerateSuggestions(string password, List<string> feedback)
    {
        var suggestions = new List<string>();

        if (password.Length < 8)
        {
            suggestions.Add("Use at least 8 characters (12+ recommended).");
        }

        if (!password.Any(char.IsLower) || !password.Any(char.IsUpper))
        {
            suggestions.Add("Mix uppercase and lowercase letters.");
        }

        if (!password.Any(char.IsDigit))
        {
            suggestions.Add("Include numbers.");
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            suggestions.Add("Add special characters (e.g., !@#$%^&*).");
        }

        if (CommonPasswords.Contains(password))
        {
            suggestions.Add("Avoid common passwords. Create a unique phrase.");
        }

        if (feedback.Any(f => f.Contains("sequential") || f.Contains("repeated")))
        {
            suggestions.Add("Avoid predictable patterns like abc or 123.");
        }

        if (!suggestions.Any())
        {
            suggestions.Add("Consider making your password even stronger with more characters.");
        }

        return suggestions;
    }
}
