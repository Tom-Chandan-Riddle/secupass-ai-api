using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Serilog.Core;

namespace SecuPass.IntegrationTests.Support;

public sealed class SecuPassApiFactory : WebApplicationFactory<Program>
{
    public CapturingLogSink LogSink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILogEventSink>(LogSink);
            services.AddSingleton<IStartupFilter, ThrowingEndpointStartupFilter>();
        });
}