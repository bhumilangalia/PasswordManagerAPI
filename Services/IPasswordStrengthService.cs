using PasswordManagerApi.DTOs;

namespace PasswordManagerApi.Services;

public interface IPasswordStrengthService
{
    PasswordStrengthResult ValidatePassword(string password, PasswordValidationMode mode);
}
