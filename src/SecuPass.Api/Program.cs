using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using Scalar.AspNetCore;

using SecuPass.Api.Features.Identity;
using SecuPass.Api.Infrastructure;
using SecuPass.Infrastructure;
using SecuPass.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddStructuredLogging(builder.Configuration);
builder.Services.AddErrorHandling();
builder.Services.AddValidation();
builder.Services.AddPersistence();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddIdentityFeature();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRequestLogging();
app.UseErrorHandling();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // Outside Development, migrations are applied as a deployment step.
    await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

// Liveness only: no registered check runs here, so /health never touches a dependency.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthCheckTags.Readiness),
}).AllowAnonymous();

// D14: features map onto this group, so their endpoints need a logged-in user unless marked anonymous.
// A fallback policy would also turn anonymous 404s and 405s into 401s.
var authenticatedEndpoints = app.MapGroup(string.Empty).RequireAuthorization();
authenticatedEndpoints.MapIdentityEndpoints();

app.Run();