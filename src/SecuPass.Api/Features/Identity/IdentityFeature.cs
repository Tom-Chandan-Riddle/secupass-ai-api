using Microsoft.AspNetCore.Identity;

using SecuPass.Api.Features.Identity.CurrentUser;
using SecuPass.Api.Features.Identity.Login;
using SecuPass.Api.Features.Identity.Refresh;
using SecuPass.Infrastructure.Identity;
using SecuPass.Infrastructure.Persistence;

namespace SecuPass.Api.Features.Identity;

public static class IdentityFeature
{
    public static IServiceCollection AddIdentityFeature(this IServiceCollection services)
    {
        // Bearer tokens are the only scheme, so no cookie can authenticate a request.
        services.AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme);
        services.AddAuthorization();

        // A16: Identity's default password rules, lockout and token lifetimes.
        services.AddIdentityCore<StaffUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<SecuPassDbContext>()
            .AddSignInManager();

        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        return services;
    }

    // D13: our own endpoints instead of MapIdentityApi, which would also map /register.
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth").WithTags("Identity");
        auth.MapLogin();
        auth.MapRefresh();
        auth.MapGetCurrentUser();
        return endpoints;
    }
}