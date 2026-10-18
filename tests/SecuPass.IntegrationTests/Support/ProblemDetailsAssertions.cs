using System.Net;
using System.Text.Json;

namespace SecuPass.IntegrationTests.Support;

public static class ProblemDetailsAssertions
{
    private const string ProblemJson = "application/problem+json";

    // Asserts the response is ProblemDetails with the expected status, and returns its body.
    public static async Task<JsonElement> ReadProblemAsync(this HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal((int)expectedStatus, problem.RootElement.GetProperty("status").GetInt32());
        return problem.RootElement.Clone();
    }
}