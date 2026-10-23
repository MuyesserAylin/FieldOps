using System;
using FieldOps.Api.Tracking;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FieldOps.Api.Tests.Reports;

public class FieldOpsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly ITrackingCodeGenerator? _trackingCodeGenerator;


    public FieldOpsApiFactory(string connectionString,
    ITrackingCodeGenerator? trackingCodeGenerator = null)
    {
        _connectionString = connectionString;
        _trackingCodeGenerator = trackingCodeGenerator;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _connectionString);

        if (_trackingCodeGenerator is not null)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITrackingCodeGenerator>();
                services.AddSingleton(_trackingCodeGenerator);
            });
        }
    }
}
