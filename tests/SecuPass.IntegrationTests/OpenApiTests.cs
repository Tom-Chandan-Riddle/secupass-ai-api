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
    [InlineData("Production", OpenApiDocumentPath)]
    [InlineData("Production", ScalarPath)]
    [InlineData("Staging", OpenApiDocumentPath)]
    [InlineData("Staging", ScalarPath)]
    public async Task Get_OutsideDevelopment_Returns404(string environment, string path)
    {
        using var environmentFactory = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        using var client = environmentFactory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}