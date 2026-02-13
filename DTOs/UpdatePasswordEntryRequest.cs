namespace PasswordManagerApi.DTOs;

public record UpdatePasswordEntryRequest(
    string? Title,
    string? Password,
    string? LoginUsername,
    string? Website,
    string? Notes);
