using FieldOps.Api.Auth;
using FieldOps.Api.Data;
using FieldOps.Api.Jobs;
using FieldOps.Api.Reports;
using FieldOps.Api.Time;
using FieldOps.Api.Tracking;
using FieldOps.Api.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.Cookies;
using FieldOps.Api.Teams;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
           .UseSnakeCaseNamingConvention());

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ITrackingCodeGenerator, TrackingCodeGenerator>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<JobStatusService>();
var jwtSettings = JwtSettings.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UserSeeder>();
builder.Services.AddScoped<DemoTeamSeeder>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<OperatorJobService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtSettings.KeyBytes),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Authentication is required.")
                    .ExecuteAsync(context.HttpContext);
            },
            OnForbidden = async context =>
            {
                await Results.Problem(
                        statusCode: StatusCodes.Status403Forbidden,
                        title: "You do not have permission to do this.")
                    .ExecuteAsync(context.HttpContext);
            }
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.Cookie.Name = "fieldops.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(jwtSettings.ExpiryMinutes);
        options.SlidingExpiration = false;
    });

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<UserSeeder>()
        .SeedAsync(CancellationToken.None);

    await scope.ServiceProvider.GetRequiredService<DemoTeamSeeder>()
        .SeedAsync(CancellationToken.None);
}

app.UseExceptionHandler();

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en-US")
    .AddSupportedCultures("en-US")
    .AddSupportedUICultures("en-US"));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapRazorPages();

app.Run();
public partial class Program { }
