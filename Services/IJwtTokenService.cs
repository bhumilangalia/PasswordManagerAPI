using PasswordManagerApi.Models;

namespace PasswordManagerApi.Services;

public interface IJwtTokenService
{
    string CreateToken(AppUser user);
}
