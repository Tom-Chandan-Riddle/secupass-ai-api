using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Serilog.Core;

namespace SecuPass.IntegrationTests.Support;

public sealed class SecuPassApiFactory(PostgresContainerFixture postgres) : WebApplicationFactory<Program>
{
    public const string ConnectionStringKey = "Database:ConnectionString";

    public CapturingLogSink LogSink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection([new(ConnectionStringKey, postgres.ConnectionString)]));

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILogEventSink>(LogSink);
            services.AddSingleton<IStartupFilter, ThrowingEndpointStartupFilter>();
        });
    }
}