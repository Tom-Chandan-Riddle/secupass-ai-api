using Microsoft.AspNetCore.Identity;

using SecuPass.Api.Features.Identity.ActiveUser;
using SecuPass.Api.Features.Identity.CreateUser;
using SecuPass.Api.Features.Identity.CurrentUser;
using SecuPass.Api.Features.Identity.DevelopmentAdmin;
using SecuPass.Api.Features.Identity.Login;
using SecuPass.Api.Features.Identity.Refresh;
using SecuPass.Api.Features.Identity.UserActivation;
using SecuPass.Domain.Identity;
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
        // A22: the user name is the email, so any character a valid email can hold is allowed.
        services.AddIdentityCore<StaffUser>(options => options.User.AllowedUserNameCharacters = string.Empty)
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<SecuPassDbContext>()
            .AddSignInManager();

        services.AddOptions<DevelopmentAdminOptions>()
            .BindConfiguration(DevelopmentAdminOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<DummyPasswordCheck>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshHandler>();
        services.AddScoped<DevelopmentAdminSeeder>();
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UserActivationHandler>();
        return services;
    }

    public static async Task SeedDevelopmentAdminAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentAdminSeeder>().SeedAsync(cancellationToken);
    }

    // A23: runs after authentication, so it sees the token's user before authorization does.
    public static IApplicationBuilder UseActiveUserCheck(this IApplicationBuilder app) =>
        app.UseMiddleware<ActiveUserMiddleware>();

    // D13: our own endpoints instead of MapIdentityApi, which would also map /register.
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth").WithTags("Identity");
        auth.MapLogin();
        auth.MapRefresh();
        auth.MapGetCurrentUser();

        var users = endpoints.MapGroup("/users").WithTags("Users")
            .RequireAuthorization(policy => policy.RequireRole(StaffRoles.Admin));
        users.MapCreateUser();
        users.MapUserActivation();
        return endpoints;
    }
}