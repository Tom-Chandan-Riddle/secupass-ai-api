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

    // A15: the exception's own status and standard title, no detail, logged at Warning only.
    [Theory]
    [InlineData(400, "Bad Request")]
    [InlineData(413, "Payload Too Large")]
    public async Task BadRequestException_ReturnsItsStatusAsProblemDetailsAndLogsWarning(int statusCode, string expectedTitle)
    {
        using var client = factory.CreateClient();
        var correlationId = NewCorrelationId();

        using var response = await client.GetWithCorrelationIdAsync(
            ThrowingEndpointStartupFilter.BadRequestPathWithStatus(statusCode), correlationId);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(statusCode, (int)response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedTitle, problem.RootElement.GetProperty("title").GetString());
        Assert.Equal(correlationId, problem.RootElement.GetProperty("correlationId").GetString());
        Assert.False(problem.RootElement.TryGetProperty("detail", out _));
        Assert.DoesNotContain(ThrowingEndpointStartupFilter.InternalDetail, body);
        var requestEvents = factory.LogSink.EventsWithCorrelationId(correlationId);
        Assert.Contains(requestEvents, logEvent => logEvent.Level == LogEventLevel.Warning);
        Assert.DoesNotContain(requestEvents, logEvent => logEvent.Level >= LogEventLevel.Error);
    }

    // Pins ASP.NET Core's own behaviour: the exception handler can't run, but it still logs once at Error (review S2).
    [Fact]
    public async Task ExceptionAfterResponseStarted_IsLoggedAtErrorWithCorrelationId()
    {
        using var client = factory.CreateClient();
        var correlationId = NewCorrelationId();

        // The client sees a broken response here; only the server-side log matters.
        await Record.ExceptionAsync(async () =>
        {
            using var response = await client.GetWithCorrelationIdAsync(
                ThrowingEndpointStartupFilter.AfterResponseStartedPath, correlationId);
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        });

        var errorEvent = Assert.Single(
            factory.LogSink.EventsWithCorrelationId(correlationId),
            logEvent => logEvent.Level >= LogEventLevel.Error);
        Assert.IsType<InvalidOperationException>(errorEvent.Exception);
    }

    // A14: ProblemDetails whatever the Accept header says.
    [Theory]
    [InlineData("/no-such-route", 404)]
    [InlineData(ThrowingEndpointStartupFilter.Path, 500)]
    public async Task ErrorResponse_WithNonJsonAccept_IsStillProblemDetails(string path, int expectedStatus)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/plain");
        var correlationId = NewCorrelationId();

        using var response = await client.GetWithCorrelationIdAsync(path, correlationId);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedStatus, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.RootElement.GetProperty("title").GetString()));
        Assert.Equal(correlationId, problem.RootElement.GetProperty("correlationId").GetString());
    }

    private static string NewCorrelationId() => Guid.NewGuid().ToString();
}