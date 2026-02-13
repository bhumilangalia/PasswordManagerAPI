namespace PasswordManagerApi.DTOs;

public record CreatePasswordEntryRequest
{
    public required string Title { get; init; }
    public required string Password { get; init; }
    public string? LoginUsername { get; init; }
    public string? Website { get; init; }
    public string? Notes { get; init; }
}
