using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using FieldOps.Api.Time;
using Microsoft.EntityFrameworkCore;

namespace FieldOps.Api.Auth;

public sealed class UserSeeder
{
    private readonly AppDbContext _db;
    private readonly PasswordService _passwords;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;

    public UserSeeder(
        AppDbContext db,
        PasswordService passwords,
        IConfiguration configuration,
        IClock clock)
    {
        _db = db;
        _passwords = passwords;
        _configuration = configuration;
        _clock = clock;
    }

    public async Task<bool> SeedAsync(CancellationToken cancellationToken)
    {
        var username = _configuration["Seed:AdminUsername"];
        var password = _configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        if (await _db.Users.AnyAsync(cancellationToken))
        {
            return false;
        }

        var hash = _passwords.Hash(password);
        _db.Users.Add(new User(username, hash, UserRole.Admin, _clock.UtcNow));
        await _db.SaveChangesAsync(cancellationToken);

        return true;
    }
}