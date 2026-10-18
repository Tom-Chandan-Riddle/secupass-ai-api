using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace SecuPass.IntegrationTests.Support;

// Appended after the app's own pipeline, so the throw happens inside the app's exception handler.
public sealed class ThrowingEndpointStartupFilter : IStartupFilter
{
    public const string Path = "/test/throw";
    public const string InternalDetail = "connection string Host=secret-db;Password=hunter2";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path == Path)
            {
                throw new InvalidOperationException(InternalDetail);
            }

            await nextMiddleware(context);
        });
    };
}