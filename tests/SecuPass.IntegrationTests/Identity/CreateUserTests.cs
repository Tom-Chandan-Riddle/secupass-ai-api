using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using SecuPass.Domain.Identity;
using SecuPass.Infrastructure.Identity;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

public sealed class CreateUserTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private const string UsersPath = "/users";
    private const string NewUserPassword = "New-User-Passw0rd!";

    private sealed record CreatedUser(Guid UserId, string Email, string Role);

    private sealed record CurrentUserResponse(Guid UserId, string Email, string Role);

    private sealed record LoggedInClient(HttpClient Client, string AccessToken) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    [Fact]
    public async Task CreateUser_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostJsonAsync(UsersPath, NewUserBody(NewEmail(), StaffRoles.Staff));

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(StaffRoles.Staff)]
    [InlineData(StaffRoles.ClientViewer)]
    public async Task CreateUser_AsNonAdmin_Returns403(string callerRole)
    {
        using var caller = await LoggedInClientAsync(callerRole);
        var email = NewEmail();

        using var response = await caller.Client.PostJsonAsync(UsersPath, NewUserBody(email, StaffRoles.Staff), accessToken: caller.AccessToken);

        await response.ReadProblemAsync(HttpStatusCode.Forbidden);
        Assert.Null(await FindUserAsync(email));
    }

    // A20: only the exact names Admin and Staff. ClientViewer waits for F02.2's client scope.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Volunteer")]
    [InlineData("admin")]
    [InlineData(StaffRoles.ClientViewer)]
    public async Task CreateUser_WithMissingOrUnsupportedRole_Returns400WithRoleError(string? role)
    {
        using var caller = await LoggedInClientAsync(StaffRoles.Admin);
        var email = NewEmail();

        using var response = await caller.Client.PostJsonAsync(UsersPath, NewUserBody(email, role), accessToken: caller.AccessToken);

        await AssertFieldErrorAsync(response, "Role");
        Assert.Null(await FindUserAsync(email));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task CreateUser_WithMissingOrMalformedEmail_Returns400WithEmailError(string? email)
    {
        using var caller = await LoggedInClientAsync(StaffRoles.Admin);

        using var response = await caller.Client.PostJsonAsync(UsersPath, NewUserBody(email, StaffRoles.Staff), accessToken: caller.AccessToken);

        await AssertFieldErrorAsync(response, "Email");
    }

    [Fact]
    public async Task CreateUser_WithMissingPassword_Returns400WithPasswordError()
    {
        using var caller = await LoggedInClientAsync(StaffRoles.Admin);

        using var response = await caller.Client.PostJsonAsync(
            UsersPath, new { email = NewEmail(), role = StaffRoles.Staff }, accessToken: caller.AccessToken);

        await AssertFieldErrorAsync(response, "Password");
    }

    // A16: Identity's password rules, with Identity's own reasons.
    [Fact]
    public async Task CreateUser_WithWeakPassword_Returns400WithIdentityReasonsAndCreatesNoUser()
    {
        using var caller = await LoggedInClientAsync(StaffRoles.Admin);
        var email = NewEmail();

        using var response = await caller.Client.PostJsonAsync(
            UsersPath, NewUserBody(email, StaffRoles.Staff, password: "weak"), accessToken: caller.AccessToken);

        var passwordErrors = await AssertFieldErrorAsync(response, "Password");
        Assert.Contains(passwordErrors, error => error.StartsWith("Passwords must", StringComparison.Ordinal));
        Assert.Null(await FindUserAsync(email));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateUser_WithExistingEmail_Returns409(bool differentCase)
    {
        var existingUser = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var caller = await LoggedInClientAsync(StaffRoles.Admin);
        var email = differentCase ? existingUser.Email.ToUpperInvariant() : existingUser.Email;

        using var response = await caller.Client.PostJsonAsync(UsersPath, NewUserBody(email, StaffRoles.Admin), accessToken: caller.AccessToken);

        await response.ReadProblemAsync(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(StaffRoles.Admin)]
    [InlineData(StaffRoles.Staff)]
    public async Task CreateUser_AsAdmin_Returns201AndTheUserCanLogInWithTheRole(string role)
    {
        using var caller = await LoggedInClientAsync(StaffRoles.Admin);
        var email = NewEmail();

        using var response = await caller.Client.PostJsonAsync(UsersPath, NewUserBody(email, role), accessToken: caller.AccessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location); // A21
        var created = await response.Content.ReadFromJsonAsync<CreatedUser>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        Assert.Equal((email, role), (created.Email, created.Role));
        var newUserTokens = await caller.Client.LoginAsync(new TestStaffUser(created.UserId, email, NewUserPassword));
        using var currentUserResponse = await caller.Client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, newUserTokens.AccessToken);
        var currentUser = await currentUserResponse.Content.ReadFromJsonAsync<CurrentUserResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(new CurrentUserResponse(created.UserId, email, role), currentUser);
    }

    // Identity stores emails in varchar(256): one character more must be a 400, not a database error.
    [Theory]
    [InlineData(256, HttpStatusCode.Created)]
    [InlineData(257, HttpStatusCode.BadRequest)]
    public async Task CreateUser_AtEmailLengthBoundary_AcceptsUpTo256Characters(int length, HttpStatusCode expectedStatus)
    {
        using var caller = await LoggedInClientAsync(StaffRoles.Admin);
        var email = EmailOfLength(length);

        using var response = await caller.Client.PostJsonAsync(UsersPath, NewUserBody(email, StaffRoles.Staff), accessToken: caller.AccessToken);

        if (expectedStatus == HttpStatusCode.BadRequest)
        {
            await AssertFieldErrorAsync(response, "Email");
            Assert.Null(await FindUserAsync(email));
            return;
        }

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    // A27: an email containing whitespace would become a second account that can't log in as typed.
    [Theory]
    [InlineData(" lead-{0}@test.secupass.local")]
    [InlineData("trail-{0}@test.secupass.local ")]
    [InlineData("in ner-{0}@test.secupass.local")]
    [InlineData("tab-{0}@test.secupass.local\t")]
    public async Task CreateUser_WithWhitespaceInEmail_Returns400WithEmailErrorAndCreatesNoUser(string emailFormat)
    {
        using var caller = await LoggedInClientAsync(StaffRoles.Admin);
        var email = string.Format(System.Globalization.CultureInfo.InvariantCulture, emailFormat, Guid.NewGuid().ToString("N"));

        using var response = await caller.Client.PostJsonAsync(UsersPath, NewUserBody(email, StaffRoles.Staff), accessToken: caller.AccessToken);

        await AssertFieldErrorAsync(response, "Email");
        Assert.Null(await FindUserAsync(email));
    }

    // Identity checks for a duplicate before inserting, so simultaneous creates race to the unique index.
    [Fact]
    public async Task CreateUser_SameEmailConcurrently_ReturnsOne201AndOtherwiseOnly409()
    {
        const int simultaneousRequests = 20;
        using var caller = await LoggedInClientAsync(StaffRoles.Admin);
        var email = NewEmail();

        var responses = await Task.WhenAll(Enumerable.Range(0, simultaneousRequests).Select(_ =>
            caller.Client.PostJsonAsync(UsersPath, NewUserBody(email, StaffRoles.Staff), accessToken: caller.AccessToken)));

        var statuses = responses.Select(response => response.StatusCode).ToList();
        foreach (var response in responses)
        {
            response.Dispose();
        }

        Assert.Single(statuses, status => status == HttpStatusCode.Created);
        Assert.All(statuses.Where(status => status != HttpStatusCode.Created), status => Assert.Equal(HttpStatusCode.Conflict, status));
    }

    // A22: the user name is the email, so any well-formed email must be accepted.
    [Fact]
    public async Task CreateUser_WithApostropheInEmail_Returns201()
    {
        using var caller = await LoggedInClientAsync(StaffRoles.Admin);
        var email = $"o'brien-{Guid.NewGuid():N}@test.secupass.local";

        using var response = await caller.Client.PostJsonAsync(UsersPath, NewUserBody(email, StaffRoles.Staff), accessToken: caller.AccessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // The role assignment fails here because a fresh database has its Staff role removed first.
    [Fact]
    public async Task CreateUser_WhenRoleAssignmentFails_Returns500AndCreatesNoUser()
    {
        using var isolatedHost = factory.WithFreshDatabase();
        var admin = await isolatedHost.CreateStaffUserAsync(StaffRoles.Admin);
        using var client = isolatedHost.CreateClient();
        var accessToken = (await client.LoginAsync(admin)).AccessToken;
        await DeleteRoleAsync(isolatedHost, StaffRoles.Staff);
        var email = NewEmail();

        using var response = await client.PostJsonAsync(UsersPath, NewUserBody(email, StaffRoles.Staff), accessToken: accessToken);

        await response.ReadProblemAsync(HttpStatusCode.InternalServerError);
        Assert.Null(await FindUserAsync(isolatedHost, email));
    }

    [Fact]
    public async Task CreateUser_AsAdmin_LogsNewUserIdAndActorButNotTheEmail()
    {
        var admin = await factory.CreateStaffUserAsync(StaffRoles.Admin);
        using var client = factory.CreateClient();
        var accessToken = (await client.LoginAsync(admin)).AccessToken;
        var email = NewEmail();
        var correlationId = Guid.NewGuid().ToString();

        using var response = await client.PostJsonAsync(UsersPath, NewUserBody(email, StaffRoles.Staff), correlationId, accessToken);

        var created = await response.Content.ReadFromJsonAsync<CreatedUser>(TestContext.Current.CancellationToken);
        Assert.NotNull(created);
        var requestEvents = factory.LogSink.EventsWithCorrelationId(correlationId);
        Assert.Contains(requestEvents, logEvent =>
            CapturingLogSink.PropertyText(logEvent, "UserId") == created.UserId.ToString()
            && CapturingLogSink.PropertyText(logEvent, "ActorId") == admin.Id.ToString());
        Assert.DoesNotContain(requestEvents, logEvent =>
            logEvent.RenderMessage().Contains(email, StringComparison.OrdinalIgnoreCase)
            || logEvent.Properties.Values.Any(value => value.ToString().Contains(email, StringComparison.OrdinalIgnoreCase)));
    }

    private static string NewEmail() => $"new-{Guid.NewGuid():N}@test.secupass.local";

    private static string EmailOfLength(int length)
    {
        const string domain = "@test.secupass.local";
        var uniquePrefix = Guid.NewGuid().ToString("N");
        return uniquePrefix + new string('a', length - uniquePrefix.Length - domain.Length) + domain;
    }

    private static object NewUserBody(string? email, string? role, string password = NewUserPassword) =>
        new { email, password, role };

    private async Task<LoggedInClient> LoggedInClientAsync(string role)
    {
        var caller = await factory.CreateStaffUserAsync(role);
        var client = factory.CreateClient();
        var tokens = await client.LoginAsync(caller);
        return new LoggedInClient(client, tokens.AccessToken);
    }

    private static async Task<string[]> AssertFieldErrorAsync(HttpResponseMessage response, string field)
    {
        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        var fieldErrors = problem.GetProperty("errors").EnumerateObject()
            .SingleOrDefault(error => error.Name == field);
        Assert.Equal(JsonValueKind.Array, fieldErrors.Value.ValueKind);
        return [.. fieldErrors.Value.EnumerateArray().Select(error => error.GetString() ?? string.Empty)];
    }

    private Task<StaffUser?> FindUserAsync(string email) => FindUserAsync(factory, email);

    private static async Task<StaffUser?> FindUserAsync(WebApplicationFactory<Program> host, string email)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>().FindByNameAsync(email);
    }

    private static async Task DeleteRoleAsync(WebApplicationFactory<Program> host, string roleName)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var role = await roleManager.FindByNameAsync(roleName) ?? throw new InvalidOperationException($"Role {roleName} not found.");
        await roleManager.DeleteAsync(role);
    }
}