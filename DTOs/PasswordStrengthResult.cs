namespace PasswordManagerApi.DTOs;

public record PasswordStrengthResult(
    PasswordStrength Strength,
    int Score,
    bool IsValid,
    List<string> Feedback,
    List<string> Suggestions
);
