using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SecuPass.Infrastructure.Persistence;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests;

public sealed class DatabaseHealthTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private const string UnreachableConnectionString =
        "Host=127.0.0.1;Port=1;Database=secupass;Username=secupass;Password=unused;Timeout=2";

    [Fact]
    public async Task GetHealthReady_WhenDatabaseReachable_Returns200()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealthReady_WhenDatabaseUnreachable_Returns503()
    {
        using var unreachableFactory = WithConnectionString(UnreachableConnectionString);
        using var client = unreachableFactory.CreateClient();

        using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_WhenDatabaseUnreachable_Returns200()
    {
        using var unreachableFactory = WithConnectionString(UnreachableConnectionString);
        using var client = unreachableFactory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Startup_InDevelopment_AppliesAllMigrations()
    {
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecuPassDbContext>();

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(pendingMigrations);
        Assert.Contains(appliedMigrations, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    // Production, so startup doesn't try to migrate a database that isn't there.
    private WebApplicationFactory<Program> WithConnectionString(string connectionString) =>
        factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection([new(SecuPassApiFactory.ConnectionStringKey, connectionString)])));
}