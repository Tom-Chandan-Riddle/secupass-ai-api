using System.Text.RegularExpressions;

namespace SecuPass.Api.Infrastructure;

public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string LogPropertyName = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers);
        context.SetCorrelationId(correlationId);

        // Set when the response starts, because the exception handler clears headers set earlier.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { [LogPropertyName] = correlationId }))
        {
            await next(context);
        }
    }

    private string ResolveCorrelationId(IHeaderDictionary requestHeaders)
    {
        if (!requestHeaders.TryGetValue(HeaderName, out var incomingValues))
        {
            return NewCorrelationId();
        }

        var incomingId = incomingValues.ToString();
        if (IsValidCorrelationId(incomingId))
        {
            return incomingId;
        }

        // A12: replace rather than reject. Only the length is logged, because the raw value is untrusted.
        var replacementId = NewCorrelationId();
        LogInvalidCorrelationId(logger, incomingId.Length, replacementId);
        return replacementId;
    }

    private static string NewCorrelationId() => Guid.NewGuid().ToString();

    // A12: 1 to 64 characters of letters, digits, '-', '_', '.' and ':'.
    private static bool IsValidCorrelationId(string candidate) => ValidCorrelationIdPattern().IsMatch(candidate);

    // \z rather than $, because $ also matches before a trailing newline.
    [GeneratedRegex(@"^[A-Za-z0-9._:-]{1,64}\z")]
    private static partial Regex ValidCorrelationIdPattern();

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Replaced an invalid X-Correlation-ID header of {InvalidLength} characters with {CorrelationId}")]
    private static partial void LogInvalidCorrelationId(ILogger logger, int invalidLength, string correlationId);
}