using System.Net;

using Microsoft.AspNetCore.Hosting;

using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests;

public sealed class OpenApiTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private const string OpenApiDocumentPath = "/openapi/v1.json";
    private const string ScalarPath = "/scalar/v1";

    [Theory]
    [InlineData(OpenApiDocumentPath)]
    [InlineData(ScalarPath)]
    public async Task Get_InDevelopment_Returns200(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(OpenApiDocumentPath)]
    [InlineData(ScalarPath)]
    public async Task Get_InProduction_Returns404(string path)
    {
        using var productionFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = productionFactory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}