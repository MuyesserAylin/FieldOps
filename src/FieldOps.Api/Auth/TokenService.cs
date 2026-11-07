using System.Security.Claims;
using FieldOps.Api.Domain;
using FieldOps.Api.Time;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FieldOps.Api.Auth;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public sealed class TokenService
{
    private readonly JwtSettings _settings;
    private readonly IClock _clock;

    public TokenService(JwtSettings settings, IClock clock)
    {
        _settings = settings;
        _clock = clock;
    }

    public AccessToken CreateToken(User user)
    {
        var now = _clock.UtcNow;
        var expiresAt = now.AddMinutes(_settings.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("name", user.UserName),
                new Claim("role", user.Role.ToString())
            ]),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_settings.KeyBytes),
                SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expiresAt);
    }
}