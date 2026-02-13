namespace PasswordManagerApi.DTOs;

public record CreatePasswordEntryRequest(
    string Title,
    string Password,
    string? LoginUsername,
    string? Website,
    string? Notes);
