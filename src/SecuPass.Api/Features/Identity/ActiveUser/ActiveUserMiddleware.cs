using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SecuPass.Infrastructure.Persistence;

namespace SecuPass.Api.Features.Identity.ActiveUser;

// A23: a token counts only while its user exists, is active and keeps the security stamp it was issued with.
public sealed partial class ActiveUserMiddleware(RequestDelegate next, ILogger<ActiveUserMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, SecuPassDbContext dbContext, IOptions<IdentityOptions> identityOptions)
    {
        if (context.User.Identity?.IsAuthenticated == true && RequiresLoggedInUser(context.GetEndpoint()))
        {
            await DropStaleTokenAsync(context, dbContext, identityOptions.Value.ClaimsIdentity);
        }

        await next(context);
    }

    private async Task DropStaleTokenAsync(HttpContext context, SecuPassDbContext dbContext, ClaimsIdentityOptions claimTypes)
    {
        var userId = Guid.Parse(RequiredClaim(context.User, claimTypes.UserIdClaimType));
        var tokenSecurityStamp = RequiredClaim(context.User, claimTypes.SecurityStampClaimType);
        var currentUser = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.IsActive, user.SecurityStamp })
            .SingleOrDefaultAsync(context.RequestAborted);

        TokenRejection? rejection = currentUser switch
        {
            null => TokenRejection.UserNotFound,
            { IsActive: false } => TokenRejection.Inactive,
            _ when currentUser.SecurityStamp != tokenSecurityStamp => TokenRejection.StaleSecurityStamp,
            _ => null,
        };
        if (rejection is not { } reason)
        {
            return;
        }

        LogTokenDropped(logger, reason, userId);
        // A24: the request carries on as anonymous, so protected endpoints answer 401 and anonymous ones still work.
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
    }

    // A30: anonymous endpoints, unknown routes and wrong methods never query the user, so /health never touches the database.
    private static bool RequiresLoggedInUser(Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<IAuthorizeData>() is not null
        && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;

    // This API issues and protects its tokens, so a missing claim is a bug, not bad input.
    private static string RequiredClaim(ClaimsPrincipal user, string claimType) =>
        user.FindFirstValue(claimType) ?? throw new InvalidOperationException($"The token has no {claimType} claim.");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token no longer counts: {Rejection} for staff user {UserId}")]
    private static partial void LogTokenDropped(ILogger logger, TokenRejection rejection, Guid userId);

    private enum TokenRejection
    {
        UserNotFound,
        Inactive,
        StaleSecurityStamp,
    }
}