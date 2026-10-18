namespace SecuPass.Api.Infrastructure;

public static class CorrelationIdHttpContextExtensions
{
    private const string ItemKey = "SecuPass.CorrelationId";

    public static void SetCorrelationId(this HttpContext context, string correlationId) =>
        context.Items[ItemKey] = correlationId;

    // Falls back to the trace identifier so error responses still carry an ID if the middleware never ran.
    public static string GetCorrelationId(this HttpContext context) =>
        context.Items[ItemKey] as string ?? context.TraceIdentifier;
}