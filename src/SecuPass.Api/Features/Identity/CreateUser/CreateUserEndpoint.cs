using System.Security.Claims;

namespace SecuPass.Api.Features.Identity.CreateUser;

public static class CreateUserEndpoint
{
    public static RouteHandlerBuilder MapCreateUser(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(
                string.Empty,
                (CreateUserRequest request, ClaimsPrincipal actor, CreateUserHandler handler, CancellationToken cancellationToken) =>
                    handler.HandleAsync(request, actor, cancellationToken))
            .WithName("CreateUser")
            .WithSummary("Create an Admin or Staff user")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
}