using System.Net;

using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests;

public sealed class HealthEndpointTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    [Fact]
    public async Task GetHealth_WhenAppIsRunning_Returns200WithHealthyBody()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHealth_WithoutAuthorizationHeader_Returns200()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}