using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace SecuPass.Api.Infrastructure;

public static class LoggingExtensions
{
    // The static Log.Logger is left alone, so each host (including each test host) logs through its own logger.
    public static IServiceCollection AddStructuredLogging(this IServiceCollection services, IConfiguration configuration) =>
        services.AddSerilog(
            (serviceProvider, loggerConfiguration) => loggerConfiguration
                .ReadFrom.Configuration(configuration)
                .ReadFrom.Services(serviceProvider)
                .Enrich.FromLogContext()
                .WriteTo.Console(new RenderedCompactJsonFormatter()),
            preserveStaticLogger: true);

    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options =>
        {
            options.Logger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();
            // Failures are logged where they happen, so a 500 shows up once at Error, not twice.
            options.GetLevel = (_, _, _) => LogEventLevel.Information;
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                diagnosticContext.Set(CorrelationIdMiddleware.LogPropertyName, httpContext.GetCorrelationId());
        });
}