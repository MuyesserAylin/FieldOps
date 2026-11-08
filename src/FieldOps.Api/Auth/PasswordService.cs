using FieldOps.Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace FieldOps.Api.Auth;

public sealed class PasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _hasher.HashPassword(null!, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        var result = _hasher.VerifyHashedPassword(null!, passwordHash, password);
        return result != PasswordVerificationResult.Failed;
    }
}