using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace SecuPass.Api.Infrastructure;

// Malformed requests are the client's fault, so they get their own 4xx and a Warning, not a 500 and an Error.
public sealed partial class BadHttpRequestExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<BadHttpRequestExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
        {
            return false;
        }

        LogBadRequest(logger, httpContext.Request.Method, httpContext.Request.Path, badRequest.StatusCode, badRequest.Message);

        httpContext.Response.StatusCode = badRequest.StatusCode;
        // A15: the standard title for the status, and no detail, so framework internals don't reach the client.
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = badRequest.StatusCode,
                Title = ReasonPhrases.GetReasonPhrase(badRequest.StatusCode),
            },
        });

        return true;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Rejected malformed request {RequestMethod} {RequestPath} with {StatusCode}: {Reason}")]
    private static partial void LogBadRequest(
        ILogger logger, string requestMethod, string requestPath, int statusCode, string reason);
}