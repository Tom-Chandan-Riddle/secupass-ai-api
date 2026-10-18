namespace SecuPass.IntegrationTests.Support;

public static class HttpClientExtensions
{
    public const string CorrelationHeader = "X-Correlation-ID";

    public static async Task<HttpResponseMessage> GetWithCorrelationIdAsync(this HttpClient client, string path, string correlationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation(CorrelationHeader, correlationId);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static string? CorrelationId(this HttpResponseMessage response) =>
        response.Headers.TryGetValues(CorrelationHeader, out var values) ? values.Single() : null;
}