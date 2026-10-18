using System.Net;
using System.Net.Http.Json;

using SecuPass.Domain.Identity;
using SecuPass.IntegrationTests.Support;

using Serilog.Events;

namespace SecuPass.IntegrationTests.Identity;

public sealed class LoginTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private const string WrongPassword = "Wrong-Passw0rd!";
    private const int FailedAttemptsBeforeLockout = 5;

    [Fact]
    public async Task Login_WithWrongPassword_Returns401ProblemDetails()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();

        using var response = await client.PostLoginAsync(user.Email, WrongPassword);

        await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
    }

    // An unknown email must look exactly like a wrong password, so login can't be used to find accounts.
    [Fact]
    public async Task Login_WithUnknownEmail_Returns401LikeWrongPassword()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();

        using var wrongPasswordResponse = await client.PostLoginAsync(user.Email, WrongPassword);
        using var unknownEmailResponse = await client.PostLoginAsync("nobody@test.secupass.local", WrongPassword);

        var wrongPassword = await wrongPasswordResponse.ReadProblemAsync(HttpStatusCode.Unauthorized);
        var unknownEmail = await unknownEmailResponse.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(wrongPassword.GetProperty("title").GetString(), unknownEmail.GetProperty("title").GetString());
        Assert.Equal(DetailOf(wrongPassword), DetailOf(unknownEmail));
    }

    [Theory]
    [InlineData("""{ "password": "Test-Passw0rd!" }""", "email")]
    [InlineData("""{ "email": "   ", "password": "Test-Passw0rd!" }""", "email")]
    [InlineData("""{ "email": "someone@test.secupass.local" }""", "password")]
    [InlineData("""{ "email": "someone@test.secupass.local", "password": "" }""", "password")]
    public async Task Login_WithMissingOrBlankField_Returns400WithFieldError(string body, string expectedField)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(IdentityTestExtensions.LoginPath, content, TestContext.Current.CancellationToken);

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        var fieldNames = problem.GetProperty("errors").EnumerateObject().Select(field => field.Name);
        Assert.Contains(expectedField, fieldNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithBearerToken()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();

        using var response = await client.PostLoginAsync(user.Email, user.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(tokens);
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal((long)TimeSpan.FromHours(1).TotalSeconds, tokens.ExpiresIn); // A16
    }

    [Fact]
    public async Task Login_WithDifferentlyCasedEmail_Returns200()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();

        using var response = await client.PostLoginAsync(user.Email.ToUpperInvariant(), user.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // A16: the fifth failure locks the account, so even the right password is refused, with the same 401.
    [Fact]
    public async Task Login_AfterFiveFailedAttempts_Returns401EvenWithCorrectPassword()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        await FailLoginAsync(client, user, FailedAttemptsBeforeLockout);
        using var wrongPasswordResponse = await client.PostLoginAsync(user.Email, WrongPassword);

        using var response = await client.PostLoginAsync(user.Email, user.Password);

        var lockedOut = await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
        var wrongPassword = await wrongPasswordResponse.ReadProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Equal(wrongPassword.GetProperty("title").GetString(), lockedOut.GetProperty("title").GetString());
        Assert.Equal(DetailOf(wrongPassword), DetailOf(lockedOut));
    }

    [Fact]
    public async Task Login_AfterFourFailedAttempts_StillReturns200()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        await FailLoginAsync(client, user, FailedAttemptsBeforeLockout - 1);

        using var response = await client.PostLoginAsync(user.Email, user.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WhenAccepted_LogsInformationWithUserId()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();

        using var response = await client.PostLoginAsync(user.Email, user.Password, correlationId);

        Assert.Contains(
            factory.LogSink.EventsWithCorrelationId(correlationId),
            logEvent => logEvent.Level == LogEventLevel.Information
                && CapturingLogSink.PropertyText(logEvent, "UserId") == user.Id.ToString());
    }

    [Fact]
    public async Task Login_WhenRejected_LogsWarningWithoutTheEmail()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();

        using var response = await client.PostLoginAsync(user.Email, WrongPassword, correlationId);

        var requestEvents = factory.LogSink.EventsWithCorrelationId(correlationId);
        Assert.Contains(requestEvents, logEvent => logEvent.Level == LogEventLevel.Warning);
        Assert.DoesNotContain(requestEvents, logEvent => MentionsText(logEvent, user.Email));
    }

    // A13 and F00 review N1: the first method-specific endpoint answers a wrong method with 405 ProblemDetails.
    [Fact]
    public async Task GetLogin_Returns405ProblemDetails()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(IdentityTestExtensions.LoginPath, TestContext.Current.CancellationToken);

        await response.ReadProblemAsync(HttpStatusCode.MethodNotAllowed);
    }

    [Theory]
    [InlineData("/register")]
    [InlineData("/auth/register")]
    public async Task Register_IsNotExposed_Returns404(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.PostJsonAsync(path, new { email = "new@test.secupass.local", password = IdentityTestExtensions.TestPassword });

        await response.ReadProblemAsync(HttpStatusCode.NotFound);
    }

    private static async Task FailLoginAsync(HttpClient client, TestStaffUser user, int attempts)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            using var response = await client.PostLoginAsync(user.Email, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private static string? DetailOf(System.Text.Json.JsonElement problem) =>
        problem.TryGetProperty("detail", out var detail) ? detail.GetString() : null;

    private static bool MentionsText(LogEvent logEvent, string text) =>
        logEvent.RenderMessage().Contains(text, StringComparison.OrdinalIgnoreCase)
        || logEvent.Properties.Values.Any(value => value.ToString().Contains(text, StringComparison.OrdinalIgnoreCase));
}