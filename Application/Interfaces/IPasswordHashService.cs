namespace WorkManagementSystem.Application.Interfaces;

public interface IPasswordHashService
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
    bool VerifyWithDummyHash(string password, string? passwordHash);
    bool NeedsRehash(string passwordHash);
}
