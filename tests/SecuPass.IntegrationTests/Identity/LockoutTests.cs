using System.Net;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using SecuPass.Domain.Identity;
using SecuPass.Infrastructure.Identity;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

// A16: five failed logins lock the account for five minutes. Identity's lockout reads the system clock, not the
// injected TimeProvider, so these tests check and move LockoutEnd directly instead of advancing a fake clock.
public sealed class LockoutTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private const string WrongPassword = "Wrong-Passw0rd!";
    private const int FailedAttemptsBeforeLockout = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task Login_FifthFailure_LocksOutForFiveMinutes()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        var lockedAt = TimeProvider.System.GetUtcNow();

        await FailLoginAsync(client, user, FailedAttemptsBeforeLockout);

        var lockoutEnd = await LockoutEndAsync(user);
        Assert.NotNull(lockoutEnd);
        Assert.InRange(lockoutEnd.Value, lockedAt + LockoutDuration - ClockTolerance, lockedAt + LockoutDuration + ClockTolerance);
    }

    [Fact]
    public async Task Login_AfterLockoutEndHasPassed_Returns200()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        await FailLoginAsync(client, user, FailedAttemptsBeforeLockout);
        await SetLockoutEndAsync(user, TimeProvider.System.GetUtcNow().AddSeconds(-1));

        using var response = await client.PostLoginAsync(user.Email, user.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Pins the chosen behaviour (review N3): lockout guards password guessing, so tokens issued before it keep
    // working, as with Identity's own refresh endpoint. Deactivation (D9) is what ends a session.
    [Fact]
    public async Task Refresh_WhileLockedOut_Returns200AndTheTokenStillWorks()
    {
        var user = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        var tokens = await client.LoginAsync(user);
        await FailLoginAsync(client, user, FailedAttemptsBeforeLockout);

        using var refreshResponse = await client.PostJsonAsync(IdentityTestExtensions.RefreshPath, new { refreshToken = tokens.RefreshToken });
        using var currentUserResponse = await client.GetWithBearerAsync(IdentityTestExtensions.CurrentUserPath, tokens.AccessToken);

        Assert.NotNull(await LockoutEndAsync(user));
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, currentUserResponse.StatusCode);
    }

    private static async Task FailLoginAsync(HttpClient client, TestStaffUser user, int attempts)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            using var response = await client.PostLoginAsync(user.Email, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    private async Task<DateTimeOffset?> LockoutEndAsync(TestStaffUser testUser)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        return await userManager.GetLockoutEndDateAsync(await FindUserAsync(userManager, testUser));
    }

    private async Task SetLockoutEndAsync(TestStaffUser testUser, DateTimeOffset lockoutEnd)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<StaffUser>>();
        var result = await userManager.SetLockoutEndDateAsync(await FindUserAsync(userManager, testUser), lockoutEnd);
        Assert.True(result.Succeeded);
    }

    private static async Task<StaffUser> FindUserAsync(UserManager<StaffUser> userManager, TestStaffUser testUser) =>
        await userManager.FindByIdAsync(testUser.Id.ToString()) ?? throw new InvalidOperationException("Test user not found.");
}