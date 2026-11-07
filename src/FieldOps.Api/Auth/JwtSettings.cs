using System;
using System.Text;

namespace FieldOps.Api.Auth;

public sealed class JwtSettings
{
    public const int MinKeyBytes = 32;

    public string Issuer { get; }
    public string Audience { get; }
    public int ExpiryMinutes { get; }
    public byte[] KeyBytes { get; }

    private JwtSettings(string issuer, string audience, int expiryMinutes, byte[] keyBytes)
    {
        Issuer = issuer;
        Audience = audience;
        ExpiryMinutes = expiryMinutes;
        KeyBytes = keyBytes;
    }
    public static JwtSettings FromConfiguration(IConfiguration configuration)
    {
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        var key = configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(issuer))
        {
            throw new InvalidOperationException("Jwt:Issuer is not configured.");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Jwt:Audience is not configured.");
        }

        if (!int.TryParse(configuration["Jwt:ExpiryMinutes"], out var expiryMinutes)
            || expiryMinutes <= 0)
        {
            throw new InvalidOperationException(
                "Jwt:ExpiryMinutes must be a positive number.");
        }

        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                "Jwt:Key is not configured. Set it with user secrets or an environment variable.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(key);
        if (keyBytes.Length < MinKeyBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Key is too short. It must be at least {MinKeyBytes} bytes.");
        }

        return new JwtSettings(issuer, audience, expiryMinutes, keyBytes);
    }
}
