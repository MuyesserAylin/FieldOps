using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FieldOps.Api.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrEmpty(request.Password))
        {
            var errors = new ModelStateDictionary();
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                errors.AddModelError("username", "username is required.");
            }

            if (string.IsNullOrEmpty(request.Password))
            {
                errors.AddModelError("password", "password is required.");
            }

            return ValidationProblem(errors);
        }

        var result = await _auth.LoginAsync(
            request.Username, request.Password, cancellationToken);

        if (result is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid username or password.");
        }

        return Ok(result);
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new MeResponse(
            User.FindFirst("name")?.Value ?? string.Empty,
            User.FindFirst("role")?.Value ?? string.Empty));
    }
}
public sealed record MeResponse(string Username, string Role);