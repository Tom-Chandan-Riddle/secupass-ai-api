using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using SecuPass.Domain.Identity;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

public sealed class RefreshTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    [Fact]
    public async Task Refresh_WithGarbageToken_Returns401ProblemDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostJsonAsync(IdentityTestExtensions.RefreshPath, new { refreshToken = "not-a-token" });

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    // Access and refresh tokens are protected with different purposes, so one can't stand in for the other.
    [Fact]
    public async Task Refresh_WithAccessTokenInsteadOfRefreshToken_Returns401()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        var tokens = await client.LoginAsync(user);

        using var response = await client.PostJsonAsync(IdentityTestExtensions.RefreshPath, new { refreshToken = tokens.AccessToken });

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithMissingToken_Returns400WithFieldError()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostJsonAsync(IdentityTestExtensions.RefreshPath, new { });

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        var fieldNames = problem.GetProperty("errors").EnumerateObject().Select(field => field.Name);
        Assert.Contains("refreshToken", fieldNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_ReturnsAccessTokenThatAuthenticates()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        var tokens = await client.LoginAsync(user);

        using var response = await client.PostJsonAsync(IdentityTestExtensions.RefreshPath, new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await response.Content.ReadFromJsonAsync<TokenResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(refreshed);
        using var currentUserResponse = await client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, refreshed.AccessToken);
        Assert.Equal(HttpStatusCode.OK, currentUserResponse.StatusCode);
    }

    // Identity changes the security stamp when credentials change, so older refresh tokens stop working.
    [Fact]
    public async Task Refresh_AfterSecurityStampChanged_Returns401()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        var tokens = await client.LoginAsync(user);
        await factory.UpdateSecurityStampAsync(user);

        using var response = await client.PostJsonAsync(IdentityTestExtensions.RefreshPath, new { refreshToken = tokens.RefreshToken });

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    // A16: refresh tokens last 14 days.
    [Fact]
    public async Task Refresh_AfterRefreshTokenExpired_Returns401()
    {
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        using var clockFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
        var user = await clockFactory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = clockFactory.CreateClient();
        var tokens = await client.LoginAsync(user);
        clock.Advance(TimeSpan.FromDays(14) + TimeSpan.FromSeconds(1));

        using var response = await client.PostJsonAsync(IdentityTestExtensions.RefreshPath, new { refreshToken = tokens.RefreshToken });

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }
}