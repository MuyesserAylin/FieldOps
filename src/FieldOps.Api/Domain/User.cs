using System;

namespace FieldOps.Api.Domain;

public class User
{
    private User() { }

    public User(
        string userName,
        string passwordHash,
        UserRole role,
        DateTimeOffset createdAt,
        bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        UserName = NormalizeUserName(userName);
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = createdAt;
        IsActive = isActive;
    }

    public static string NormalizeUserName(string username)
       => username.Trim().ToLowerInvariant();


    public int Id { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

}
