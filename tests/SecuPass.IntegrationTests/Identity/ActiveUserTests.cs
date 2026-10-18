using System.Net;

using SecuPass.Domain.Identity;
using SecuPass.IntegrationTests.Support;

using Serilog.Events;

namespace SecuPass.IntegrationTests.Identity;

// Each test changes the database directly, so the active check and the stamp check are proven separately (A23).
public sealed class ActiveUserTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    [Fact]
    public async Task GetCurrentUser_WithTokenOfInactiveUser_Returns401ProblemDetails()
    {
        using var caller = await LoggedInUserAsync();
        await factory.SetActiveInDatabaseAsync(caller.User, isActive: false);

        using var response = await caller.Client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, caller.Tokens.AccessToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_AfterSecurityStampChanged_Returns401ProblemDetails()
    {
        using var caller = await LoggedInUserAsync();
        await factory.UpdateSecurityStampAsync(caller.User);

        using var response = await caller.Client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, caller.Tokens.AccessToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WithTokenOfDeletedUser_Returns401ProblemDetails()
    {
        using var caller = await LoggedInUserAsync();
        await factory.DeleteUserInDatabaseAsync(caller.User);

        using var response = await caller.Client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, caller.Tokens.AccessToken);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    // A24: a token that no longer counts is the same as no token, so anonymous endpoints still answer.
    [Fact]
    public async Task GetHealth_WithTokenOfInactiveUser_Returns200()
    {
        using var caller = await LoggedInUserAsync();
        await factory.SetActiveInDatabaseAsync(caller.User, isActive: false);

        using var response = await caller.Client.GetWithBearerAsync("/health", caller.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // D9: same response as a wrong password, so login doesn't reveal that the account exists but is inactive.
    [Fact]
    public async Task Login_AsInactiveUserWithCorrectPassword_Returns401LikeWrongPassword()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        await factory.SetActiveInDatabaseAsync(user, isActive: false);
        using var client = factory.CreateClient();

        using var inactiveResponse = await client.PostLoginAsync(user.Email, user.Password);
        using var wrongPasswordResponse = await client.PostLoginAsync("nobody@test.secupass.local", user.Password);

        var inactive = await inactiveResponse.ReadProblemAsync(HttpStatusCode.Unauthorized);
        var wrongPassword = await wrongPasswordResponse.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(wrongPassword.GetProperty("title").GetString(), inactive.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Refresh_ForInactiveUser_Returns401ProblemDetails()
    {
        using var caller = await LoggedInUserAsync();
        await factory.SetActiveInDatabaseAsync(caller.User, isActive: false);

        using var response = await caller.Client.PostJsonAsync(IdentityTestExtensions.RefreshPath, new { refreshToken = caller.Tokens.RefreshToken });

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestWithTokenOfInactiveUser_LogsWarningWithUserIdAndNotTheEmail()
    {
        using var caller = await LoggedInUserAsync();
        await factory.SetActiveInDatabaseAsync(caller.User, isActive: false);
        var correlationId = Guid.NewGuid().ToString();

        using var response = await caller.Client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, caller.Tokens.AccessToken, correlationId);

        var requestEvents = factory.LogSink.EventsWithCorrelationId(correlationId);
        Assert.Contains(requestEvents, logEvent =>
            logEvent.Level == LogEventLevel.Warning && CapturingLogSink.PropertyText(logEvent, "UserId") == caller.User.Id.ToString());
        Assert.DoesNotContain(requestEvents, logEvent =>
            logEvent.RenderMessage().Contains(caller.User.Email, StringComparison.OrdinalIgnoreCase)
            || logEvent.Properties.Values.Any(value => value.ToString().Contains(caller.User.Email, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task GetCurrentUser_WithTokenOfActiveUser_Returns200()
    {
        using var caller = await LoggedInUserAsync();

        using var response = await caller.Client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, caller.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<LoggedInUser> LoggedInUserAsync()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        var client = factory.CreateClient();
        var tokens = await client.LoginAsync(user);
        return new LoggedInUser(user, client, tokens);
    }

    private sealed record LoggedInUser(TestStaffUser User, HttpClient Client, TokenResponse Tokens) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}