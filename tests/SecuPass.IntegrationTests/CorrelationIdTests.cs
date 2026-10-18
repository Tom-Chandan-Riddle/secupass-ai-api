using SecuPass.IntegrationTests.Support;

using Serilog.Events;

namespace SecuPass.IntegrationTests;

public sealed class CorrelationIdTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private const string HealthPath = "/health";

    [Fact]
    public async Task Request_WithoutCorrelationHeader_ReturnsGeneratedId()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(HealthPath, TestContext.Current.CancellationToken);

        Assert.True(Guid.TryParse(response.CorrelationId(), out _));
    }

    [Theory]
    [InlineData("order-123")]
    [InlineData("a1:b2.c3_d4-e5")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Request_WithValidCorrelationHeader_EchoesSameId(string incomingId)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetWithCorrelationIdAsync(HealthPath, incomingId);

        Assert.Equal(incomingId, response.CorrelationId());
    }

    // A12: invalid IDs are replaced, logged at Warning, and the request still succeeds.
    [Theory]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Request_WithInvalidCorrelationHeader_ReturnsGeneratedIdAndLogsWarning(string incomingId)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetWithCorrelationIdAsync(HealthPath, incomingId);

        var returnedId = response.CorrelationId();
        Assert.True(response.IsSuccessStatusCode);
        Assert.True(Guid.TryParse(returnedId, out _));
        Assert.Contains(
            factory.LogSink.EventsWithCorrelationId(returnedId!),
            logEvent => logEvent.Level == LogEventLevel.Warning);
    }

    // A12: repeated headers arrive joined with a comma, which isn't an allowed character.
    [Fact]
    public async Task Request_WithRepeatedCorrelationHeader_ReturnsGeneratedIdAndLogsWarning()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetWithCorrelationIdAsync(HealthPath, "first-id", "second-id");

        var returnedId = response.CorrelationId();
        Assert.True(response.IsSuccessStatusCode);
        Assert.True(Guid.TryParse(returnedId, out _));
        Assert.Contains(
            factory.LogSink.EventsWithCorrelationId(returnedId!),
            logEvent => logEvent.Level == LogEventLevel.Warning);
    }
}