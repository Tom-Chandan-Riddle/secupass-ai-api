using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using SecuPass.Domain.Identity;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

// A16: access tokens last 1 hour and refresh tokens 14 days. Each boundary is checked from both sides.
public sealed class TokenLifetimeTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(14);

    [Fact]
    public async Task GetCurrentUser_JustBeforeAccessTokenExpires_Returns200()
    {
        var (clock, host) = HostWithFakeClock();
        using var clockHost = host;
        var tokens = await LoginAsync(clockHost);
        clock.Advance(AccessTokenLifetime - TimeSpan.FromMinutes(1));

        using var client = clockHost.CreateClient();
        using var response = await client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentUser_AfterAccessTokenExpired_Returns401()
    {
        var (clock, host) = HostWithFakeClock();
        using var clockHost = host;
        var tokens = await LoginAsync(clockHost);
        clock.Advance(AccessTokenLifetime + TimeSpan.FromSeconds(1));

        using var client = clockHost.CreateClient();
        using var response = await client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, tokens.AccessToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_JustBeforeRefreshTokenExpires_Returns200()
    {
        var (clock, host) = HostWithFakeClock();
        using var clockHost = host;
        var tokens = await LoginAsync(clockHost);
        clock.Advance(RefreshTokenLifetime - TimeSpan.FromMinutes(1));

        using var client = clockHost.CreateClient();
        using var response = await client.PostJsonAsync(IdentityTestExtensions.RefreshPath, new { refreshToken = tokens.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private (FakeTimeProvider Clock, WebApplicationFactory<Program> Host) HostWithFakeClock()
    {
        var clock = new FakeTimeProvider(TimeProvider.System.GetUtcNow());
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(clock)));
        return (clock, host);
    }

    private static async Task<TokenResponse> LoginAsync(WebApplicationFactory<Program> host)
    {
        var user = await host.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = host.CreateClient();
        return await client.LoginAsync(user);
    }
}