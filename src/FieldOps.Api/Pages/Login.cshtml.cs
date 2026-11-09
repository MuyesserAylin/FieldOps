using System.Security.Claims;
using FieldOps.Api.Auth;
using FieldOps.Api.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FieldOps.Api.Pages;

public sealed class LoginInput
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class LoginModel : PageModel
{
    private readonly AuthService _auth;

    public LoginModel(AuthService auth)
    {
        _auth = auth;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Input.Username) || string.IsNullOrEmpty(Input.Password))
        {
            ErrorMessage = "Kullanıcı adı ve şifre gerekli.";
            return Page();
        }

        var user = await _auth.VerifyAsync(Input.Username, Input.Password, cancellationToken);
        if (user is null)
        {
            ErrorMessage = "Kullanıcı adı veya şifre hatalı.";
            return Page();
        }

        if (user.Role is not (UserRole.Operator or UserRole.Admin))
        {
            ErrorMessage = "Bu panele sadece operatör ve yönetici giriş yapabilir.";
            return Page();
        }

        var identity = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("name", user.UserName),
                new Claim("role", user.Role.ToString())
            ],
            CookieAuthenticationDefaults.AuthenticationScheme,
            nameType: "name",
            roleType: "role");

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToPage("/Operator");
    }
}