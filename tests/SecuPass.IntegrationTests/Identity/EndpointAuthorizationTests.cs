using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

// D14: there is no fallback policy, so this test is what keeps a new endpoint from being public by accident.
public sealed class EndpointAuthorizationTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    [Fact]
    public void EveryEndpoint_DeclaresAuthorizeOrAllowAnonymous()
    {
        using var client = factory.CreateClient();

        var endpoints = MappedEndpoints();

        Assert.NotEmpty(endpoints);
        var undeclared = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAuthorizeData>() is null
                && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(endpoint => endpoint.RoutePattern.RawText);
        Assert.Empty(undeclared);
    }

    [Fact]
    public void CurrentUserEndpoint_RequiresAuthorization()
    {
        using var client = factory.CreateClient();

        var currentUser = Assert.Single(MappedEndpoints(), endpoint => endpoint.RoutePattern.RawText == IdentityTestExtensions.CurrentUserPath);

        Assert.NotNull(currentUser.Metadata.GetMetadata<IAuthorizeData>());
        Assert.Null(currentUser.Metadata.GetMetadata<IAllowAnonymous>());
    }

    private List<RouteEndpoint> MappedEndpoints() =>
        [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];
}