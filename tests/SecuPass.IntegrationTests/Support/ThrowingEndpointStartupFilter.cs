using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace SecuPass.IntegrationTests.Support;

// Appended after the app's own pipeline, so the throw happens inside the app's exception handler.
public sealed class ThrowingEndpointStartupFilter : IStartupFilter
{
    public const string Path = "/test/throw";
    public const string BadRequestPath = "/test/bad-request";
    public const string AfterResponseStartedPath = "/test/throw-after-response-started";
    public const string InternalDetail = "connection string Host=secret-db;Password=hunter2";

    private const string StatusQueryKey = "status";

    public static string BadRequestPathWithStatus(int statusCode) => $"{BadRequestPath}?{StatusQueryKey}={statusCode}";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path == Path)
            {
                throw new InvalidOperationException(InternalDetail);
            }

            if (context.Request.Path == BadRequestPath)
            {
                var statusCode = int.Parse(context.Request.Query[StatusQueryKey]!, System.Globalization.CultureInfo.InvariantCulture);
                throw new BadHttpRequestException(InternalDetail, statusCode);
            }

            if (context.Request.Path == AfterResponseStartedPath)
            {
                await context.Response.WriteAsync("partial body", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
                throw new InvalidOperationException(InternalDetail);
            }

            await nextMiddleware(context);
        });
    };
}