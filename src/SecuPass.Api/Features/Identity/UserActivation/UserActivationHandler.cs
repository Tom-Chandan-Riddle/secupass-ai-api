using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

using SecuPass.Infrastructure.Identity;
using SecuPass.Infrastructure.Persistence;

namespace SecuPass.Api.Features.Identity.UserActivation;

public sealed partial class UserActivationHandler(
    UserManager<StaffUser> userManager,
    SecuPassDbContext dbContext,
    ILogger<UserActivationHandler> logger)
{
    private const string UserNotFoundTitle = "User not found";
    private const string OwnAccountTitle = "You can't deactivate your own account";

    // A29: the first attempt plus one retry after a concurrency conflict.
    private const int MaxSaveAttempts = 2;

    public async Task<Results<NoContent, ProblemHttpResult>> DeactivateAsync(
        Guid userId, ClaimsPrincipal actor, CancellationToken cancellationToken)
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

        if (await ChangeActivationAsync(user, makeActive: false, cancellationToken))
        {
            LogUserDeactivated(logger, userId, actorId);
        }

        return TypedResults.NoContent();
    }

    public async Task<Results<NoContent, ProblemHttpResult>> ReactivateAsync(
        Guid userId, ClaimsPrincipal actor, CancellationToken cancellationToken)
    {
        var actorId = userManager.GetUserId(actor);
        if (await userManager.FindByIdAsync(userId.ToString()) is not { } user)
        {
            return UserNotFound(userId, actorId);
        }

        if (await ChangeActivationAsync(user, makeActive: true, cancellationToken))
        {
            LogUserReactivated(logger, userId, actorId);
        }

        return TypedResults.NoContent();
    }

    // Returns whether this request changed the user, so the business event is logged once.
    private async Task<bool> ChangeActivationAsync(StaffUser user, bool makeActive, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            // A26: already in the asked-for state, so there is nothing to change.
            if (user.IsActive == makeActive)
            {
                return false;
            }

            var result = makeActive ? await SaveReactivationAsync(user) : await SaveDeactivationAsync(user);
            if (result.Succeeded)
            {
                return true;
            }

            if (attempt == MaxSaveAttempts || !IsConcurrencyFailure(result))
            {
                ThrowFailed(result);
            }

            // A29: another request changed the user first, so re-read it and decide again.
            await dbContext.Entry(user).ReloadAsync(cancellationToken);
        }
    }

    // Saves the flag together with a new stamp, so the user's existing tokens stop counting (A23).
    private Task<IdentityResult> SaveDeactivationAsync(StaffUser user)
    {
        user.Deactivate();
        return userManager.UpdateSecurityStampAsync(user);
    }

    // The security stamp stays as it is, so tokens from before the deactivation stay dead (A23).
    private Task<IdentityResult> SaveReactivationAsync(StaffUser user)
    {
        user.Reactivate();
        return userManager.UpdateAsync(user);
    }

    private static bool IsConcurrencyFailure(IdentityResult result) =>
        result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure));

    private ProblemHttpResult UserNotFound(Guid userId, string? actorId)
    {
        LogActivationRejected(logger, ActivationRejection.UserNotFound, userId, actorId);
        return TypedResults.Problem(title: UserNotFoundTitle, statusCode: StatusCodes.Status404NotFound);
    }

    [DoesNotReturn]
    private static void ThrowFailed(IdentityResult result)
    {
        var reasons = string.Join(" ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"The user's active flag could not be saved: {reasons}");
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