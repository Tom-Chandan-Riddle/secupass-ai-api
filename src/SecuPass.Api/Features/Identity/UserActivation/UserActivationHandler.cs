using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

using SecuPass.Infrastructure.Identity;

namespace SecuPass.Api.Features.Identity.UserActivation;

public sealed partial class UserActivationHandler(UserManager<StaffUser> userManager, ILogger<UserActivationHandler> logger)
{
    private const string UserNotFoundTitle = "User not found";
    private const string OwnAccountTitle = "You can't deactivate your own account";

    public async Task<Results<NoContent, ProblemHttpResult>> DeactivateAsync(Guid userId, ClaimsPrincipal actor)
    {
        var actorId = userManager.GetUserId(actor);
        // A25: an Admin can't lock themselves out.
        if (Guid.TryParse(actorId, out var actorUserId) && actorUserId == userId)
        {
            LogActivationRejected(logger, ActivationRejection.OwnAccount, userId, actorId);
            return TypedResults.Problem(title: OwnAccountTitle, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        if (await userManager.FindByIdAsync(userId.ToString()) is not { } user)
        {
            return UserNotFound(userId, actorId);
        }

        // A26: already inactive, so there is nothing to change.
        if (!user.IsActive)
        {
            return TypedResults.NoContent();
        }

        user.Deactivate();
        // Saves the flag together with a new stamp, so the user's existing tokens stop counting (A23).
        ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
        LogUserDeactivated(logger, userId, actorId);
        return TypedResults.NoContent();
    }

    public async Task<Results<NoContent, ProblemHttpResult>> ReactivateAsync(Guid userId, ClaimsPrincipal actor)
    {
        var actorId = userManager.GetUserId(actor);
        if (await userManager.FindByIdAsync(userId.ToString()) is not { } user)
        {
            return UserNotFound(userId, actorId);
        }

        // A26: already active, so there is nothing to change.
        if (user.IsActive)
        {
            return TypedResults.NoContent();
        }

        // The security stamp stays as it is, so tokens from before the deactivation stay dead (A23).
        user.Reactivate();
        ThrowIfFailed(await userManager.UpdateAsync(user));
        LogUserReactivated(logger, userId, actorId);
        return TypedResults.NoContent();
    }

    private ProblemHttpResult UserNotFound(Guid userId, string? actorId)
    {
        LogActivationRejected(logger, ActivationRejection.UserNotFound, userId, actorId);
        return TypedResults.Problem(title: UserNotFoundTitle, statusCode: StatusCodes.Status404NotFound);
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            var reasons = string.Join(" ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"The user's active flag could not be saved: {reasons}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Staff user {UserId} deactivated by {ActorId}")]
    private static partial void LogUserDeactivated(ILogger logger, Guid userId, string? actorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Staff user {UserId} reactivated by {ActorId}")]
    private static partial void LogUserReactivated(ILogger logger, Guid userId, string? actorId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Activation change rejected: {Rejection} for user {UserId}, requested by {ActorId}")]
    private static partial void LogActivationRejected(ILogger logger, ActivationRejection rejection, Guid userId, string? actorId);

    private enum ActivationRejection
    {
        UserNotFound,
        OwnAccount,
    }
}