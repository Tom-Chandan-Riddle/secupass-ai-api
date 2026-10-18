using System.Net;

using SecuPass.Domain.Identity;
using SecuPass.IntegrationTests.Support;

namespace SecuPass.IntegrationTests.Identity;

// Every Identity error response carries the request's correlation ID in its ProblemDetails (F00, review N4).
public sealed class IdentityErrorCorrelationTests(SecuPassApiFactory factory) : IClassFixture<SecuPassApiFactory>
{
    private const string LoginValidation = "login with a missing password";
    private const string LoginRejected = "login with a wrong password";
    private const string RefreshRejected = "refresh with an invalid token";
    private const string CreateUserWeakPassword = "create user with a weak password";
    private const string CreateUserAsStaff = "create user as Staff";
    private const string DeactivateUnknownUser = "deactivate an unknown user";
    private const string CreateUserDuplicate = "create user with a taken email";
    private const string DeactivateOwnAccount = "deactivate your own account";

    public static TheoryData<string, HttpStatusCode> ErrorCases => new()
    {
        { LoginValidation, HttpStatusCode.BadRequest },
        { LoginRejected, HttpStatusCode.Unauthorized },
        { RefreshRejected, HttpStatusCode.Unauthorized },
        { CreateUserWeakPassword, HttpStatusCode.BadRequest },
        { CreateUserAsStaff, HttpStatusCode.Forbidden },
        { DeactivateUnknownUser, HttpStatusCode.NotFound },
        { CreateUserDuplicate, HttpStatusCode.Conflict },
        { DeactivateOwnAccount, HttpStatusCode.UnprocessableEntity },
    };

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public async Task IdentityErrorResponse_IncludesTheRequestsCorrelationId(string errorCase, HttpStatusCode expectedStatus)
    {
        var admin = await factory.CreateStaffUserAsync(StaffRoles.Admin);
        var staff = await factory.CreateStaffUserAsync(StaffRoles.Staff);
        using var client = factory.CreateClient();
        var adminToken = (await client.LoginAsync(admin)).AccessToken;
        var staffToken = (await client.LoginAsync(staff)).AccessToken;
        var correlationId = Guid.NewGuid().ToString();
        var newUser = new { email = $"new-{Guid.NewGuid():N}@test.secupass.local", password = IdentityTestExtensions.TestPassword, role = StaffRoles.Staff };

        using var response = errorCase switch
        {
            LoginValidation => await client.PostJsonAsync(IdentityTestExtensions.LoginPath, new { email = staff.Email }, correlationId),
            LoginRejected => await client.PostLoginAsync(staff.Email, "Wrong-Passw0rd!", correlationId),
            RefreshRejected => await client.PostJsonAsync(IdentityTestExtensions.RefreshPath, new { refreshToken = "not-a-token" }, correlationId),
            CreateUserWeakPassword => await client.PostJsonAsync("/users", newUser with { password = "weak" }, correlationId, adminToken),
            CreateUserAsStaff => await client.PostJsonAsync("/users", newUser, correlationId, staffToken),
            DeactivateUnknownUser => await client.PostJsonAsync($"/users/{Guid.NewGuid()}/deactivate", new { }, correlationId, adminToken),
            CreateUserDuplicate => await client.PostJsonAsync("/users", newUser with { email = staff.Email }, correlationId, adminToken),
            DeactivateOwnAccount => await client.PostJsonAsync($"/users/{admin.Id}/deactivate", new { }, correlationId, adminToken),
            _ => throw new ArgumentOutOfRangeException(nameof(errorCase), errorCase, "Unknown error case."),
        };

        var problem = await response.ReadProblemAsync(expectedStatus);
        Assert.Equal(correlationId, problem.GetProperty("correlationId").GetString());
    }
}