using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SecuPass.Api.Infrastructure;

public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const string UnexpectedErrorTitle = "An unexpected error occurred.";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = UnexpectedErrorTitle,
            },
        });

        // Handled even if no ProblemDetails body could be written, so the exception is logged only once.
        return true;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {RequestMethod} {RequestPath}")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception, string requestMethod, string requestPath);
}