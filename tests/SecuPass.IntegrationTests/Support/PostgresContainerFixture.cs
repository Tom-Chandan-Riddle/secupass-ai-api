using Microsoft.EntityFrameworkCore;

using SecuPass.Infrastructure.Persistence;

using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(SecuPass.IntegrationTests.Support.PostgresContainerFixture))]

namespace SecuPass.IntegrationTests.Support;

// One container for the whole test run. D11: same image as compose.yaml.
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string PostgresImage = "postgres:17-alpine";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(PostgresImage).Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await MigrateSharedDatabaseAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    // Test hosts start in parallel and each migrates at startup. Two hosts migrating one database at once
    // fail with 42P07, so the shared database is migrated here first and the hosts find nothing to apply.
    private async Task MigrateSharedDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<SecuPassDbContext>().UseNpgsql(ConnectionString).Options;
        await using var dbContext = new SecuPassDbContext(options);
        await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }
}