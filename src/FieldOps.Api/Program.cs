using FieldOps.Api.Data;
using FieldOps.Api.Time;
using FieldOps.Api.Tracking;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
           .UseSnakeCaseNamingConvention());

builder.Services.AddControllers();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ITrackingCodeGenerator, TrackingCodeGenerator>();
builder.Services.AddOpenApi();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
