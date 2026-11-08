using FieldOps.Api.Auth;
using FieldOps.Api.Data;
using FieldOps.Api.Domain;
using FieldOps.Api.Time;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FieldOps.Api.Users;

public enum CreateUserOutcome
{
    Created,
    DuplicateUserName
}

public sealed record CreatedUser(
    int Id, string Username, string Role, bool IsActive, DateTimeOffset CreatedAt);

public sealed record CreateUserResult(CreateUserOutcome Outcome, CreatedUser? User = null)
{
    public static CreateUserResult Created(CreatedUser user)
        => new(CreateUserOutcome.Created, user);

    public static CreateUserResult DuplicateUserName()
        => new(CreateUserOutcome.DuplicateUserName);
}

public sealed class UserService
{
    private readonly AppDbContext _db;
    private readonly PasswordService _passwords;
    private readonly IClock _clock;

    public UserService(AppDbContext db, PasswordService passwords, IClock clock)
    {
        _db = db;
        _passwords = passwords;
        _clock = clock;
    }

    public async Task<CreateUserResult> CreateAsync(
        ValidatedUser validated, CancellationToken cancellationToken)
    {
        if (await _db.Users.AnyAsync(u => u.UserName == validated.UserName, cancellationToken))
        {
            return CreateUserResult.DuplicateUserName();
        }

        var hash = _passwords.Hash(validated.Password);
        var user = new User(validated.UserName, hash, validated.Role, _clock.UtcNow);
        _db.Users.Add(user);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUserNameCollision(ex))
        {
            return CreateUserResult.DuplicateUserName();
        }

        return CreateUserResult.Created(new CreatedUser(
            user.Id, user.UserName, user.Role.ToString(), user.IsActive, user.CreatedAt));
    }

    private static bool IsUserNameCollision(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ix_users_user_name"
        };
    }
}