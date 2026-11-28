using System;
using FieldOps.Api.Data;
using FieldOps.Api.Teams;
using FieldOps.Api.Time;
using FieldOps.Api.Tracking;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FieldOps.Api.Tests.Reports;

public class FieldOpsApiFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "test-only-fake-jwt-key-0123456789-abcdef";
    private readonly string _connectionString;
    private readonly ITrackingCodeGenerator? _trackingCodeGenerator;
    private readonly bool _demoLocationRefresh;

    public FieldOpsApiFactory(
        string connectionString,
        ITrackingCodeGenerator? trackingCodeGenerator = null,
        bool demoLocationRefresh = false)
    {
        _connectionString = connectionString;
        _trackingCodeGenerator = trackingCodeGenerator;
        _demoLocationRefresh = demoLocationRefresh;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Seed:AdminUsername", "");
        builder.UseSetting("Seed:AdminPassword", "");
        builder.UseSetting("Seed:DemoTeams", "false");
        builder.UseSetting("Jwt:Key", TestJwtKey);

        if (_trackingCodeGenerator is null && !_demoLocationRefresh)
        {
            return;
        }

        builder.ConfigureTestServices(services =>
        {
            if (_trackingCodeGenerator is not null)
            {
                services.RemoveAll<ITrackingCodeGenerator>();
                services.AddSingleton(_trackingCodeGenerator);
            }

            if (_demoLocationRefresh)
            {
                var demoConfiguration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?> { ["Seed:DemoTeams"] = "true" })
                    .Build();

                services.RemoveAll<DemoLocationRefresher>();
                services.AddScoped(sp => new DemoLocationRefresher(
                    sp.GetRequiredService<AppDbContext>(),
                    sp.GetRequiredService<IClock>(),
                    demoConfiguration));
            }
        });
    }
}