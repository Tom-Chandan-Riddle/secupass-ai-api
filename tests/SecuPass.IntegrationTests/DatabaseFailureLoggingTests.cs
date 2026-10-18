using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

using SecuPass.Api.Infrastructure;
using SecuPass.Domain.Identity;
using SecuPass.IntegrationTests.Support;

using Serilog.Events;

namespace SecuPass.IntegrationTests;

// D16: EF Core's own failure events stay below Error. A handled database failure logs only the app's Warning,
// and an unexpected one logs a single Error, from the global handler.
public sealed class DatabaseFailureLoggingTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private static readonly string? GlobalExceptionHandlerSource = typeof(GlobalExceptionHandler).FullName;

    // Nothing listens on port 1, so every query fails fast.
    private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=secupass;Username=secupass;Password=unused;Timeout=1";

    [Fact]
    public async Task CreateUser_SameEmailConcurrently_LogsNoErrorForTheConflicts()
    {
        const int simultaneousRequests = 20;
        var admin = await factory.CreateStaffUserAsync(StaffRoles.Admin);
        using var client = factory.CreateClient();
        var accessToken = (await client.LoginAsync(admin)).AccessToken;
        var email = $"race-{Guid.NewGuid():N}@test.secupass.local";
        var correlationIds = Enumerable.Range(0, simultaneousRequests).Select(_ => Guid.NewGuid().ToString()).ToList();

        var responses = await Task.WhenAll(correlationIds.Select(correlationId => client.PostJsonAsync(
            "/users", new { email, password = IdentityTestExtensions.TestPassword, role = StaffRoles.Staff }, correlationId, accessToken)));

        var conflictCorrelationIds = correlationIds.Where((_, index) => responses[index].StatusCode == HttpStatusCode.Conflict).ToList();
        foreach (var response in responses)
        {
            response.Dispose();
        }

        Assert.Equal(simultaneousRequests - 1, conflictCorrelationIds.Count);
        Assert.All(conflictCorrelationIds, correlationId => Assert.DoesNotContain(
            factory.LogSink.EventsWithCorrelationId(correlationId), logEvent => logEvent.Level >= LogEventLevel.Error));
    }

    [Fact]
    public async Task ProtectedRequest_WhileDatabaseUnreachable_LogsOneErrorFromTheGlobalHandler()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        var accessToken = (await client.LoginAsync(user)).AccessToken;
        using var databaseDownHost = factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection([
                new(SecuPassApiFactory.ConnectionStringKey, UnreachableDatabase),
            ])));
        using var databaseDownClient = databaseDownHost.CreateClient();
        var correlationId = Guid.NewGuid().ToString();

        using var response = await databaseDownClient.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, accessToken, correlationId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var errorEvent = Assert.Single(
            factory.LogSink.EventsWithCorrelationId(correlationId), logEvent => logEvent.Level >= LogEventLevel.Error);
        Assert.Equal(GlobalExceptionHandlerSource, CapturingLogSink.PropertyText(errorEvent, "SourceContext"));
    }
}