using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using SecuPass.Api.Features.Identity.DevelopmentAdmin;

using Serilog.Core;

namespace SecuPass.IntegrationTests.Support;

public sealed class SecuPassApiFactory(PostgresContainerFixture postgres) : WebApplicationFactory<Program>
{
    public const string ConnectionStringKey = "Database:ConnectionString";
    public const string DevelopmentAdminEnabledKey = $"{DevelopmentAdminOptions.SectionName}:{nameof(DevelopmentAdminOptions.Enabled)}";
    public const string DevelopmentAdminEmailKey = $"{DevelopmentAdminOptions.SectionName}:{nameof(DevelopmentAdminOptions.Email)}";
    public const string DevelopmentAdminPasswordKey = $"{DevelopmentAdminOptions.SectionName}:{nameof(DevelopmentAdminOptions.Password)}";

    public CapturingLogSink LogSink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Tests arrange their own users, so the Development Admin seed is off unless a test turns it on.
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection([
            new(ConnectionStringKey, postgres.ConnectionString),
            new(DevelopmentAdminEnabledKey, "false"),
        ]));

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILogEventSink>(LogSink);
            services.AddSingleton<IStartupFilter, ThrowingEndpointStartupFilter>();
        });
    }

    // A database of its own, migrated at startup, for tests that change shared data such as the seeded roles.
    public WebApplicationFactory<Program> WithFreshDatabase()
    {
        var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Database = $"secupass_{Guid.NewGuid():N}",
        }.ConnectionString;
        return WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection([new(ConnectionStringKey, connectionString)])));
    }
}