using System.Security.Claims;

using Microsoft.AspNetCore.Http.HttpResults;

namespace SecuPass.Api.Features.Identity.CurrentUser;

public static class GetCurrentUserEndpoint
{
    public static RouteHandlerBuilder MapGetCurrentUser(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithSummary("Get the logged-in user's ID, email and role")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    // Read from the token's claims, which login filled from the user and their role.
    private static Ok<CurrentUserResponse> GetCurrentUser(ClaimsPrincipal user) =>
        TypedResults.Ok(new CurrentUserResponse(
            Guid.Parse(RequiredClaim(user, ClaimTypes.NameIdentifier)),
            RequiredClaim(user, ClaimTypes.Email),
            RequiredClaim(user, ClaimTypes.Role)));

    // This API issues and protects its tokens, so a missing claim is a bug, not bad input.
    private static string RequiredClaim(ClaimsPrincipal user, string claimType) =>
        user.FindFirstValue(claimType) ?? throw new InvalidOperationException($"The token has no {claimType} claim.");
}