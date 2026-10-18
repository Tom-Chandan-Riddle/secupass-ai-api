using Microsoft.AspNetCore.Authentication.BearerToken;

namespace SecuPass.Api.Features.Identity.Login;

public static class LoginEndpoint
{
    public static RouteHandlerBuilder MapLogin(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("/login", (LoginRequest request, LoginHandler handler) => handler.HandleAsync(request))
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Log in with email and password and get a bearer token")
            .Produces<AccessTokenResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
}