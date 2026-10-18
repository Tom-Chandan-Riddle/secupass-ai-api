using SecuPass.IntegrationTests.Support;

using Serilog.Events;

namespace SecuPass.IntegrationTests;

public sealed class RequestLoggingTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    [Fact]
    public async Task Request_IsLoggedWithMethodPathStatusElapsedAndCorrelationId()
    {
        using var client = factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();

        using var response = await client.GetWithCorrelationIdAsync("/health", correlationId);

        var requestEvent = Assert.Single(
            factory.LogSink.EventsWithCorrelationId(correlationId),
            logEvent => logEvent.Properties.ContainsKey("StatusCode"));
        Assert.Equal(LogEventLevel.Information, requestEvent.Level);
        Assert.Equal("GET", CapturingLogSink.PropertyText(requestEvent, "RequestMethod"));
        Assert.Equal("/health", CapturingLogSink.PropertyText(requestEvent, "RequestPath"));
        Assert.Equal("200", CapturingLogSink.PropertyText(requestEvent, "StatusCode"));
        Assert.NotNull(CapturingLogSink.PropertyText(requestEvent, "Elapsed"));
    }
}