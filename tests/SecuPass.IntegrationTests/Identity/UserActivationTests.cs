using System.Net;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using SecuPass.Domain.Identity;
using SecuPass.Infrastructure.Identity;
using SecuPass.Infrastructure.Persistence;
using SecuPass.IntegrationTests.Support;

using Serilog.Events;

namespace SecuPass.IntegrationTests.Identity;

public sealed class UserActivationTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private const string Deactivate = "deactivate";
    private const string Reactivate = "reactivate";

    [Theory]
    [InlineData(Deactivate)]
    [InlineData(Reactivate)]
    public async Task ChangeActivation_WithoutToken_Returns401(string action)
    {
        var target = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();

        using var response = await client.PostJsonAsync(ActionPath(target.Id, action), new { });

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(Deactivate, StaffRoles.Staff)]
    [InlineData(Deactivate, StaffRoles.ClientViewer)]
    [InlineData(Reactivate, StaffRoles.Staff)]
    [InlineData(Reactivate, StaffRoles.ClientViewer)]
    public async Task ChangeActivation_AsNonAdmin_Returns403AndChangesNothing(string action, string callerRole)
    {
        var target = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var caller = await LoggedInClientAsync(callerRole);

        using var response = await caller.Client.PostJsonAsync(ActionPath(target.Id, action), new { }, accessToken: caller.Tokens.AccessToken);

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.True(await IsActiveAsync(target));
    }

    [Theory]
    [InlineData(Deactivate)]
    [InlineData(Reactivate)]
    public async Task ChangeActivation_ForUnknownUser_Returns404ProblemDetails(string action)
    {
        using var admin = await LoggedInClientAsync(StaffRoles.Admin);

        using var response = await admin.Client.PostJsonAsync(ActionPath(Guid.NewGuid(), action), new { }, accessToken: admin.Tokens.AccessToken);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deactivate_WithMalformedUserId_Returns404()
    {
        using var admin = await LoggedInClientAsync(StaffRoles.Admin);

        using var response = await admin.Client.PostJsonAsync("/users/not-a-guid/deactivate", new { }, accessToken: admin.Tokens.AccessToken);

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    // A25: an Admin can't lock themselves out.
    [Fact]
    public async Task Deactivate_OwnAccount_Returns422AndStaysActive()
    {
        using var admin = await LoggedInClientAsync(StaffRoles.Admin);

        using var response = await admin.Client.PostJsonAsync(ActionPath(admin.User.Id, Deactivate), new { }, accessToken: admin.Tokens.AccessToken);

        await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity);
        Assert.True(await IsActiveAsync(admin.User));
    }

    // D9 and A23: the target's existing tokens stop working on their next request, and login fails.
    [Fact]
    public async Task Deactivate_ActiveUser_Returns204AndRevokesTheirTokensAndLogin()
    {
        using var admin = await LoggedInClientAsync(StaffRoles.Admin);
        using var target = await LoggedInClientAsync(StaffRoles.Staff);

        using var response = await admin.Client.PostJsonAsync(ActionPath(target.User.Id, Deactivate), new { }, accessToken: admin.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await IsActiveAsync(target.User));
        using var currentUserResponse = await target.Client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, target.Tokens.AccessToken);
        await currentUserResponse.ReadProblemAsync(HttpStatusCode.Unauthorized);
        using var refreshResponse = await target.Client.PostJsonAsync(IdentityTestExtensions.RefreshPath, new { refreshToken = target.Tokens.RefreshToken });
        await refreshResponse.ReadProblemAsync(HttpStatusCode.Unauthorized);
        using var loginResponse = await target.Client.PostLoginAsync(target.User.Email, target.User.Password);
        await loginResponse.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    // A23: reactivation lets the user log in again, but tokens from before the deactivation stay dead.
    [Fact]
    public async Task Reactivate_DeactivatedUser_Returns204AndLoginWorksButOldTokenStaysDead()
    {
        using var admin = await LoggedInClientAsync(StaffRoles.Admin);
        using var target = await LoggedInClientAsync(StaffRoles.Staff);
        using var deactivateResponse = await admin.Client.PostJsonAsync(ActionPath(target.User.Id, Deactivate), new { }, accessToken: admin.Tokens.AccessToken);

        using var response = await admin.Client.PostJsonAsync(ActionPath(target.User.Id, Reactivate), new { }, accessToken: admin.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await IsActiveAsync(target.User));
        using var loginResponse = await target.Client.PostLoginAsync(target.User.Email, target.User.Password);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        using var oldTokenResponse = await target.Client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, target.Tokens.AccessToken);
        await oldTokenResponse.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    // A26: asking for the state the user is already in succeeds and changes nothing.
    [Fact]
    public async Task Deactivate_AlreadyInactiveUser_Returns204AndKeepsTheSecurityStamp()
    {
        using var admin = await LoggedInClientAsync(StaffRoles.Admin);
        var target = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        await factory.SetActiveInDatabaseAsync(target, isActive: false);
        var stampBefore = await SecurityStampAsync(target);

        using var response = await admin.Client.PostJsonAsync(ActionPath(target.Id, Deactivate), new { }, accessToken: admin.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await IsActiveAsync(target));
        Assert.Equal(stampBefore, await SecurityStampAsync(target));
    }

    [Fact]
    public async Task Reactivate_ActiveUser_Returns204AndStaysActive()
    {
        using var admin = await LoggedInClientAsync(StaffRoles.Admin);
        var target = await factory.CreateStaffUserAsync(StaffRoles.Staff);

        using var response = await admin.Client.PostJsonAsync(ActionPath(target.Id, Reactivate), new { }, accessToken: admin.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(await IsActiveAsync(target));
    }

    // A29: simultaneous repeats race on Identity's concurrency stamp. The losers reload, find the work done, and return 204.
    [Theory]
    [InlineData(Deactivate)]
    [InlineData(Reactivate)]
    public async Task ChangeActivation_SameUserConcurrently_AllReturn204AndChangeItOnce(string action)
    {
        const int simultaneousRequests = 10;
        using var admin = await LoggedInClientAsync(StaffRoles.Admin);
        var target = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        if (action == Reactivate)
        {
            await factory.SetActiveInDatabaseAsync(target, isActive: false);
        }

        var responses = await Task.WhenAll(Enumerable.Range(0, simultaneousRequests).Select(_ =>
            admin.Client.PostJsonAsync(ActionPath(target.Id, action), new { }, accessToken: admin.Tokens.AccessToken)));

        var statuses = responses.Select(response => response.StatusCode).ToList();
        foreach (var response in responses)
        {
            response.Dispose();
        }

        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.NoContent, status));
        Assert.Equal(action == Reactivate, await IsActiveAsync(target));
        Assert.Single(factory.LogSink.AllEvents, logEvent =>
            logEvent.Level == LogEventLevel.Information
            && CapturingLogSink.PropertyText(logEvent, "UserId") == target.Id.ToString()
            && logEvent.MessageTemplate.Text.Contains($"{action}d", StringComparison.Ordinal));
    }

    // A29: another write (like a failed login updating the lockout counter) changes the user's concurrency stamp just
    // before the deactivation saves. The handler must re-read the user, not trust its own unsaved change, and try again.
    [Fact]
    public async Task Deactivate_WhenAnotherWriteChangesTheUserFirst_RetriesAndEndsInactive()
    {
        var target = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        var concurrentEdit = new ConcurrentEditBeforeFirstSave(target.Id);
        using var conflictHost = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.ConfigureDbContext<SecuPassDbContext>(options => options.AddInterceptors(concurrentEdit))));
        var admin = await conflictHost.CreateStaffUserAsync(StaffRoles.Admin);
        using var client = conflictHost.CreateClient();
        var tokens = await client.LoginAsync(admin);

        using var response = await client.PostJsonAsync(ActionPath(target.Id, Deactivate), new { }, accessToken: tokens.AccessToken);

        Assert.True(concurrentEdit.HasEdited);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await IsActiveAsync(target));
    }

    [Theory]
    [InlineData(Deactivate)]
    [InlineData(Reactivate)]
    public async Task ChangeActivation_LogsInformationWithUserIdAndActorButNotTheEmail(string action)
    {
        using var admin = await LoggedInClientAsync(StaffRoles.Admin);
        var target = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        if (action == Reactivate)
        {
            await factory.SetActiveInDatabaseAsync(target, isActive: false);
        }

        var correlationId = Guid.NewGuid().ToString();

        using var response = await admin.Client.PostJsonAsync(ActionPath(target.Id, action), new { }, correlationId, admin.Tokens.AccessToken);

        var requestEvents = factory.LogSink.EventsWithCorrelationId(correlationId);
        Assert.Contains(requestEvents, logEvent =>
            logEvent.Level == LogEventLevel.Information
            && CapturingLogSink.PropertyText(logEvent, "UserId") == target.Id.ToString()
            && CapturingLogSink.PropertyText(logEvent, "ActorId") == admin.User.Id.ToString());
        Assert.DoesNotContain(requestEvents, logEvent =>
            logEvent.RenderMessage().Contains(target.Email, StringComparison.OrdinalIgnoreCase)
            || logEvent.Properties.Values.Any(value => value.ToString().Contains(target.Email, StringComparison.OrdinalIgnoreCase)));
    }

    private static string ActionPath(Guid userId, string action) => $"/users/{userId}/{action}";

    private async Task<LoggedInUser> LoggedInClientAsync(string role)
    {
        var user = await factory.CreateStaffUserAsync(role);
        var client = factory.CreateClient();
        var tokens = await client.LoginAsync(user);
        return new LoggedInUser(user, client, tokens);
    }

    private async Task<bool> IsActiveAsync(TestStaffUser testUser) => (await FindUserAsync(testUser)).IsActive;

    private async Task<string?> SecurityStampAsync(TestStaffUser testUser) => (await FindUserAsync(testUser)).SecurityStamp;

    private async Task<StaffUser> FindUserAsync(TestStaffUser testUser)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>().FindByIdAsync(testUser.Id.ToString());
        return user ?? throw new InvalidOperationException("Test user not found.");
    }

    // Plays the part of another request: changes the user's concurrency stamp on a separate connection, once.
    private sealed class ConcurrentEditBeforeFirstSave(Guid userId) : SaveChangesInterceptor
    {
        public bool HasEdited { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!HasEdited && eventData.Context is { } context && IsSavingUser(context))
            {
                HasEdited = true;
                await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);
                await using var command = new NpgsqlCommand(
                    """UPDATE "AspNetUsers" SET "ConcurrencyStamp" = @stamp WHERE "Id" = @id""", connection);
                command.Parameters.AddWithValue("stamp", Guid.NewGuid().ToString());
                command.Parameters.AddWithValue("id", userId);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private bool IsSavingUser(DbContext context) =>
            context.ChangeTracker.Entries<StaffUser>()
                .Any(entry => entry.State == EntityState.Modified && entry.Entity.Id == userId);
    }

    private sealed record LoggedInUser(TestStaffUser User, HttpClient Client, TokenResponse Tokens) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}