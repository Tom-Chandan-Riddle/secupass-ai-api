using System.Diagnostics;
using System.Net;

using SecuPass.Domain.Identity;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

// A28: login takes about as long for an unknown email, an inactive user or a locked-out account as for a wrong
// password, so timing doesn't reveal which accounts exist. Checking a password costs about 50 ms, so skipping it
// shows up as a large gap.
public sealed class LoginTimingTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private const string WrongPassword = "Wrong-Passw0rd!";
    private const int FailedAttemptsBeforeLockout = 5;
    private const int WarmUpRounds = 3;
    private const int MeasuredRounds = 7;

    // Generous on purpose: without the dummy check the ratio is about 1 to 50, with it close to 1 to 1.
    private const double MinimumDurationRatio = 0.5;

    [Fact]
    public async Task Login_WithUnknownEmail_TakesAboutAsLongAsWrongPassword()
    {
        using var client = factory.CreateClient();

        var unknownEmailMedian = await MedianDurationAsync(
            round => client.PostLoginAsync($"unknown-{round}-{Guid.NewGuid():N}@test.secupass.local", WrongPassword));

        await AssertAboutAsLongAsWrongPasswordAsync(client, unknownEmailMedian, "Unknown email");
    }

    [Fact]
    public async Task Login_AsInactiveUser_TakesAboutAsLongAsWrongPassword()
    {
        var inactiveUser = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        await factory.SetActiveInDatabaseAsync(inactiveUser, isActive: false);
        using var client = factory.CreateClient();

        var inactiveMedian = await MedianDurationAsync(_ => client.PostLoginAsync(inactiveUser.Email, inactiveUser.Password));

        await AssertAboutAsLongAsWrongPasswordAsync(client, inactiveMedian, "Inactive user");
    }

    [Fact]
    public async Task Login_AsLockedOutUser_TakesAboutAsLongAsWrongPassword()
    {
        var lockedOutUser = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        for (var attempt = 0; attempt < FailedAttemptsBeforeLockout; attempt++)
        {
            using var failedLogin = await client.PostLoginAsync(lockedOutUser.Email, WrongPassword);
        }

        var lockedOutMedian = await MedianDurationAsync(_ => client.PostLoginAsync(lockedOutUser.Email, lockedOutUser.Password));

        await AssertAboutAsLongAsWrongPasswordAsync(client, lockedOutMedian, "Locked-out user");
    }

    // The reference is a wrong password for a user who has never failed before, so lockout never shortens it.
    private async Task AssertAboutAsLongAsWrongPasswordAsync(HttpClient client, double rejectedMedian, string rejectedCase)
    {
        var referenceUsers = new List<TestStaffUser>();
        for (var round = 0; round < WarmUpRounds + MeasuredRounds; round++)
        {
            referenceUsers.Add(await factory.CreateStaffUserAsync(StaffRoles.Staff));
        }

        var wrongPasswordMedian = await MedianDurationAsync(round => client.PostLoginAsync(referenceUsers[round].Email, WrongPassword));

        Assert.True(
            rejectedMedian >= wrongPasswordMedian * MinimumDurationRatio,
            $"{rejectedCase} median {rejectedMedian:F1} ms against wrong password median {wrongPasswordMedian:F1} ms.");
    }

    private static async Task<double> MedianDurationAsync(Func<int, Task<HttpResponseMessage>> rejectedLogin)
    {
        var durations = new List<double>();
        for (var round = 0; round < WarmUpRounds + MeasuredRounds; round++)
        {
            var started = Stopwatch.GetTimestamp();
            using var response = await rejectedLogin(round);
            var elapsed = Stopwatch.GetElapsedTime(started);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            if (round >= WarmUpRounds)
            {
                durations.Add(elapsed.TotalMilliseconds);
            }
        }

        return durations.Order().ElementAt(durations.Count / 2);
    }
}