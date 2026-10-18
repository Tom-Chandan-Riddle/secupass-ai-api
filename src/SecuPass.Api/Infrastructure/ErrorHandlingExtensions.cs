namespace SecuPass.Api.Infrastructure;

public static class ErrorHandlingExtensions
{
    private const string CorrelationIdExtensionName = "correlationId";

    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions[CorrelationIdExtensionName] = context.HttpContext.GetCorrelationId());
        services.AddSingleton<IProblemDetailsWriter, JsonProblemDetailsFallbackWriter>();

        // Handlers run in registration order. The global handler takes whatever the others decline.
        services.AddExceptionHandler<BadHttpRequestExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        // A13: bodyless error responses such as unmatched-route 404s become ProblemDetails.
        app.UseStatusCodePages();
        return app;
    }
}