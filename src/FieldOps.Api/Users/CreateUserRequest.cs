namespace FieldOps.Api.Users;

public sealed record CreateUserRequest(string? Username, string? Password, string? Role);
