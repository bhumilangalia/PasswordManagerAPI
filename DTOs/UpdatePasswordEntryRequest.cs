namespace PasswordManagerApi.DTOs;

public record UpdatePasswordEntryRequest
{
    public string? Title { get; init; }
    public string? Password { get; init; }
    public string? LoginUsername { get; init; }
    public string? Website { get; init; }
    public string? Notes { get; init; }
}
