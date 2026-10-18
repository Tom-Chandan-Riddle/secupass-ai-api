using Microsoft.AspNetCore.Authentication.BearerToken;

namespace SecuPass.Api.Features.Identity.Refresh;

public static class RefreshEndpoint
{
    public static RouteHandlerBuilder MapRefresh(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/refresh", (RefreshRequest request, RefreshHandler handler) => handler.HandleAsync(request))
            .AllowAnonymous()
            .WithName("RefreshToken")
            .WithSummary("Exchange a refresh token for a new bearer token")
            .Produces<AccessTokenResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
}