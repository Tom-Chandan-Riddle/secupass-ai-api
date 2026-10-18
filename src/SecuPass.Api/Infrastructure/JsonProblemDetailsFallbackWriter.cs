using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace SecuPass.Api.Infrastructure;

// A14: registered after the built-in writer, which declines when Accept doesn't allow JSON.
// This one always writes, so every error response has the same ProblemDetails shape.
public sealed class JsonProblemDetailsFallbackWriter(IOptions<ProblemDetailsOptions> options) : IProblemDetailsWriter
{
    private const string ProblemJsonContentType = "application/problem+json";

    public bool CanWrite(ProblemDetailsContext context) => true;

    public async ValueTask WriteAsync(ProblemDetailsContext context)
    {
        var problemDetails = context.ProblemDetails;
        problemDetails.Status ??= context.HttpContext.Response.StatusCode;
        problemDetails.Title ??= ReasonPhrases.GetReasonPhrase(problemDetails.Status.Value);
        options.Value.CustomizeProblemDetails?.Invoke(context);

        await context.HttpContext.Response.WriteAsJsonAsync(
            problemDetails,
            problemDetails.GetType(),
            (JsonSerializerOptions?)null,
            ProblemJsonContentType,
            context.HttpContext.RequestAborted);
    }
}