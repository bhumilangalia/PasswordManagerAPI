namespace PasswordManagerApi.Models;

public class PasswordEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? LoginUsername { get; set; }
    public string? Website { get; set; }
    public string EncryptedPassword { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
