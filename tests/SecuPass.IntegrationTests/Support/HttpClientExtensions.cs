namespace SecuPass.IntegrationTests.Support;

public static class HttpClientExtensions
{
    public const string CorrelationHeader = "X-Correlation-ID";

    public static async Task<HttpResponseMessage> GetWithCorrelationIdAsync(
        this HttpClient client, string path, params string[] correlationIds)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation(CorrelationHeader, correlationIds);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static string? CorrelationId(this HttpResponseMessage response) =>
        response.Headers.TryGetValues(CorrelationHeader, out var values) ? values.Single() : null;
}