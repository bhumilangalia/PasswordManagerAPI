namespace PasswordManagerApi.DTOs;

public record AuthResponse(string Token, DateTime ExpiresAtUtc);
