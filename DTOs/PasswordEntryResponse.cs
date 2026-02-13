namespace PasswordManagerApi.DTOs;

public record PasswordEntryResponse(
    int Id,
    string Title,
    string? LoginUsername,
    string? Website,
    string Password,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    PasswordStrength? PasswordStrength = null);
