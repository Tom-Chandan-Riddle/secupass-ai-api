using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using SecuPass.Infrastructure.Identity;

namespace SecuPass.Api.Features.Identity.Refresh;

public sealed partial class RefreshHandler(
    SignInManager<StaffUser> signInManager,
    IOptionsMonitor<BearerTokenOptions> bearerTokenOptions,
    TimeProvider timeProvider,
    ILogger<RefreshHandler> logger)
{
    private const string RefreshRejectedTitle = "Invalid refresh token";

    public async Task<Results<SignInHttpResult, ProblemHttpResult>> HandleAsync(RefreshRequest request)
    {
        var refreshTokenProtector = bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var refreshTicket = refreshTokenProtector.Unprotect(request.RefreshToken);
        if (refreshTicket?.Properties.ExpiresUtc is not { } expiresAt)
        {
            return Rejected(RefreshRejection.Unreadable);
        }

        // A16: refresh tokens last 14 days.
        if (timeProvider.GetUtcNow() >= expiresAt)
        {
            return Rejected(RefreshRejection.Expired);
        }

        // A changed security stamp means the user's credentials changed after the token was issued.
        if (await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not { } user)
        {
            return Rejected(RefreshRejection.StaleSecurityStamp);
        }

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        LogTokenRefreshed(logger, user.Id);
        return TypedResults.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    private ProblemHttpResult Rejected(RefreshRejection rejection)
    {
        LogRefreshRejected(logger, rejection);
        return TypedResults.Problem(title: RefreshRejectedTitle, statusCode: StatusCodes.Status401Unauthorized);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Bearer token refreshed for staff user {UserId}")]
    private static partial void LogTokenRefreshed(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token refresh rejected: {Rejection}")]
    private static partial void LogRefreshRejected(ILogger logger, RefreshRejection rejection);

    private enum RefreshRejection
    {
        Unreadable,
        Expired,
        StaleSecurityStamp,
    }
}