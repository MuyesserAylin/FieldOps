using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FieldOps.Api.Tests.Reports;

public class FieldOpsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public FieldOpsApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
    }
}
