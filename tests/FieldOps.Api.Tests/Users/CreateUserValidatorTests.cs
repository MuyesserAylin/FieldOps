using FieldOps.Api.Domain;
using FieldOps.Api.Users;

namespace FieldOps.Api.Tests.Users;

public class CreateUserValidatorTests
{
    private const string ValidPassword = "fake-password-1";

    private static CreateUserRequest Request(
        string? username = "fake-user", string? password = ValidPassword, string? role = "Operator")
        => new(username, password, role);

    [Fact]
    public void Validate_ValidRequest_NormalizesUsernameAndParsesRole()
    {
        var (validated, errors) = CreateUserValidator.Validate(
            Request(username: "  Ali.Veli  ", role: "operator"));

        Assert.Empty(errors);
        Assert.NotNull(validated);
        Assert.Equal("ali.veli", validated.UserName);
        Assert.Equal(UserRole.Operator, validated.Role);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("ali veli")]
    [InlineData("ali@veli")]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_InvalidUsername_ReturnsUsernameError(string? username)
    {
        var (validated, errors) = CreateUserValidator.Validate(Request(username: username));

        Assert.Null(validated);
        Assert.Contains("username", errors.Keys);
    }

    [Fact]
    public void Validate_UsernameLongerThan50_ReturnsUsernameError()
    {
        var (validated, errors) = CreateUserValidator.Validate(
            Request(username: new string('a', 51)));

        Assert.Null(validated);
        Assert.Contains("username", errors.Keys);
    }

    [Fact]
    public void Validate_PasswordTooShort_ReturnsPasswordError()
    {
        var (validated, errors) = CreateUserValidator.Validate(
            Request(password: new string('p', 9)));

        Assert.Null(validated);
        Assert.Contains("password", errors.Keys);
    }

    [Fact]
    public void Validate_PasswordTooLong_ReturnsPasswordError()
    {
        var (validated, errors) = CreateUserValidator.Validate(
            Request(password: new string('p', 129)));

        Assert.Null(validated);
        Assert.Contains("password", errors.Keys);
    }

    [Fact]
    public void Validate_PasswordAtBothLimits_IsAccepted()
    {
        var shortest = CreateUserValidator.Validate(Request(password: new string('p', 10)));
        var longest = CreateUserValidator.Validate(Request(password: new string('p', 128)));

        Assert.Empty(shortest.Errors);
        Assert.Empty(longest.Errors);
    }

    [Fact]
    public void Validate_MissingPassword_ReturnsPasswordError()
    {
        var (validated, errors) = CreateUserValidator.Validate(Request(password: null));

        Assert.Null(validated);
        Assert.Contains("password", errors.Keys);
    }

    [Theory]
    [InlineData("Superuser")]
    [InlineData("1")]
    [InlineData(null)]
    public void Validate_InvalidRole_ReturnsRoleError(string? role)
    {
        var (validated, errors) = CreateUserValidator.Validate(Request(role: role));

        Assert.Null(validated);
        Assert.Contains("role", errors.Keys);
    }

    [Fact]
    public void Validate_EverythingWrong_ReportsAllThreeFields()
    {
        var (validated, errors) = CreateUserValidator.Validate(new CreateUserRequest(null, null, null));

        Assert.Null(validated);
        Assert.Equal(3, errors.Count);
    }
}