using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using Npgsql;

using SecuPass.IntegrationTests.Support;

using Serilog.Events;

namespace SecuPass.IntegrationTests;

public sealed class DatabaseMigrationTests(PostgresContainerFixture postgres)
{
    private const string MigrationsAppliedEventName = "LogMigrationsApplied";
    private const string EfCoreLogPrefix = "Microsoft.EntityFrameworkCore";

    [Fact]
    public void MigrateDatabase_OnEmptyDatabase_LogsAppliedInitialCreate()
    {
        using var factory = new SecuPassApiFactory(postgres);
        using var freshDatabaseFactory = WithDatabase(factory, NewDatabaseName());

        using var client = freshDatabaseFactory.CreateClient();

        var migrationEvent = Assert.Single(MigrationEvents(factory.LogSink));
        Assert.Contains("_InitialCreate", migrationEvent.Properties["Migrations"].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MigrateDatabase_WhenUpToDate_LogsNothing()
    {
        var databaseName = NewDatabaseName();
        using (var firstFactory = new SecuPassApiFactory(postgres))
        using (var firstHost = WithDatabase(firstFactory, databaseName))
        {
            using var firstClient = firstHost.CreateClient();
        }

        using var secondFactory = new SecuPassApiFactory(postgres);
        using var secondHost = WithDatabase(secondFactory, databaseName);

        using var secondClient = secondHost.CreateClient();

        Assert.Empty(MigrationEvents(secondFactory.LogSink));
    }

    // D12: migrating an empty database runs SQL commands, and none of them is logged below Warning.
    [Fact]
    public void MigrateDatabase_OnEmptyDatabase_LogsNoEfCoreInformation()
    {
        using var factory = new SecuPassApiFactory(postgres);
        using var freshDatabaseFactory = WithDatabase(factory, NewDatabaseName());

        using var client = freshDatabaseFactory.CreateClient();

        Assert.DoesNotContain(
            factory.LogSink.AllEvents,
            logEvent => logEvent.Level < LogEventLevel.Warning
                && (CapturingLogSink.PropertyText(logEvent, "SourceContext") ?? string.Empty)
                    .StartsWith(EfCoreLogPrefix, StringComparison.Ordinal));
    }

    private static string NewDatabaseName() => $"secupass_{Guid.NewGuid():N}";

    private WebApplicationFactory<Program> WithDatabase(SecuPassApiFactory factory, string databaseName)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = databaseName }.ConnectionString;
        return factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection([new(SecuPassApiFactory.ConnectionStringKey, connectionString)])));
    }

    private static IEnumerable<LogEvent> MigrationEvents(CapturingLogSink logSink) =>
        logSink.AllEvents.Where(logEvent => logEvent.Properties.TryGetValue("EventId", out var eventId)
            && eventId.ToString().Contains(MigrationsAppliedEventName, StringComparison.Ordinal));
}