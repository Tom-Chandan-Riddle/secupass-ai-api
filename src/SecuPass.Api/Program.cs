using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using Scalar.AspNetCore;

using SecuPass.Api.Infrastructure;
using SecuPass.Infrastructure;
using SecuPass.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStructuredLogging(builder.Configuration);
builder.Services.AddErrorHandling();
builder.Services.AddPersistence();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRequestLogging();
app.UseErrorHandling();

if (app.Environment.IsDevelopment())
{
    // Outside Development, migrations are applied as a deployment step.
    await app.Services.MigrateDatabaseAsync(app.Lifetime.ApplicationStopping);
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Liveness only: no registered check runs here, so /health never touches a dependency.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(HealthCheckTags.Readiness),
});

app.Run();