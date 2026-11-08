using FieldOps.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace FieldOps.Api.Users;

[ApiController]
[Route("api/users")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class UsersController : ControllerBase
{
    private readonly UserService _users;

    public UsersController(UserService users)
    {
        _users = users;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateUserRequest request, CancellationToken cancellationToken)
    {
        var (validated, errors) = CreateUserValidator.Validate(request);
        if (validated is null)
        {
            return ValidationProblem(ToModelState(errors));
        }

        var result = await _users.CreateAsync(validated, cancellationToken);
        if (result.Outcome == CreateUserOutcome.DuplicateUserName)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Username is already taken.");
        }

        return StatusCode(StatusCodes.Status201Created, result.User);
    }

    private static ModelStateDictionary ToModelState(Dictionary<string, string> errors)
    {
        var modelState = new ModelStateDictionary();
        foreach (var (field, message) in errors)
        {
            modelState.AddModelError(field, message);
        }

        return modelState;
    }
}