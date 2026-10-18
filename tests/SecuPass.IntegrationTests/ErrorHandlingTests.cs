using System.Net;
using System.Text.Json;

using SecuPass.IntegrationTests.Support;

using Serilog.Events;

namespace SecuPass.IntegrationTests;

public sealed class ErrorHandlingTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task UnhandledException_Returns500ProblemDetailsWithCorrelationIdAndNoInternals()
    {
        using var client = factory.CreateClient();
        var correlationId = NewCorrelationId();

        using var response = await client.GetWithCorrelationIdAsync(ThrowingEndpointStartupFilter.Path, correlationId);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(correlationId, response.CorrelationId());
        Assert.Equal(500, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(correlationId, problem.RootElement.GetProperty("correlationId").GetString());
        Assert.DoesNotContain(ThrowingEndpointStartupFilter.InternalDetail, body);
        Assert.DoesNotContain(nameof(InvalidOperationException), body);
        Assert.DoesNotContain(nameof(ThrowingEndpointStartupFilter), body);
    }

    [Fact]
    public async Task UnhandledException_IsLoggedOnceAtErrorWithTheException()
    {
        using var client = factory.CreateClient();
        var correlationId = NewCorrelationId();

        using var response = await client.GetWithCorrelationIdAsync(ThrowingEndpointStartupFilter.Path, correlationId);

        var errorEvent = Assert.Single(
            factory.LogSink.EventsWithCorrelationId(correlationId),
            logEvent => logEvent.Level >= LogEventLevel.Error);
        Assert.IsType<InvalidOperationException>(errorEvent.Exception);
    }

    // A13: bodyless errors are returned as ProblemDetails too.
    [Fact]
    public async Task UnknownRoute_Returns404ProblemDetailsWithCorrelationId()
    {
        using var client = factory.CreateClient();
        var correlationId = NewCorrelationId();

        using var response = await client.GetWithCorrelationIdAsync("/no-such-route", correlationId);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(correlationId, problem.RootElement.GetProperty("correlationId").GetString());
    }

    private static string NewCorrelationId() => Guid.NewGuid().ToString();
}