using FieldOps.Api.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FieldOps.Api.Tests.Database;

public sealed class PostgisFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder("postgis/postgis:17-3.5").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
    public string ConnectionString => _container.GetConnectionString();
    public AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_container.GetConnectionString(), o => o.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }

    public async Task ResetAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
           "TRUNCATE TABLE jobs, job_status_histories, users, teams, team_skills, shifts RESTART IDENTITY CASCADE;");
    }
}