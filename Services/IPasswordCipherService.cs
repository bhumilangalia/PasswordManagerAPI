namespace PasswordManagerApi.Services;

public interface IPasswordCipherService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}
