using System.Security.Claims;

namespace SecuPass.Api.Features.Identity.UserActivation;

public static class UserActivationEndpoints
{
    public static IEndpointRouteBuilder MapUserActivation(this IEndpointRouteBuilder users)
    {
        users.MapPost(
                "/{userId:guid}/deactivate",
                (Guid userId, ClaimsPrincipal actor, UserActivationHandler handler, CancellationToken cancellationToken) =>
                    handler.DeactivateAsync(userId, actor, cancellationToken))
            .WithName("DeactivateUser")
            .WithSummary("Deactivate a user, so their tokens stop working on the next request and they can't log in")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        users.MapPost(
                "/{userId:guid}/reactivate",
                (Guid userId, ClaimsPrincipal actor, UserActivationHandler handler, CancellationToken cancellationToken) =>
                    handler.ReactivateAsync(userId, actor, cancellationToken))
            .WithName("ReactivateUser")
            .WithSummary("Reactivate a user, so they can log in again")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return users;
    }
}