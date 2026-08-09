using WorkManagementSystem.Application.Interfaces;
using WorkManagementSystem.Application.Common;
using System.Text;

namespace WorkManagementSystem.Infrastructure.Security;

public sealed class BcryptPasswordHashService : IPasswordHashService
{
    public const int WorkFactor = 12;

    private static readonly string DummyHash =
        BCrypt.Net.BCrypt.HashPassword("authentication-timing-placeholder", WorkFactor);

    public string Hash(string password)
    {
        PasswordPolicy.EnsureValid(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) ||
            string.IsNullOrEmpty(passwordHash) ||
            Encoding.UTF8.GetByteCount(password) > PasswordPolicy.MaximumUtf8Bytes)
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException or BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

    public bool VerifyWithDummyHash(string password, string? passwordHash)
        => Verify(password, string.IsNullOrWhiteSpace(passwordHash) ? DummyHash : passwordHash);

    public bool NeedsRehash(string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.PasswordNeedsRehash(passwordHash, WorkFactor);
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException or BCrypt.Net.SaltParseException)
        {
            return true;
        }
    }
}
