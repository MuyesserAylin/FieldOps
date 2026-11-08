using FieldOps.Api.Domain;

namespace FieldOps.Api.Users;

public sealed record ValidatedUser(string UserName, string Password, UserRole Role);

public static class CreateUserValidator
{
    public const int MinUserNameLength = 3;
    public const int MaxUserNameLength = 50;
    public const int MinPasswordLength = 10;
    public const int MaxPasswordLength = 128;

    private const string AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyz0123456789._-";

    public static (ValidatedUser? Validated, Dictionary<string, string> Errors) Validate(
        CreateUserRequest request)
    {
        var errors = new Dictionary<string, string>();

        string? userName = null;
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            errors["username"] = "username is required.";
        }
        else
        {
            userName = User.NormalizeUserName(request.Username);

            if (userName.Length < MinUserNameLength || userName.Length > MaxUserNameLength)
            {
                errors["username"] =
                    $"username must be {MinUserNameLength}-{MaxUserNameLength} characters.";
            }
            else if (userName.Any(c => !AllowedUserNameCharacters.Contains(c)))
            {
                errors["username"] =
                    "username may only contain a-z, 0-9, '.', '_' and '-'.";
            }
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors["password"] = "password is required.";
        }
        else if (request.Password.Length < MinPasswordLength
                 || request.Password.Length > MaxPasswordLength)
        {
            errors["password"] =
                $"password must be {MinPasswordLength}-{MaxPasswordLength} characters.";
        }

        UserRole? role = null;
        if (string.IsNullOrWhiteSpace(request.Role))
        {
            errors["role"] = "role is required.";
        }
        else
        {
            var roleName = Enum.GetNames<UserRole>().FirstOrDefault(name =>
                string.Equals(name, request.Role.Trim(), StringComparison.OrdinalIgnoreCase));

            if (roleName is null)
            {
                errors["role"] =
                    $"role must be one of: {string.Join(", ", Enum.GetNames<UserRole>())}.";
            }
            else
            {
                role = Enum.Parse<UserRole>(roleName);
            }
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new ValidatedUser(userName!, request.Password!, role!.Value), errors);
    }
}