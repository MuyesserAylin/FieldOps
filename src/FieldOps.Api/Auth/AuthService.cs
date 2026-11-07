using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Auth;

public sealed record LoginResult(string AccessToken, DateTimeOffset ExpiresAt, string Role);

public sealed class AuthService
{
    private static readonly string DummyHash =
        new PasswordService().Hash("not-a-real-password");

    private readonly AppDbContext _db;
    private readonly PasswordService _passwords;
    private readonly TokenService _tokens;

    public AuthService(AppDbContext db, PasswordService passwords, TokenService tokens)
    {
        _db = db;
        _passwords = passwords;
        _tokens = tokens;
    }

    public async Task<LoginResult?> LoginAsync(
        string username, string password, CancellationToken cancellationToken)
    {
        var normalized = User.NormalizeUserName(username);

        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserName == normalized, cancellationToken);

        if (user is null)
        {
            _passwords.Verify(DummyHash, password);
            return null;
        }

        var passwordMatches = _passwords.Verify(user.PasswordHash, password);
        if (!passwordMatches || !user.IsActive)
        {
            return null;
        }

        var token = _tokens.CreateToken(user);
        return new LoginResult(token.Value, token.ExpiresAt, user.Role.ToString());
    }
}