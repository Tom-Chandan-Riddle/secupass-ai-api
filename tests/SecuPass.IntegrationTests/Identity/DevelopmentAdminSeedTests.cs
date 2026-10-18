using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SecuPass.Api.Features.Identity.DevelopmentAdmin;
using SecuPass.Domain.Identity;
using SecuPass.Infrastructure.Identity;
using SecuPass.Infrastructure.Persistence;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

public sealed class DevelopmentAdminSeedTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private const string SeedPassword = "Seed-Admin-Passw0rd!";

    private sealed record CurrentUserResponse(Guid UserId, string Email, string Role);

    [Fact]
    public async Task DefaultTestHost_DoesNotSeedTheConfiguredAdmin()
    {
        using var client = factory.CreateClient();
        var configuredEmail = factory.Services.GetRequiredService<IConfiguration>()[SecuPassApiFactory.DevelopmentAdminEmailKey];

        Assert.False(string.IsNullOrWhiteSpace(configuredEmail));
        Assert.Null(await FindUserAsync(factory, configuredEmail));
    }

    [Fact]
    public async Task Seed_OutsideDevelopment_CreatesNoUser()
    {
        var email = NewEmail();
        using var productionHost = WithSeed(email, SeedPassword, environment: "Production");

        using var client = productionHost.CreateClient();

        Assert.Null(await FindUserAsync(productionHost, email));
    }

    [Fact]
    public async Task Seed_WhenEnabled_CreatesAdminWhoCanLogIn()
    {
        var email = NewEmail();
        using var seededHost = WithSeed(email, SeedPassword);
        using var client = seededHost.CreateClient();

        using var loginResponse = await client.PostLoginAsync(email, SeedPassword);

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(tokens);
        using var currentUserResponse = await client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, tokens.AccessToken);
        var currentUser = await currentUserResponse.Content.ReadFromJsonAsync<CurrentUserResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(StaffRoles.Admin, currentUser?.Role);
    }

    // A18: a second start with a different configured password leaves the existing Admin alone.
    [Fact]
    public async Task Seed_WhenAdminAlreadyExists_LeavesItUnchanged()
    {
        var email = NewEmail();
        using (var firstHost = WithSeed(email, SeedPassword))
        {
            using var firstClient = firstHost.CreateClient();
        }

        using var secondHost = WithSeed(email, "Other-Admin-Passw0rd!");
        using var client = secondHost.CreateClient();

        using var originalPasswordResponse = await client.PostLoginAsync(email, SeedPassword);
        using var newPasswordResponse = await client.PostLoginAsync(email, "Other-Admin-Passw0rd!");
        Assert.Equal(HttpStatusCode.OK, originalPasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, newPasswordResponse.StatusCode);
    }

    // A19: called directly, because a test that waits for host startup to fail was flaky in F00.
    [Fact]
    public async Task Seed_WithPasswordBreakingIdentityRules_ThrowsWithReasonsAndCreatesNoUser()
    {
        var email = NewEmail();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewSeeder(scope.ServiceProvider, email, "weak").SeedAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Passwords must", exception.Message, StringComparison.Ordinal);
        Assert.Null(await FindUserAsync(factory, email));
    }

    // The role assignment fails here because a fresh database has its Admin role removed first.
    [Fact]
    public async Task Seed_WhenRoleAssignmentFails_CreatesNoUser()
    {
        var email = NewEmail();
        using var isolatedHost = factory.WithFreshDatabase();
        using var client = isolatedHost.CreateClient();
        await using var scope = isolatedHost.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var adminRole = await roleManager.FindByNameAsync(StaffRoles.Admin);
        Assert.NotNull(adminRole);
        await roleManager.DeleteAsync(adminRole);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(
            () => NewSeeder(scope.ServiceProvider, email, SeedPassword).SeedAsync(TestContext.Current.CancellationToken));

        Assert.Null(await FindUserAsync(isolatedHost, email));
    }

    [Fact]
    public async Task Seed_WhenEnabled_LogsUserIdButNotTheEmail()
    {
        var email = NewEmail();
        using var seededHost = WithSeed(email, SeedPassword);

        using var client = seededHost.CreateClient();

        var admin = await FindUserAsync(seededHost, email);
        Assert.NotNull(admin);
        Assert.Contains(
            factory.LogSink.AllEvents,
            logEvent => CapturingLogSink.PropertyText(logEvent, "UserId") == admin.Id.ToString());
        Assert.DoesNotContain(
            factory.LogSink.AllEvents,
            logEvent => logEvent.RenderMessage().Contains(email, StringComparison.OrdinalIgnoreCase)
                || logEvent.Properties.Values.Any(value => value.ToString().Contains(email, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NewEmail() => $"admin-{Guid.NewGuid():N}@test.secupass.local";

    private WebApplicationFactory<Program> WithSeed(string email, string password, string environment = "Development") =>
        factory.WithWebHostBuilder(builder => builder
            .UseEnvironment(environment)
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection([
                new(SecuPassApiFactory.DevelopmentAdminEnabledKey, "true"),
                new(SecuPassApiFactory.DevelopmentAdminEmailKey, email),
                new(SecuPassApiFactory.DevelopmentAdminPasswordKey, password),
            ])));

    private static DevelopmentAdminSeeder NewSeeder(IServiceProvider services, string email, string password) => new(
        services.GetRequiredService<UserManager<StaffUser>>(),
        services.GetRequiredService<SecuPassDbContext>(),
        Options.Create(new DevelopmentAdminOptions { Enabled = true, Email = email, Password = password }),
        NullLogger<DevelopmentAdminSeeder>.Instance);

    private static async Task<StaffUser?> FindUserAsync(WebApplicationFactory<Program> host, string email)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>().FindByNameAsync(email);
    }
}