using System.Net;
using System.Net.Http.Json;

using SecuPass.Domain.Identity;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

public sealed class CurrentUserTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private sealed record CurrentUserResponse(Guid UserId, string Email, string Role);

    // The fallback policy closes every endpoint not marked anonymous; /auth/me is the first such endpoint.
    [Fact]
    public async Task GetCurrentUser_WithoutToken_Returns401ProblemDetailsWithCorrelationId()
    {
        using var client = factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();

        using var response = await client.GetWithCorrelationIdAsync(IdentityTestExtensions.CurrentUserPath, correlationId);

        var problem = await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(correlationId, problem.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task GetCurrentUser_WithMalformedToken_Returns401ProblemDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, "not-a-token");

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WithRefreshTokenAsBearer_Returns401ProblemDetails()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        var tokens = await client.LoginAsync(user);

        using var response = await client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, tokens.RefreshToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    // The role comes from the token's claims, so this also proves login puts the role into the token.
    [Theory]
    [InlineData(StaffRoles.Admin)]
    [InlineData(StaffRoles.Staff)]
    [InlineData(StaffRoles.ClientViewer)]
    public async Task GetCurrentUser_WithValidToken_ReturnsUserIdEmailAndRole(string role)
    {
        var user = await factory.CreateStaffUserAsync(role);
        using var client = factory.CreateClient();
        var tokens = await client.LoginAsync(user);

        using var response = await client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var currentUser = await response.Content.ReadFromJsonAsync<CurrentUserResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(new CurrentUserResponse(user.Id, user.Email, role), currentUser);
    }
}